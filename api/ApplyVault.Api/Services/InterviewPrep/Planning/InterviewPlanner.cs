using ApplyVault.Api.Services.InterviewPrep.Ai;
using ApplyVault.Api.Services.InterviewPrep.Ai.Contracts;
using ApplyVault.Api.Services.InterviewPrep.Catalogs;
using ApplyVault.Api.Services.InterviewPrep.Domain;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.InterviewPrep.Planning;

public interface IInterviewPlanner
{
    Task<InterviewPlan> PlanAsync(
        InterviewPrepPlanningContext context,
        InterviewBrief brief,
        IReadOnlyList<InterviewPrepBankQuestion> spokenQuestions,
        CancellationToken cancellationToken = default);
}

public sealed class InterviewPlanner(
    IInterviewPrepAiGateway aiGateway,
    IInterviewPrepCompetencyCatalog competencyCatalog,
    IInterviewPrepModeCatalog modeCatalog,
    IInterviewPrepPersonaCatalog personaCatalog,
    IInterviewPrepCaseCatalog caseCatalog,
    IOptions<InterviewPrepOptions> options) : IInterviewPlanner
{
    public async Task<InterviewPlan> PlanAsync(
        InterviewPrepPlanningContext context,
        InterviewBrief brief,
        IReadOnlyList<InterviewPrepBankQuestion> spokenQuestions,
        CancellationToken cancellationToken = default)
    {
        var mode = modeCatalog.Get(context.Mode);
        var persona = personaCatalog.Get(context.Persona);
        var prioritized = BuildPrioritizedCompetencies(context, mode, persona, brief);

        var config = new InterviewPrepAiSessionConfig(
            InterviewPrepEnumNames.ToWire(context.Mode),
            InterviewPrepEnumNames.ToWire(context.Persona),
            InterviewPrepEnumNames.ToWire(context.Language),
            InterviewPrepEnumNames.ToWire(context.Market),
            InterviewPrepEnumNames.ToWire(context.ExperienceType),
            InterviewPrepEnumNames.ToWire(context.InteractionType));

        var aiBrief = new CreateInterviewBriefResponse(
            brief.Summary,
            brief.Themes,
            brief.Risks,
            brief.TalkingPoints);

        InterviewPrepAiExecutionResult<PlanInterviewResponse>? aiResult = null;
        try
        {
            aiResult = await aiGateway.PlanInterviewAsync(
                new PlanInterviewRequest(config, aiBrief),
                cancellationToken);
        }
        catch (Exception)
        {
            aiResult = null;
        }

        if (aiResult is null || !aiResult.Succeeded || aiResult.Value is null)
        {
            return BuildPlan(
                context,
                brief,
                spokenQuestions,
                prioritized,
                aiStages: null,
                source: InterviewPrepArtifactSources.DeterministicFallback,
                usedAiFallback: true);
        }

        return BuildPlan(
            context,
            brief,
            spokenQuestions,
            prioritized,
            aiResult.Value.Stages,
            source: InterviewPrepArtifactSources.Ai,
            usedAiFallback: false,
            planSummaryOverride: aiResult.Value.PlanSummary);
    }

    private InterviewPlan BuildPlan(
        InterviewPrepPlanningContext context,
        InterviewBrief brief,
        IReadOnlyList<InterviewPrepBankQuestion> spokenQuestions,
        IReadOnlyList<(string CompetencyId, int Priority)> prioritized,
        IReadOnlyList<InterviewPrepAiPlannedStage>? aiStages,
        string source,
        bool usedAiFallback,
        string? planSummaryOverride = null)
    {
        var mode = modeCatalog.Get(context.Mode);
        var loopGuard = options.Value.LoopGuard;

        var competencies = prioritized
            .Select((entry) =>
            {
                competencyCatalog.TryGet(entry.CompetencyId, out var definition);
                var unknownMissing = brief.Unknowns.Count > 0
                    && entry.Priority == 1
                    && brief.Unknowns.Any((unknown) =>
                        unknown.Signal.Contains("cv_section_unknown", StringComparison.OrdinalIgnoreCase));

                return new InterviewPlanCompetency(
                    CompetencyId: entry.CompetencyId,
                    DisplayName: definition?.DisplayName ?? entry.CompetencyId,
                    Priority: entry.Priority,
                    InitialCoverageState: InterviewPrepCatalogNames.ToWire(
                        unknownMissing ? InterviewCoverageState.Unknown : InterviewCoverageState.NotStarted),
                    ExpectedEvidenceClassification: InterviewPrepCatalogNames.ToWire(
                        InterviewEvidenceClassification.Observed),
                    ExpectedEvidenceStrength: InterviewPrepCatalogNames.ToWire(
                        InterviewEvidenceStrength.Moderate),
                    ExpectedEvidenceConfidence: InterviewPrepCatalogNames.ToWire(
                        InterviewEvidenceConfidence.Medium));
            })
            .ToArray();

        var intents = BuildQuestionIntents(spokenQuestions, prioritized, mode.DefaultProbeBudget);
        var budgets = new InterviewPlanBudgets(
            MaxQuestions: Math.Max(spokenQuestions.Count, mode.DefaultQuestionBudget),
            MaxProbes: mode.DefaultProbeBudget * Math.Max(1, competencies.Length),
            MaxTurns: loopGuard.MaxSessionTurns);

        var languageAllocation = InterviewPrepTurnLanguage.BuildAllocation(
            context.Language,
            budgets.MaxQuestions);

        var evidenceExpectations = competencies
            .Select((competency) => new InterviewEvidenceExpectation(
                competency.CompetencyId,
                competency.ExpectedEvidenceClassification,
                competency.ExpectedEvidenceStrength,
                competency.ExpectedEvidenceConfidence,
                InterviewPrepCatalogNames.ToWire(InterviewCoverageState.Covered)))
            .ToArray();

        var required = competencies.Take(Math.Min(3, competencies.Length)).Select((c) => c.CompetencyId).ToArray();
        var completion = new InterviewCompletionConditions(
            RequiredCompetencyIds: required,
            MinimumCoveredCompetencies: Math.Max(1, required.Length),
            RequireClosingTurn: true);

        var stages = MapStages(aiStages)
            ??
            [
                new InterviewPlanStage("opening", "Rapport and expectations", []),
                new InterviewPlanStage(
                    "core",
                    "Core competency probe",
                    prioritized.Select((entry) => entry.CompetencyId).Take(4).ToArray()),
                new InterviewPlanStage("close", "Candidate questions and wrap-up", [])
            ];

        var summary = string.IsNullOrWhiteSpace(planSummaryOverride)
            ? $"Plan for {mode.DisplayName}: {competencies.Length} competencies, {intents.Count} question intents."
            : planSummaryOverride.Trim();

        InterviewCasePlanAttachment? caseAttachment = null;
        if (context.Mode == InterviewPrepMode.ProblemSolvingCase)
        {
            var caseDefinition = caseCatalog.ResolveDefaultForSession(
                context.Comparison.JobTitle,
                context.Comparison.CompanyName);
            caseAttachment = new InterviewCasePlanAttachment(caseDefinition.CaseId, caseDefinition.Title);
            summary = $"Case plan ({caseDefinition.CaseId}): {caseDefinition.Title}. {summary}";
        }

        return new InterviewPlan(
            PlanSummary: summary,
            Competencies: competencies,
            QuestionIntents: intents,
            Budgets: budgets,
            EvidenceExpectations: evidenceExpectations,
            CompletionConditions: completion,
            Stages: stages,
            Source: source,
            UsedAiFallback: usedAiFallback,
            CaseAttachment: caseAttachment,
            LanguageAllocation: languageAllocation);
    }

    private IReadOnlyList<(string CompetencyId, int Priority)> BuildPrioritizedCompetencies(
        InterviewPrepPlanningContext context,
        InterviewModeDefinition mode,
        InterviewPersonaDefinition persona,
        InterviewBrief brief)
    {
        var ordered = new List<string>();
        void AddRange(IEnumerable<string> ids)
        {
            foreach (var raw in ids)
            {
                var id = competencyCatalog.ResolveId(raw);
                if (id is null)
                {
                    continue;
                }

                if (!ordered.Contains(id, StringComparer.Ordinal))
                {
                    ordered.Add(id);
                }
            }
        }

        AddRange(mode.PriorityCompetencyIds);
        AddRange(persona.EmphasisCompetencyIds);

        if (context.Comparison.HasJob)
        {
            AddRange([InterviewPrepCompetencyCatalog.RoleDepth]);
        }

        if (context.Mode == InterviewPrepMode.LanguagePractice)
        {
            ordered.RemoveAll((id) => string.Equals(id, InterviewPrepCompetencyCatalog.RoleDepth, StringComparison.Ordinal));
            ordered.Insert(0, InterviewPrepCompetencyCatalog.LanguageFluency);
        }

        AddRange(brief.Themes);

        if (ordered.Count == 0)
        {
            AddRange(competencyCatalog.Core.Select((definition) => definition.Id));
        }

        return ordered
            .Select((id, index) => (id, index + 1))
            .ToArray();
    }

    private IReadOnlyList<InterviewQuestionIntent> BuildQuestionIntents(
        IReadOnlyList<InterviewPrepBankQuestion> spokenQuestions,
        IReadOnlyList<(string CompetencyId, int Priority)> prioritized,
        int defaultProbeBudget)
    {
        // Spoken wording stays on stage PlanJson (fixed bank). Intents are internal goals only.
        var intents = new List<InterviewQuestionIntent>();
        for (var i = 0; i < spokenQuestions.Count; i++)
        {
            var question = spokenQuestions[i];
            var fallbackCompetency = prioritized.Count > 0
                ? prioritized[i % prioritized.Count].CompetencyId
                : InterviewPrepCompetencyCatalog.Motivation;
            var competencyId = competencyCatalog.ResolveId(question.CompetencyTag) ?? fallbackCompetency;

            intents.Add(new InterviewQuestionIntent(
                IntentId: $"intent-{i + 1:00}-{competencyId}",
                CompetencyId: competencyId,
                InternalGoal: $"Elicit evidence for {competencyId} (bank slot {i}).",
                SortOrder: i,
                ProbeBudget: defaultProbeBudget));
        }

        // Ensure every prioritized competency has at least one intent slot for M4 adaptive use.
        var covered = intents.Select((intent) => intent.CompetencyId).ToHashSet(StringComparer.Ordinal);
        var sort = intents.Count;
        foreach (var (competencyId, _) in prioritized)
        {
            if (covered.Contains(competencyId))
            {
                continue;
            }

            intents.Add(new InterviewQuestionIntent(
                IntentId: $"intent-extra-{competencyId}",
                CompetencyId: competencyId,
                InternalGoal: $"Cover remaining competency {competencyId} if adaptive runtime is enabled.",
                SortOrder: sort++,
                ProbeBudget: defaultProbeBudget));
        }

        return intents;
    }

    private IReadOnlyList<InterviewPlanStage>? MapStages(IReadOnlyList<InterviewPrepAiPlannedStage>? aiStages)
    {
        if (aiStages is null || aiStages.Count == 0)
        {
            return null;
        }

        return aiStages
            .Select((stage) => new InterviewPlanStage(
                StageKey: stage.StageKey.Trim(),
                Goal: stage.Goal.Trim(),
                CompetencyIds: (stage.CompetencyIds ?? [])
                    .Select((id) => competencyCatalog.ResolveId(id))
                    .Where((id) => !string.IsNullOrWhiteSpace(id))
                    .Select((id) => id!)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()))
            .ToArray();
    }
}
