using System.Text.Json;
using ApplyVault.Api.Data;
using ApplyVault.Api.Models.InterviewPrep;
using ApplyVault.Api.Services.InterviewPrep.Ai;
using ApplyVault.Api.Services.InterviewPrep.Ai.Contracts;
using ApplyVault.Api.Services.InterviewPrep.Catalogs;
using ApplyVault.Api.Services.InterviewPrep.Domain;
using ApplyVault.Api.Services.InterviewPrep.Planning;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Services.InterviewPrep.Reporting;

public interface IInterviewPrepReportingService
{
    Task<InterviewPrepTranscriptDto> GetTranscriptAsync(
        AppUserEntity user,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepCandidateReportDto> GetReportAsync(
        AppUserEntity user,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepCompetencyResultsDto> GetCompetenciesAsync(
        AppUserEntity user,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task EnsureReportGeneratedAsync(
        InterviewPrepSessionEntity session,
        CancellationToken cancellationToken = default);
}

public sealed class InterviewPrepReportingService(
    ApplyVaultDbContext dbContext,
    IInterviewPrepAiGateway aiGateway,
    IInterviewPrepCompetencyCatalog competencyCatalog,
    IInterviewContextBuilder contextBuilder) : IInterviewPrepReportingService
{
    internal const string PracticeDisclaimer =
        "Practice feedback for interview preparation only — not an employer hiring decision.";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly HashSet<string> TranscriptRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        InterviewPrepEnumNames.ToWire(InterviewPrepTurnRole.Interviewer),
        InterviewPrepEnumNames.ToWire(InterviewPrepTurnRole.Candidate)
    };

    public async Task<InterviewPrepTranscriptDto> GetTranscriptAsync(
        AppUserEntity user,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadOwnedSessionAsync(user.Id, sessionId, tracking: false, cancellationToken);
        return MapTranscript(session);
    }

    public async Task<InterviewPrepCandidateReportDto> GetReportAsync(
        AppUserEntity user,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadOwnedSessionAsync(user.Id, sessionId, tracking: true, cancellationToken);
        EnsureReportableStatus(session);
        var artifact = await EnsureArtifactAsync(session, cancellationToken);
        return MapReport(session, artifact);
    }

    public async Task<InterviewPrepCompetencyResultsDto> GetCompetenciesAsync(
        AppUserEntity user,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadOwnedSessionAsync(user.Id, sessionId, tracking: true, cancellationToken);
        EnsureReportableStatus(session);
        var artifact = await EnsureArtifactAsync(session, cancellationToken);
        return MapCompetencies(session.Id, artifact);
    }

    public async Task EnsureReportGeneratedAsync(
        InterviewPrepSessionEntity session,
        CancellationToken cancellationToken = default)
    {
        if (!IsReportableStatus(session.Status))
        {
            return;
        }

        _ = await EnsureArtifactAsync(session, cancellationToken);
    }

    private async Task<InterviewPrepReportArtifact> EnsureArtifactAsync(
        InterviewPrepSessionEntity session,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(session.CandidateReportJson))
        {
            var cached = JsonSerializer.Deserialize<InterviewPrepReportArtifact>(
                session.CandidateReportJson,
                SerializerOptions);
            if (cached is not null)
            {
                return cached;
            }
        }

        var artifact = await BuildArtifactAsync(session, cancellationToken);
        session.CandidateReportJson = JsonSerializer.Serialize(artifact, SerializerOptions);
        session.StageAssessmentsJson = JsonSerializer.Serialize(artifact.StageAssessments, SerializerOptions);
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return artifact;
    }

    private async Task<InterviewPrepReportArtifact> BuildArtifactAsync(
        InterviewPrepSessionEntity session,
        CancellationToken cancellationToken)
    {
        var plan = DeserializePlan(session.PlanJson);
        var brief = DeserializeBrief(session.BriefJson);
        var comparison = contextBuilder.CompareSnapshots(session.CvSnapshotJson, session.JobSnapshotJson);
        var config = BuildAiConfig(session);
        var turnSnippets = BuildTurnSnippets(session);
        var conversationSummary = await ResolveConversationSummaryAsync(session, turnSnippets, cancellationToken);

        var evidenceTrace = BuildEvidenceTrace(session);
        var competencyResults = BuildCompetencyResults(session, plan, evidenceTrace);
        var missingEvidence = BuildMissingEvidence(session, plan, brief, comparison, competencyResults);
        var strengths = BuildStrengths(evidenceTrace, competencyResults);
        var developmentAreas = BuildDevelopmentAreas(evidenceTrace, competencyResults, missingEvidence);
        var answerPatterns = BuildAnswerQualityPatterns(session, evidenceTrace);
        var jobCoverage = BuildJobCoverage(session, plan, brief, comparison, competencyResults);

        var stageAssessments = await BuildStageAssessmentsAsync(
            session,
            plan,
            turnSnippets,
            evidenceTrace,
            cancellationToken);

        var aiFeedback = await GenerateFeedbackAsync(
            config,
            conversationSummary,
            strengths,
            developmentAreas,
            cancellationToken);

        var usedAiFallback = stageAssessments.Any((stage) => stage.UsedAiFallback) || aiFeedback.UsedFallback;
        var summary = ComposeSummary(conversationSummary, aiFeedback.Response.OverallFeedback, stageAssessments);
        var practiceRecommendations = BuildPracticeRecommendations(aiFeedback.Response, developmentAreas, missingEvidence);
        var overallConfidence = ComputeOverallConfidence(evidenceTrace, missingEvidence, turnSnippets.Count);
        var languageFeedback = BuildLanguageFeedback(session, competencyResults);

        return new InterviewPrepReportArtifact(
            GeneratedAt: DateTimeOffset.UtcNow,
            Disclaimer: PracticeDisclaimer,
            Summary: summary,
            Strengths: strengths,
            DevelopmentAreas: developmentAreas,
            MissingEvidence: missingEvidence,
            JobCoverage: jobCoverage,
            AnswerQualityPatterns: answerPatterns,
            PracticeRecommendations: practiceRecommendations,
            OverallConfidence: overallConfidence,
            EvidenceTrace: evidenceTrace,
            StageAssessments: stageAssessments,
            CompetencyResults: competencyResults,
            LanguageFeedback: languageFeedback,
            UsedAiFallback: usedAiFallback);
    }

    private async Task<IReadOnlyList<InterviewPrepStageAssessmentArtifact>> BuildStageAssessmentsAsync(
        InterviewPrepSessionEntity session,
        InterviewPlan? plan,
        IReadOnlyList<InterviewPrepAiTurnSnippet> turnSnippets,
        IReadOnlyList<InterviewPrepEvidenceTraceArtifact> evidenceTrace,
        CancellationToken cancellationToken)
    {
        var stages = plan?.Stages is { Count: > 0 }
            ? plan.Stages
            :
            [
                new InterviewPlanStage("coreAssessment", "Core interview practice", [])
            ];

        var results = new List<InterviewPrepStageAssessmentArtifact>();
        foreach (var stage in stages)
        {
            var request = new EvaluateStageRequest(stage.StageKey, stage.Goal, turnSnippets);
            var execution = await aiGateway.EvaluateStageAsync(request, cancellationToken);
            var response = execution.Value
                ?? FakeDeterministicInterviewPrepAiProvider.SafeStageFallback(request);
            var confidence = StageConfidence(evidenceTrace, turnSnippets.Count, execution.Meta.UsedFallback);
            results.Add(new InterviewPrepStageAssessmentArtifact(
                stage.StageKey,
                response.Summary,
                response.AchievedGoals,
                response.MissedGoals,
                confidence,
                execution.Meta.UsedFallback));
        }

        return results;
    }

    private async Task<(GenerateFeedbackResponse Response, bool UsedFallback)> GenerateFeedbackAsync(
        InterviewPrepAiSessionConfig config,
        string conversationSummary,
        IReadOnlyList<string> strengths,
        IReadOnlyList<string> developmentAreas,
        CancellationToken cancellationToken)
    {
        var request = new GenerateFeedbackRequest(
            config,
            conversationSummary,
            strengths,
            developmentAreas);
        var execution = await aiGateway.GenerateFeedbackAsync(request, cancellationToken);
        return (
            execution.Value ?? FakeDeterministicInterviewPrepAiProvider.SafeFeedbackFallback(request),
            execution.Meta.UsedFallback);
    }

    private async Task<string> ResolveConversationSummaryAsync(
        InterviewPrepSessionEntity session,
        IReadOnlyList<InterviewPrepAiTurnSnippet> turnSnippets,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(session.ConversationSummary))
        {
            return session.ConversationSummary.Trim();
        }

        if (turnSnippets.Count == 0)
        {
            return "No interview turns were recorded for this session.";
        }

        var execution = await aiGateway.SummarizeConversationAsync(
            new SummarizeConversationRequest(turnSnippets),
            cancellationToken);
        return execution.Value?.Summary?.Trim()
            ?? $"Conversation with {turnSnippets.Count} turn(s).";
    }

    private static string ComposeSummary(
        string conversationSummary,
        string overallFeedback,
        IReadOnlyList<InterviewPrepStageAssessmentArtifact> stages)
    {
        var stageBit = stages.Count > 0
            ? stages[0].Summary
            : conversationSummary;
        if (string.IsNullOrWhiteSpace(overallFeedback))
        {
            return stageBit;
        }

        return string.IsNullOrWhiteSpace(stageBit)
            ? overallFeedback
            : $"{overallFeedback} {stageBit}".Trim();
    }

    private static IReadOnlyList<string> BuildPracticeRecommendations(
        GenerateFeedbackResponse feedback,
        IReadOnlyList<string> developmentAreas,
        IReadOnlyList<InterviewPrepMissingEvidenceArtifact> missingEvidence)
    {
        var items = new List<string>();
        items.AddRange(feedback.CoachingTips);
        items.AddRange(feedback.PracticeSuggestions);
        if (missingEvidence.Count > 0)
        {
            items.Add("Prepare one concrete example for areas where evidence was not yet demonstrated.");
        }

        foreach (var area in developmentAreas.Take(2))
        {
            items.Add($"Practice elaborating on: {area}");
        }

        return items
            .Where((item) => !string.IsNullOrWhiteSpace(item))
            .Select((item) => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
    }

    private static IReadOnlyList<InterviewPrepEvidenceTraceArtifact> BuildEvidenceTrace(
        InterviewPrepSessionEntity session)
    {
        var turnSequenceById = session.Turns.ToDictionary((turn) => turn.Id, (turn) => turn.Sequence);
        return session.EvidenceItems
            .OrderBy((item) => item.CreatedAt)
            .Select((item) => new InterviewPrepEvidenceTraceArtifact(
                item.CompetencyId,
                item.Claim,
                item.EvidenceQuote,
                item.CandidateTurnId is { } turnId && turnSequenceById.TryGetValue(turnId, out var sequence)
                    ? sequence
                    : null,
                item.Classification))
            .ToArray();
    }

    private IReadOnlyList<InterviewPrepCompetencyResultArtifact> BuildCompetencyResults(
        InterviewPrepSessionEntity session,
        InterviewPlan? plan,
        IReadOnlyList<InterviewPrepEvidenceTraceArtifact> evidenceTrace)
    {
        var planCompetencies = plan?.Competencies ?? [];
        var coverageById = session.CompetencyCoverages.ToDictionary(
            (coverage) => coverage.CompetencyId,
            StringComparer.OrdinalIgnoreCase);

        var competencyIds = planCompetencies
            .Select((competency) => competency.CompetencyId)
            .Concat(session.CompetencyCoverages.Select((coverage) => coverage.CompetencyId))
            .Concat(evidenceTrace.Select((trace) => trace.CompetencyId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var results = new List<InterviewPrepCompetencyResultArtifact>();
        foreach (var competencyId in competencyIds)
        {
            var displayName = planCompetencies
                .FirstOrDefault((entry) => string.Equals(entry.CompetencyId, competencyId, StringComparison.OrdinalIgnoreCase))
                ?.DisplayName;
            if (string.IsNullOrWhiteSpace(displayName)
                && competencyCatalog.TryGet(competencyId, out var definition))
            {
                displayName = definition.DisplayName;
            }

            displayName ??= competencyId;
            coverageById.TryGetValue(competencyId, out var coverage);
            var items = session.EvidenceItems
                .Where((item) => string.Equals(item.CompetencyId, competencyId, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var supporting = items
                .Where((item) => !string.Equals(item.Polarity, "negative", StringComparison.OrdinalIgnoreCase))
                .Select((item) => new InterviewPrepCompetencyEvidenceArtifact(
                    item.Claim,
                    item.EvidenceQuote,
                    item.Classification,
                    item.Strength,
                    item.Confidence))
                .ToArray();

            var observedGaps = items
                .Where((item) => string.Equals(item.Polarity, "negative", StringComparison.OrdinalIgnoreCase))
                .Select((item) => item.Claim)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var coverageState = coverage?.CoverageState
                ?? planCompetencies
                    .FirstOrDefault((entry) => string.Equals(entry.CompetencyId, competencyId, StringComparison.OrdinalIgnoreCase))
                    ?.InitialCoverageState
                ?? InterviewPrepCatalogNames.ToWire(InterviewCoverageState.Unknown);

            var confidence = CompetencyConfidence(coverageState, items.Length, coverage?.AttemptCount ?? 0);
            results.Add(new InterviewPrepCompetencyResultArtifact(
                competencyId,
                displayName,
                coverageState,
                items.Length,
                coverage?.AttemptCount ?? 0,
                confidence,
                supporting,
                observedGaps));
        }

        return results
            .OrderBy((result) => result.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<InterviewPrepMissingEvidenceArtifact> BuildMissingEvidence(
        InterviewPrepSessionEntity session,
        InterviewPlan? plan,
        InterviewBrief? brief,
        InterviewPrepSnapshotComparison comparison,
        IReadOnlyList<InterviewPrepCompetencyResultArtifact> competencyResults)
    {
        var missing = new List<InterviewPrepMissingEvidenceArtifact>();

        foreach (var unknown in brief?.Unknowns ?? [])
        {
            missing.Add(new InterviewPrepMissingEvidenceArtifact(
                unknown.Signal,
                "Context was not available during preparation; this is not scored as a weakness.",
                IsUnknownNotWeakness: true));
        }

        foreach (var signal in comparison.UnknownSignals)
        {
            missing.Add(new InterviewPrepMissingEvidenceArtifact(
                signal,
                "Snapshot gap — treated as unknown, not demonstrated weakness.",
                IsUnknownNotWeakness: true));
        }

        foreach (var competency in competencyResults)
        {
            if (competency.EvidenceCount > 0)
            {
                continue;
            }

            if (InterviewPrepCatalogNames.TryParse(competency.CoverageState, out InterviewCoverageState state)
                && state is InterviewCoverageState.Unknown or InterviewCoverageState.NotStarted)
            {
                missing.Add(new InterviewPrepMissingEvidenceArtifact(
                    $"competency:{competency.CompetencyId}",
                    "No answer evidence was collected for this competency in this session.",
                    IsUnknownNotWeakness: true));
            }
        }

        if (session.Turns.All((turn) =>
                !string.Equals(
                    turn.Role,
                    InterviewPrepPersistence.Role(InterviewPrepTurnRole.Candidate),
                    StringComparison.Ordinal)))
        {
            missing.Add(new InterviewPrepMissingEvidenceArtifact(
                "session:no_candidate_turns",
                "No candidate answers were recorded; feedback is intentionally limited.",
                IsUnknownNotWeakness: true));
        }

        return missing
            .GroupBy((entry) => entry.Signal, StringComparer.OrdinalIgnoreCase)
            .Select((group) => group.First())
            .ToArray();
    }

    private static IReadOnlyList<string> BuildStrengths(
        IReadOnlyList<InterviewPrepEvidenceTraceArtifact> evidenceTrace,
        IReadOnlyList<InterviewPrepCompetencyResultArtifact> competencyResults)
    {
        var fromEvidence = evidenceTrace
            .Where((trace) => !string.IsNullOrWhiteSpace(trace.EvidenceQuote)
                && !string.Equals(trace.Classification, InterviewPrepCatalogNames.ToWire(InterviewEvidenceClassification.Absent), StringComparison.OrdinalIgnoreCase))
            .Select((trace) => $"{trace.Claim} (supported by your answer)")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        foreach (var competency in competencyResults)
        {
            if (string.Equals(
                    competency.CoverageState,
                    InterviewPrepCatalogNames.ToWire(InterviewCoverageState.Covered),
                    StringComparison.OrdinalIgnoreCase)
                && competency.SupportingEvidence.Count > 0)
            {
                fromEvidence.Add($"Demonstrated {competency.DisplayName} with recorded evidence.");
            }
        }

        return fromEvidence
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildDevelopmentAreas(
        IReadOnlyList<InterviewPrepEvidenceTraceArtifact> evidenceTrace,
        IReadOnlyList<InterviewPrepCompetencyResultArtifact> competencyResults,
        IReadOnlyList<InterviewPrepMissingEvidenceArtifact> missingEvidence)
    {
        var missingCompetencies = missingEvidence
            .Where((entry) => entry.Signal.StartsWith("competency:", StringComparison.OrdinalIgnoreCase))
            .Select((entry) => entry.Signal["competency:".Length..])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var areas = competencyResults
            .Where((result) =>
                !missingCompetencies.Contains(result.CompetencyId)
                && result.ObservedGaps.Count > 0)
            .SelectMany((result) => result.ObservedGaps)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        areas.AddRange(
            competencyResults
                .Where((result) =>
                    !missingCompetencies.Contains(result.CompetencyId)
                    && string.Equals(
                        result.CoverageState,
                        InterviewPrepCatalogNames.ToWire(InterviewCoverageState.GapsRemain),
                        StringComparison.OrdinalIgnoreCase)
                    && result.SupportingEvidence.Count > 0)
                .Select((result) => $"Strengthen {result.DisplayName} with clearer outcomes in your examples."));

        return areas
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
    }

    private static IReadOnlyList<InterviewPrepAnswerQualityPatternArtifact> BuildAnswerQualityPatterns(
        InterviewPrepSessionEntity session,
        IReadOnlyList<InterviewPrepEvidenceTraceArtifact> evidenceTrace)
    {
        var candidateTurns = session.Turns
            .Where((turn) => string.Equals(
                turn.Role,
                InterviewPrepPersistence.Role(InterviewPrepTurnRole.Candidate),
                StringComparison.Ordinal))
            .ToArray();

        if (candidateTurns.Length == 0)
        {
            return
            [
                new InterviewPrepAnswerQualityPatternArtifact(
                    "No answers recorded",
                    "Submit answers during the session to receive answer-quality patterns.",
                    "high")
            ];
        }

        var shortCount = candidateTurns.Count((turn) => turn.Text.Trim().Length < 40);
        var detailedCount = candidateTurns.Count((turn) => turn.Text.Trim().Length >= 120);
        var patterns = new List<InterviewPrepAnswerQualityPatternArtifact>();

        if (shortCount > 0)
        {
            patterns.Add(new InterviewPrepAnswerQualityPatternArtifact(
                "Brief responses",
                $"{shortCount} answer(s) were very short; add situation, action, and measurable result when you can.",
                shortCount >= candidateTurns.Length / 2 ? "medium" : "low"));
        }

        if (detailedCount > 0)
        {
            patterns.Add(new InterviewPrepAnswerQualityPatternArtifact(
                "Detailed narratives",
                $"{detailedCount} answer(s) included enough detail to support evidence extraction.",
                "medium"));
        }

        if (evidenceTrace.Count == 0)
        {
            patterns.Add(new InterviewPrepAnswerQualityPatternArtifact(
                "Limited traceable evidence",
                "Few claims could be tied to quoted answer text; practice citing specific outcomes.",
                "medium"));
        }
        else
        {
            patterns.Add(new InterviewPrepAnswerQualityPatternArtifact(
                "Evidence-backed claims",
                $"{evidenceTrace.Count} claim(s) were linked to quoted answer text in this session.",
                "high"));
        }

        return patterns;
    }

    private static InterviewPrepJobCoverageArtifact? BuildJobCoverage(
        InterviewPrepSessionEntity session,
        InterviewPlan? plan,
        InterviewBrief? brief,
        InterviewPrepSnapshotComparison comparison,
        IReadOnlyList<InterviewPrepCompetencyResultArtifact> competencyResults)
    {
        if (!comparison.HasJob && string.IsNullOrWhiteSpace(session.JobSnapshotJson))
        {
            return null;
        }

        var themeSet = new HashSet<string>(brief?.Themes ?? [], StringComparer.OrdinalIgnoreCase);
        var requirements = (plan?.Competencies ?? [])
            .Select((competency) =>
            {
                var result = competencyResults.FirstOrDefault((entry) =>
                    string.Equals(entry.CompetencyId, competency.CompetencyId, StringComparison.OrdinalIgnoreCase));
                var aligned = themeSet.Contains(competency.CompetencyId);
                return new InterviewPrepJobRequirementCoverageArtifact(
                    competency.CompetencyId,
                    competency.DisplayName,
                    result?.CoverageState ?? competency.InitialCoverageState,
                    aligned ? "Aligned with themes from the selected job snapshot." : null,
                    result?.Confidence ?? "low");
            })
            .ToArray();

        return new InterviewPrepJobCoverageArtifact(
            session.JobTitle ?? comparison.JobTitle,
            session.CompanyName ?? comparison.CompanyName,
            requirements);
    }

    private static IReadOnlyList<string>? BuildLanguageFeedback(
        InterviewPrepSessionEntity session,
        IReadOnlyList<InterviewPrepCompetencyResultArtifact> competencyResults)
    {
        if (!InterviewPrepEnumNames.TryParseLanguage(session.Language, out var language)
            || !InterviewPrepEnumNames.TryParseMode(session.Mode, out var mode))
        {
            return null;
        }

        if (!InterviewPrepTurnLanguage.SessionUsesLanguageFeedback(language, mode))
        {
            if (!InterviewPrepEnumNames.TryParseMarket(session.Market, out var marketOnly)
                || marketOnly != InterviewPrepMarket.Danish)
            {
                return null;
            }
        }

        var feedback = new List<string>();
        var fluency = competencyResults.FirstOrDefault((result) =>
            string.Equals(result.CompetencyId, InterviewPrepCompetencyCatalog.LanguageFluency, StringComparison.Ordinal));
        if (fluency is not null)
        {
            feedback.AddRange(fluency.ObservedGaps.Take(2));
            if (fluency.EvidenceCount > 0)
            {
                feedback.Add("Language clarity was assessed separately from role competence.");
            }
        }

        if (InterviewPrepEnumNames.TryParseMarket(session.Market, out var market))
        {
            var hint = InterviewPrepLanguageMarketCatalog.MarketCoachingHint(market);
            if (!string.IsNullOrWhiteSpace(hint))
            {
                feedback.Add(hint);
            }
        }

        if (language == InterviewPrepLanguage.MixedEnglishDanish)
        {
            var tags = session.Turns
                .Where((turn) => !string.IsNullOrWhiteSpace(turn.Language))
                .Select((turn) => turn.Language!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (tags.Length >= 2)
            {
                feedback.Add($"Mixed session used planned languages: {string.Join(", ", tags)}.");
            }
        }

        return feedback.Count == 0 ? null : feedback;
    }

    private static string ComputeOverallConfidence(
        IReadOnlyList<InterviewPrepEvidenceTraceArtifact> evidenceTrace,
        IReadOnlyList<InterviewPrepMissingEvidenceArtifact> missingEvidence,
        int turnCount)
    {
        if (turnCount == 0 || evidenceTrace.Count == 0)
        {
            return "low";
        }

        if (missingEvidence.Count >= 3)
        {
            return "low";
        }

        return evidenceTrace.Count >= 3 ? "medium" : "low";
    }

    private static string CompetencyConfidence(string coverageState, int evidenceCount, int attemptCount)
    {
        if (evidenceCount >= 2 && attemptCount > 0)
        {
            return "medium";
        }

        if (InterviewPrepCatalogNames.TryParse(coverageState, out InterviewCoverageState state)
            && state == InterviewCoverageState.Unknown)
        {
            return "low";
        }

        return evidenceCount > 0 ? "medium" : "low";
    }

    private static string StageConfidence(
        IReadOnlyList<InterviewPrepEvidenceTraceArtifact> evidenceTrace,
        int turnCount,
        bool usedFallback)
    {
        if (usedFallback || turnCount == 0)
        {
            return "low";
        }

        return evidenceTrace.Count > 0 ? "medium" : "low";
    }

    private static IReadOnlyList<InterviewPrepAiTurnSnippet> BuildTurnSnippets(InterviewPrepSessionEntity session) =>
        session.Turns
            .OrderBy((turn) => turn.Sequence)
            .Where((turn) => TranscriptRoles.Contains(turn.Role))
            .Select((turn) => new InterviewPrepAiTurnSnippet(turn.Role, turn.Text, turn.CompetencyTag))
            .ToArray();

    private static InterviewPrepAiSessionConfig BuildAiConfig(InterviewPrepSessionEntity session) =>
        new(
            session.Mode,
            session.Persona,
            session.Language,
            session.Market,
            session.ExperienceType,
            session.InteractionType);

    private async Task<InterviewPrepSessionEntity> LoadOwnedSessionAsync(
        Guid userId,
        Guid sessionId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        IQueryable<InterviewPrepSessionEntity> query = dbContext.InterviewPrepSessions
            .Include((session) => session.Stages)
            .Include((session) => session.Turns)
            .Include((session) => session.EvidenceItems)
            .Include((session) => session.CompetencyCoverages)
            .Include((session) => session.QuestionAttempts);

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var session = await query.FirstOrDefaultAsync(
            (entry) => entry.Id == sessionId && entry.UserId == userId,
            cancellationToken);

        return session ?? throw new InterviewPrepNotFoundException();
    }

    private static void EnsureReportableStatus(InterviewPrepSessionEntity session)
    {
        if (!IsReportableStatus(session.Status))
        {
            throw new InterviewPrepConflictException(
                "Reports are available when the session is completing or completed.")
            {
                ErrorCode = "interview_prep_report_not_ready"
            };
        }
    }

    private static bool IsReportableStatus(string status) =>
        InterviewPrepEnumNames.TryParseSessionStatus(status, out var parsed)
        && parsed is InterviewPrepSessionStatus.Completing or InterviewPrepSessionStatus.Completed;

    private static InterviewPlan? DeserializePlan(string? planJson) =>
        string.IsNullOrWhiteSpace(planJson)
            ? null
            : JsonSerializer.Deserialize<InterviewPlan>(planJson, SerializerOptions);

    private static InterviewBrief? DeserializeBrief(string? briefJson) =>
        string.IsNullOrWhiteSpace(briefJson)
            ? null
            : JsonSerializer.Deserialize<InterviewBrief>(briefJson, SerializerOptions);

    private static InterviewPrepTranscriptDto MapTranscript(InterviewPrepSessionEntity session) =>
        new(
            session.Id,
            session.Status,
            session.Turns
                .OrderBy((turn) => turn.Sequence)
                .Where((turn) => TranscriptRoles.Contains(turn.Role))
                .Select((turn) => new InterviewPrepTranscriptTurnDto(
                    turn.Id,
                    turn.Sequence,
                    turn.Role,
                    turn.Text,
                    turn.CreatedAt))
                .ToArray());

    private static InterviewPrepCandidateReportDto MapReport(
        InterviewPrepSessionEntity session,
        InterviewPrepReportArtifact artifact) =>
        new(
            session.Id,
            session.Status,
            artifact.Disclaimer,
            artifact.Summary,
            artifact.Strengths,
            artifact.DevelopmentAreas,
            artifact.MissingEvidence
                .Select((entry) => new InterviewPrepMissingEvidenceDto(
                    entry.Signal,
                    entry.Reason,
                    entry.IsUnknownNotWeakness))
                .ToArray(),
            artifact.JobCoverage is null
                ? null
                : new InterviewPrepJobCoverageSummaryDto(
                    artifact.JobCoverage.JobTitle,
                    artifact.JobCoverage.CompanyName,
                    artifact.JobCoverage.Requirements
                        .Select((req) => new InterviewPrepJobRequirementCoverageDto(
                            req.CompetencyId,
                            req.DisplayName,
                            req.CoverageState,
                            req.JobAlignmentNote,
                            req.Confidence))
                        .ToArray()),
            artifact.AnswerQualityPatterns
                .Select((pattern) => new InterviewPrepAnswerQualityPatternDto(
                    pattern.Pattern,
                    pattern.Detail,
                    pattern.Confidence))
                .ToArray(),
            artifact.PracticeRecommendations,
            artifact.OverallConfidence,
            artifact.EvidenceTrace
                .Select((trace) => new InterviewPrepEvidenceTraceDto(
                    trace.CompetencyId,
                    trace.Claim,
                    trace.EvidenceQuote,
                    trace.CandidateTurnSequence,
                    trace.Classification))
                .ToArray(),
            artifact.StageAssessments
                .Select((stage) => new InterviewPrepStageSummaryDto(
                    stage.StageKey,
                    stage.Summary,
                    stage.Highlights,
                    stage.MissedGoals,
                    stage.Confidence))
                .ToArray(),
            artifact.LanguageFeedback,
            artifact.GeneratedAt,
            artifact.UsedAiFallback);

    private static InterviewPrepCompetencyResultsDto MapCompetencies(
        Guid sessionId,
        InterviewPrepReportArtifact artifact) =>
        new(
            sessionId,
            artifact.Disclaimer,
            artifact.CompetencyResults
                .Where((result) => !string.Equals(
                    result.CompetencyId,
                    InterviewPrepCompetencyCatalog.LanguageFluency,
                    StringComparison.Ordinal))
                .Select((result) => new InterviewPrepCompetencyResultDto(
                    result.CompetencyId,
                    result.DisplayName,
                    result.CoverageState,
                    result.EvidenceCount,
                    result.AttemptCount,
                    result.Confidence,
                    result.SupportingEvidence
                        .Select((evidence) => new InterviewPrepCompetencyEvidenceDto(
                            evidence.Claim,
                            evidence.EvidenceQuote,
                            evidence.Classification,
                            evidence.Strength,
                            evidence.Confidence))
                        .ToArray(),
                    result.ObservedGaps))
                .ToArray());
}
