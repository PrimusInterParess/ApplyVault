using System.Text.Json;
using ApplyVault.Api.Data;
using ApplyVault.Api.Models.InterviewPrep;
using ApplyVault.Api.Services.InterviewPrep.Ai;
using ApplyVault.Api.Services.InterviewPrep.Ai.Contracts;
using ApplyVault.Api.Services.InterviewPrep.Catalogs;
using ApplyVault.Api.Services.InterviewPrep.Domain;
using ApplyVault.Api.Services.InterviewPrep.Planning;
using ApplyVault.Api.Services.InterviewPrep.Runtime;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Services.InterviewPrep.FullLoop;

public interface IInterviewPrepFullLoopService
{
    bool IsFullLoopSession(InterviewPrepSessionEntity session);

    InterviewPrepStageEntity? GetActiveInterviewStage(InterviewPrepSessionEntity session);

    InterviewPrepStageEntity? GetNextPlannedStage(InterviewPrepSessionEntity session);

    InterviewPlan RequireStagePlan(InterviewPrepSessionEntity session, InterviewPrepStageEntity stage);

    InterviewPrepAiSessionConfig BuildStageConfig(
        InterviewPrepSessionEntity session,
        InterviewPrepStageEntity stage);

    InterviewPrepFullLoopOrchestration? ReadOrchestration(InterviewPrepSessionEntity session);

    Task PrepareFullLoopAsync(
        InterviewPrepSessionEntity session,
        InterviewPrepPlanningContext planningContext,
        InterviewBrief brief,
        CancellationToken cancellationToken = default);

    Task<bool> HandleStageInterviewEndAsync(
        InterviewPrepSessionEntity session,
        InterviewPrepStageEntity stage,
        CancellationToken cancellationToken = default);

    Task OpenPlannedStageAsync(
        InterviewPrepSessionEntity session,
        InterviewPrepStageEntity stage,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepSessionDetailDto> StartNextStageAsync(
        AppUserEntity user,
        Guid sessionId,
        string? ifMatch,
        CancellationToken cancellationToken = default);

    void ApproveLoopGuardRevisit(
        InterviewPrepSessionEntity session,
        string questionSignature,
        string reason);

    bool IsRevisitApproved(InterviewPrepSessionEntity session, string questionSignature);

    IReadOnlyList<InterviewPrepStageHandoffArtifact> ReadStageHandoffs(InterviewPrepSessionEntity session);

    Task<InterviewPrepPanelDebriefDto> GetPanelDebriefAsync(
        AppUserEntity user,
        Guid sessionId,
        CancellationToken cancellationToken = default);
}

public sealed class InterviewPrepFullLoopService(
    ApplyVaultDbContext dbContext,
    IInterviewPrepAiGateway aiGateway,
    IInterviewPlanner planner,
    IInterviewPrepQuestionBank questionBank) : IInterviewPrepFullLoopService
{
    private static readonly JsonSerializerOptions PlanSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public bool IsFullLoopSession(InterviewPrepSessionEntity session) =>
        InterviewPrepEnumNames.TryParseMode(session.Mode, out var mode)
        && mode == InterviewPrepMode.FullLoop;

    public InterviewPrepFullLoopOrchestration? ReadOrchestration(InterviewPrepSessionEntity session) =>
        IsFullLoopSession(session)
            ? InterviewPrepFullLoopSerialization.DeserializeOrchestration(session.PlanJson)
            : null;

    public InterviewPrepStageEntity? GetActiveInterviewStage(InterviewPrepSessionEntity session)
    {
        if (!IsFullLoopSession(session))
        {
            return session.Stages.OrderBy((entry) => entry.SortOrder).FirstOrDefault();
        }

        return session.Stages
            .OrderBy((entry) => entry.SortOrder)
            .FirstOrDefault((stage) =>
            {
                if (!InterviewPrepEnumNames.TryParseStageStatus(stage.Status, out var status))
                {
                    return false;
                }

                return status is InterviewPrepStageStatus.Opening
                    or InterviewPrepStageStatus.WarmUp
                    or InterviewPrepStageStatus.CoreAssessment
                    or InterviewPrepStageStatus.CandidateQuestions
                    or InterviewPrepStageStatus.Closing
                    or InterviewPrepStageStatus.AssessmentPending;
            });
    }

    public InterviewPrepStageEntity? GetNextPlannedStage(InterviewPrepSessionEntity session) =>
        session.Stages
            .OrderBy((entry) => entry.SortOrder)
            .FirstOrDefault((stage) =>
                InterviewPrepEnumNames.TryParseStageStatus(stage.Status, out var status)
                && status == InterviewPrepStageStatus.Planned);

    public InterviewPlan RequireStagePlan(InterviewPrepSessionEntity session, InterviewPrepStageEntity stage)
    {
        if (!IsFullLoopSession(session))
        {
            return RequireSessionPlan(session);
        }

        var bundle = InterviewPrepFullLoopSerialization.DeserializeStageBundle(stage.PlanJson);
        if (bundle?.InterviewPlan is null)
        {
            throw new InterviewPrepValidationException(
                "Full-loop stage plan is missing. Prepare the session first.");
        }

        return bundle.InterviewPlan;
    }

    public InterviewPrepAiSessionConfig BuildStageConfig(
        InterviewPrepSessionEntity session,
        InterviewPrepStageEntity stage)
    {
        if (!IsFullLoopSession(session))
        {
            return new InterviewPrepAiSessionConfig(
                session.Mode,
                session.Persona,
                session.Language,
                session.Market,
                session.ExperienceType,
                session.InteractionType);
        }

        var bundle = InterviewPrepFullLoopSerialization.DeserializeStageBundle(stage.PlanJson);
        if (bundle is null)
        {
            return new InterviewPrepAiSessionConfig(
                session.Mode,
                session.Persona,
                session.Language,
                session.Market,
                session.ExperienceType,
                session.InteractionType);
        }

        return new InterviewPrepAiSessionConfig(
            InterviewPrepEnumNames.ToWire(bundle.Mode),
            InterviewPrepEnumNames.ToWire(bundle.Persona),
            session.Language,
            session.Market,
            session.ExperienceType,
            session.InteractionType);
    }

    public async Task PrepareFullLoopAsync(
        InterviewPrepSessionEntity session,
        InterviewPrepPlanningContext planningContext,
        InterviewBrief brief,
        CancellationToken cancellationToken = default)
    {
        var config = new InterviewPrepAiSessionConfig(
            InterviewPrepEnumNames.ToWire(planningContext.Mode),
            InterviewPrepEnumNames.ToWire(planningContext.Persona),
            InterviewPrepEnumNames.ToWire(planningContext.Language),
            InterviewPrepEnumNames.ToWire(planningContext.Market),
            InterviewPrepEnumNames.ToWire(planningContext.ExperienceType),
            InterviewPrepEnumNames.ToWire(planningContext.InteractionType));

        var aiBrief = new CreateInterviewBriefResponse(
            brief.Summary,
            brief.Themes,
            brief.Risks,
            brief.TalkingPoints);

        InterviewPrepFullLoopOrchestration orchestration;
        try
        {
            var loopResult = await aiGateway.PlanFullLoopAsync(
                new PlanFullLoopRequest(config, aiBrief),
                cancellationToken);

            if (loopResult.Succeeded && loopResult.Value is not null)
            {
                orchestration = MapAiLoop(loopResult.Value, brief);
            }
            else
            {
                orchestration = InterviewPrepFullLoopCatalog.BuildDeterministicOrchestration(
                    $"Full loop practice for {brief.JobTitle ?? "target role"}.",
                    brief.Themes);
            }
        }
        catch (Exception)
        {
            orchestration = InterviewPrepFullLoopCatalog.BuildDeterministicOrchestration(
                $"Full loop practice for {brief.JobTitle ?? "target role"}.",
                brief.Themes);
        }

        session.PlanJson = InterviewPrepFullLoopSerialization.SerializeOrchestration(orchestration);

        var utcNow = DateTimeOffset.UtcNow;
        var existingStages = session.Stages.ToList();
        if (existingStages.Count > 0)
        {
            dbContext.InterviewPrepStages.RemoveRange(existingStages);
            session.Stages.Clear();
        }

        for (var index = 0; index < orchestration.StageSlots.Count; index++)
        {
            var slot = orchestration.StageSlots[index];
            var stageContext = planningContext with
            {
                Mode = slot.Mode,
                Persona = slot.Persona
            };
            var questions = questionBank.GetQuestions(slot.Mode, slot.Persona);
            var stageBrief = brief with
            {
                Themes = slot.CompetencyIds.Count > 0 ? slot.CompetencyIds : brief.Themes
            };
            var stagePlan = await planner.PlanAsync(stageContext, stageBrief, questions, cancellationToken);
            var bundle = new InterviewPrepStagePlanBundle(stagePlan, slot.Mode, slot.Persona);

            var stage = new InterviewPrepStageEntity
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                SortOrder = index,
                StageType = slot.StageKey,
                Status = InterviewPrepPersistence.Status(InterviewPrepStageStatus.Planned),
                PlanJson = InterviewPrepFullLoopSerialization.SerializeStageBundle(bundle),
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };

            session.Stages.Add(stage);
            dbContext.InterviewPrepStages.Add(stage);
        }
    }

    public async Task<bool> HandleStageInterviewEndAsync(
        InterviewPrepSessionEntity session,
        InterviewPrepStageEntity stage,
        CancellationToken cancellationToken = default)
    {
        if (!IsFullLoopSession(session))
        {
            return true;
        }

        var utcNow = DateTimeOffset.UtcNow;
        if (InterviewPrepEnumNames.TryParseStageStatus(stage.Status, out var stageStatus)
            && stageStatus != InterviewPrepStageStatus.Completed)
        {
            if (stageStatus != InterviewPrepStageStatus.AssessmentPending)
            {
                InterviewPrepStageStateMachine.EnsureTransition(stageStatus, InterviewPrepStageStatus.AssessmentPending);
                stage.Status = InterviewPrepPersistence.Status(InterviewPrepStageStatus.AssessmentPending);
            }

            await RecordStageAssessmentAsync(session, stage, cancellationToken);
            InterviewPrepStageStateMachine.EnsureTransition(
                InterviewPrepStageStatus.AssessmentPending,
                InterviewPrepStageStatus.Completed);
            stage.Status = InterviewPrepPersistence.Status(InterviewPrepStageStatus.Completed);
            stage.CompletedAt = utcNow;
            stage.UpdatedAt = utcNow;
        }

        AppendStageHandoff(session, stage);
        ResetPerStageRuntime(session);

        var next = GetNextPlannedStage(session);
        if (next is not null)
        {
            if (InterviewPrepEnumNames.TryParseSessionStatus(session.Status, out var sessionStatus)
                && sessionStatus == InterviewPrepSessionStatus.Completing)
            {
                InterviewPrepSessionStateMachine.EnsureTransition(
                    InterviewPrepSessionStatus.Completing,
                    InterviewPrepSessionStatus.InProgress);
                session.Status = InterviewPrepPersistence.Status(InterviewPrepSessionStatus.InProgress);
            }

            session.UpdatedAt = utcNow;
            return false;
        }

        if (InterviewPrepEnumNames.TryParseSessionStatus(session.Status, out var status)
            && status == InterviewPrepSessionStatus.InProgress)
        {
            InterviewPrepSessionStateMachine.EnsureTransition(status, InterviewPrepSessionStatus.Completing);
            session.Status = InterviewPrepPersistence.Status(InterviewPrepSessionStatus.Completing);
        }

        session.UpdatedAt = utcNow;
        return true;
    }

    public Task OpenPlannedStageAsync(
        InterviewPrepSessionEntity session,
        InterviewPrepStageEntity stage,
        CancellationToken cancellationToken = default)
    {
        if (!IsFullLoopSession(session))
        {
            throw new InterviewPrepValidationException("Session is not a full-loop interview.");
        }

        if (!InterviewPrepEnumNames.TryParseStageStatus(stage.Status, out var stageStatus)
            || stageStatus != InterviewPrepStageStatus.Planned)
        {
            throw new InterviewPrepConflictException("Only a planned stage can be opened.");
        }

        var utcNow = DateTimeOffset.UtcNow;
        InterviewPrepStageStateMachine.EnsureTransition(InterviewPrepStageStatus.Planned, InterviewPrepStageStatus.Opening);
        stage.Status = InterviewPrepPersistence.Status(InterviewPrepStageStatus.Opening);
        InterviewPrepStageStateMachine.EnsureTransition(InterviewPrepStageStatus.Opening, InterviewPrepStageStatus.CoreAssessment);
        stage.Status = InterviewPrepPersistence.Status(InterviewPrepStageStatus.CoreAssessment);
        stage.UpdatedAt = utcNow;
        session.UpdatedAt = utcNow;
        return Task.CompletedTask;
    }

    public async Task<InterviewPrepSessionDetailDto> StartNextStageAsync(
        AppUserEntity user,
        Guid sessionId,
        string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadOwnedSessionAsync(user.Id, sessionId, tracking: true, cancellationToken);
        if (!IsFullLoopSession(session))
        {
            throw new InterviewPrepValidationException("Session is not a full-loop interview.");
        }

        if (!InterviewPrepEnumNames.TryParseSessionStatus(session.Status, out var status)
            || status is not InterviewPrepSessionStatus.InProgress and not InterviewPrepSessionStatus.Ready)
        {
            throw new InterviewPrepConflictException(
                "Full-loop next stage is only available while the session is ready or inProgress.");
        }

        var active = GetActiveInterviewStage(session);
        if (active is not null
            && InterviewPrepEnumNames.TryParseStageStatus(active.Status, out var activeStatus)
            && activeStatus is not InterviewPrepStageStatus.Completed)
        {
            throw new InterviewPrepConflictException(
                "The current full-loop stage is still active. Complete it before starting the next stage.");
        }

        var next = GetNextPlannedStage(session)
            ?? throw new InterviewPrepConflictException("No further full-loop stages are planned.");

        if (status == InterviewPrepSessionStatus.Ready)
        {
            InterviewPrepSessionStateMachine.EnsureTransition(status, InterviewPrepSessionStatus.InProgress);
            session.Status = InterviewPrepPersistence.Status(InterviewPrepSessionStatus.InProgress);
            session.StartedAt ??= DateTimeOffset.UtcNow;
        }

        var utcNow = DateTimeOffset.UtcNow;
        await OpenPlannedStageAsync(session, next, cancellationToken);
        session.UpdatedAt = utcNow;
        BumpToken(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapDetail(session);
    }

    public void ApproveLoopGuardRevisit(
        InterviewPrepSessionEntity session,
        string questionSignature,
        string reason)
    {
        if (string.IsNullOrWhiteSpace(questionSignature) || string.IsNullOrWhiteSpace(reason))
        {
            throw new InterviewPrepValidationException("questionSignature and reason are required.");
        }

        var runtime = InterviewPrepCombinedRuntimeState.Read(session.RuntimeStateJson);
        var approvals = runtime.ApprovedRevisits?.ToList() ?? [];
        approvals.RemoveAll((entry) =>
            string.Equals(entry.QuestionSignature, questionSignature.Trim(), StringComparison.OrdinalIgnoreCase));
        approvals.Add(new InterviewPrepLoopGuardRevisitApproval(
            questionSignature.Trim(),
            reason.Trim()));
        runtime.ApprovedRevisits = approvals;
        InterviewPrepCombinedRuntimeState.Write(session, runtime);
    }

    public bool IsRevisitApproved(InterviewPrepSessionEntity session, string questionSignature) =>
        InterviewPrepCombinedRuntimeState.Read(session.RuntimeStateJson).ApprovedRevisits?
            .Any((entry) => string.Equals(
                entry.QuestionSignature,
                questionSignature,
                StringComparison.OrdinalIgnoreCase))
        ?? false;

    public IReadOnlyList<InterviewPrepStageHandoffArtifact> ReadStageHandoffs(InterviewPrepSessionEntity session) =>
        InterviewPrepCombinedRuntimeState.Read(session.RuntimeStateJson).StageHandoffs ?? [];

    public async Task<InterviewPrepPanelDebriefDto> GetPanelDebriefAsync(
        AppUserEntity user,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadOwnedSessionAsync(user.Id, sessionId, tracking: true, cancellationToken);
        if (!IsFullLoopSession(session))
        {
            throw new InterviewPrepValidationException("Panel debrief is only available for full-loop sessions.");
        }

        EnsurePanelDebriefable(session);

        if (!string.IsNullOrWhiteSpace(session.PanelDebriefJson))
        {
            var cached = InterviewPrepFullLoopSerialization.DeserializePanelDebrief(session.PanelDebriefJson);
            if (cached is not null)
            {
                return MapPanelDebrief(cached);
            }
        }

        var artifact = await BuildPanelDebriefAsync(session, cancellationToken);
        session.PanelDebriefJson = InterviewPrepFullLoopSerialization.SerializePanelDebrief(artifact);
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapPanelDebrief(artifact);
    }

    private async Task<InterviewPrepPanelDebriefArtifact> BuildPanelDebriefAsync(
        InterviewPrepSessionEntity session,
        CancellationToken cancellationToken)
    {
        var orchestration = ReadOrchestration(session)
            ?? throw new InterviewPrepValidationException("Full-loop orchestration is missing.");

        var stageAssessments = InterviewPrepFullLoopSerialization.DeserializeStageAssessments(session.StageAssessmentsJson);
        var handoffs = ReadStageHandoffs(session);
        var evidenceHighlights = session.EvidenceItems
            .OrderBy((item) => item.CreatedAt)
            .Take(8)
            .Select((item) => $"{item.CompetencyId}: {Truncate(item.Claim, 120)}")
            .ToArray();

        var missingEvidence = session.CompetencyCoverages
            .Where((coverage) =>
                string.Equals(
                    coverage.CoverageState,
                    InterviewPrepCatalogNames.ToWire(InterviewCoverageState.NotStarted),
                    StringComparison.Ordinal)
                || string.Equals(
                    coverage.CoverageState,
                    InterviewPrepCatalogNames.ToWire(InterviewCoverageState.InProgress),
                    StringComparison.Ordinal))
            .Take(6)
            .Select((coverage) => new InterviewPrepPanelMissingEvidenceDto(
                coverage.CompetencyId,
                "Insufficient evidence across full-loop stages."))
            .ToArray();

        var contradictions = BuildContradictions(stageAssessments);
        var strengths = session.EvidenceItems
            .Where((item) => string.Equals(item.Polarity, "positive", StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .Select((item) => item.Claim)
            .ToArray();
        var gaps = missingEvidence.Select((entry) => entry.Signal).ToArray();

        var conversationSummary = session.ConversationSummary ?? orchestration.LoopSummary;
        var config = new InterviewPrepAiSessionConfig(
            session.Mode,
            session.Persona,
            session.Language,
            session.Market,
            session.ExperienceType,
            session.InteractionType);

        GeneratePanelDebriefResponse? aiDebrief = null;
        string source = InterviewPrepArtifactSources.DeterministicFallback;
        var usedFallback = true;
        try
        {
            var execution = await aiGateway.GeneratePanelDebriefAsync(
                new GeneratePanelDebriefRequest(config, conversationSummary, strengths, gaps),
                cancellationToken);
            if (execution.Succeeded && execution.Value is not null)
            {
                aiDebrief = execution.Value;
                source = InterviewPrepArtifactSources.Ai;
                usedFallback = false;
            }
        }
        catch (Exception)
        {
            aiDebrief = null;
        }

        aiDebrief ??= new GeneratePanelDebriefResponse(
            OverallDebrief: Truncate(conversationSummary, 400),
            Perspectives:
            [
                new PanelPerspective("Panel chair", "Evidence-backed practice summary without score averaging.", 0),
                new PanelPerspective("Recruiter", strengths.Length > 0 ? strengths[0] : "Motivation signals need more depth.", 0)
            ]);

        var perspectives = aiDebrief.Perspectives
            .Select((perspective) => new InterviewPrepPanelPerspectiveDto(
                perspective.PersonaLabel,
                perspective.Assessment,
                perspective.Score))
            .ToArray();

        var overallConfidence = stageAssessments.Count == 0
            ? "low"
            : stageAssessments.Any((entry) => string.Equals(entry.Confidence, "high", StringComparison.OrdinalIgnoreCase))
                ? "medium"
                : "low";

        return new InterviewPrepPanelDebriefArtifact(
            aiDebrief.OverallDebrief,
            perspectives,
            evidenceHighlights,
            contradictions,
            missingEvidence,
            overallConfidence,
            source,
            usedFallback,
            DateTimeOffset.UtcNow);
    }

    private static IReadOnlyList<string> BuildContradictions(
        IReadOnlyList<InterviewPrepStageAssessmentEntry> stageAssessments)
    {
        var highlights = stageAssessments
            .SelectMany((entry) => entry.Highlights)
            .Where((text) => !string.IsNullOrWhiteSpace(text))
            .Take(4)
            .ToArray();
        var missed = stageAssessments
            .SelectMany((entry) => entry.MissedGoals)
            .Where((text) => !string.IsNullOrWhiteSpace(text))
            .Take(4)
            .ToArray();

        if (highlights.Length == 0 || missed.Length == 0)
        {
            return [];
        }

        return [ $"Stage evidence tension: {Truncate(highlights[0], 80)} vs {Truncate(missed[0], 80)}" ];
    }

    private async Task RecordStageAssessmentAsync(
        InterviewPrepSessionEntity session,
        InterviewPrepStageEntity stage,
        CancellationToken cancellationToken)
    {
        var snippets = session.Turns
            .Where((turn) => turn.StageId == stage.Id)
            .OrderBy((turn) => turn.Sequence)
            .Select((turn) => new InterviewPrepAiTurnSnippet(turn.Role, Truncate(turn.Text, 400), turn.CompetencyTag))
            .ToArray();

        var orchestration = ReadOrchestration(session);
        var slot = orchestration?.StageSlots.FirstOrDefault((entry) => entry.StageKey == stage.StageType);
        var request = new EvaluateStageRequest(
            stage.StageType,
            slot?.Goal ?? stage.StageType,
            snippets);

        EvaluateStageResponse assessment;
        try
        {
            var execution = await aiGateway.EvaluateStageAsync(request, cancellationToken);
            assessment = execution.Succeeded && execution.Value is not null
                ? execution.Value
                : FakeDeterministicInterviewPrepAiProvider.SafeStageFallback(request);
        }
        catch (Exception)
        {
            assessment = FakeDeterministicInterviewPrepAiProvider.SafeStageFallback(request);
        }

        var entries = InterviewPrepFullLoopSerialization.DeserializeStageAssessments(session.StageAssessmentsJson).ToList();
        entries.RemoveAll((entry) => entry.StageId == stage.Id);
        entries.Add(new InterviewPrepStageAssessmentEntry(
            stage.Id,
            stage.StageType,
            assessment.Summary,
            assessment.AchievedGoals,
            assessment.MissedGoals,
            assessment.Score >= 70 ? "high" : assessment.Score >= 50 ? "medium" : "low"));
        session.StageAssessmentsJson = InterviewPrepFullLoopSerialization.SerializeStageAssessments(entries);
    }

    private void AppendStageHandoff(InterviewPrepSessionEntity session, InterviewPrepStageEntity stage)
    {
        var entries = InterviewPrepFullLoopSerialization.DeserializeStageAssessments(session.StageAssessmentsJson);
        var assessment = entries.FirstOrDefault((entry) => entry.StageId == stage.Id);
        var competencies = session.CompetencyCoverages
            .Where((coverage) =>
                string.Equals(
                    coverage.CoverageState,
                    InterviewPrepCatalogNames.ToWire(InterviewCoverageState.Covered),
                    StringComparison.Ordinal)
                || string.Equals(
                    coverage.CoverageState,
                    InterviewPrepCatalogNames.ToWire(InterviewCoverageState.InProgress),
                    StringComparison.Ordinal))
            .Select((coverage) => coverage.CompetencyId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var stories = session.Turns
            .Where((turn) =>
                turn.StageId == stage.Id
                && string.Equals(
                    turn.Role,
                    InterviewPrepPersistence.Role(InterviewPrepTurnRole.Candidate),
                    StringComparison.Ordinal))
            .Take(3)
            .Select((turn) => Truncate(turn.Text, 160))
            .ToArray();

        var unresolved = assessment?.MissedGoals ?? [];

        var handoff = new InterviewPrepStageHandoffArtifact(
            stage.StageType,
            assessment?.Summary ?? $"Stage {stage.StageType} completed.",
            competencies,
            stories,
            unresolved);

        var runtime = InterviewPrepCombinedRuntimeState.Read(session.RuntimeStateJson);
        var handoffs = runtime.StageHandoffs?.ToList() ?? [];
        handoffs.RemoveAll((entry) => string.Equals(entry.StageKey, stage.StageType, StringComparison.Ordinal));
        handoffs.Add(handoff);
        runtime.StageHandoffs = handoffs;
        InterviewPrepCombinedRuntimeState.Write(session, runtime);
    }

    private static void ResetPerStageRuntime(InterviewPrepSessionEntity session)
    {
        var runtime = InterviewPrepCombinedRuntimeState.Read(session.RuntimeStateJson);
        runtime.MainQuestionCount = 0;
        runtime.ConsecutiveNoProgress = 0;
        runtime.FollowUpsForCurrentIntent = 0;
        runtime.CurrentIntentId = null;
        runtime.CurrentCompetencyId = null;
        runtime.Case = null;
        InterviewPrepCombinedRuntimeState.Write(session, runtime);
    }

    private static InterviewPrepFullLoopOrchestration MapAiLoop(
        PlanFullLoopResponse aiLoop,
        InterviewBrief brief)
    {
        var catalog = InterviewPrepFullLoopCatalog.StandardStageSlots();
        var slots = new List<InterviewPrepFullLoopStageSlot>();
        for (var index = 0; index < aiLoop.Stages.Count; index++)
        {
            var aiStage = aiLoop.Stages[index];
            var template = catalog[Math.Min(index, catalog.Count - 1)];
            slots.Add(new InterviewPrepFullLoopStageSlot(
                string.IsNullOrWhiteSpace(aiStage.StageKey) ? template.StageKey : aiStage.StageKey,
                string.IsNullOrWhiteSpace(aiStage.Goal) ? template.Goal : aiStage.Goal,
                template.Mode,
                template.Persona,
                aiStage.CompetencyIds.Count > 0 ? aiStage.CompetencyIds : brief.Themes));
        }

        return new InterviewPrepFullLoopOrchestration(
            aiLoop.LoopSummary,
            aiLoop.CoverageGoals,
            slots,
            InterviewPrepArtifactSources.Ai,
            UsedAiFallback: false);
    }

    private static InterviewPlan RequireSessionPlan(InterviewPrepSessionEntity session)
    {
        if (string.IsNullOrWhiteSpace(session.PlanJson))
        {
            throw new InterviewPrepValidationException("Session plan is missing. Prepare the session first.");
        }

        var plan = JsonSerializer.Deserialize<InterviewPlan>(session.PlanJson, PlanSerializerOptions);
        return plan ?? throw new InterviewPrepValidationException("Session plan is invalid.");
    }

    private static void EnsurePanelDebriefable(InterviewPrepSessionEntity session)
    {
        if (!InterviewPrepEnumNames.TryParseSessionStatus(session.Status, out var status))
        {
            throw new InterviewPrepValidationException("Session status is invalid.");
        }

        if (status is not InterviewPrepSessionStatus.Completing
            and not InterviewPrepSessionStatus.Completed)
        {
            throw new InterviewPrepConflictException(
                "Panel debrief is available after all full-loop stages complete.");
        }

        var incomplete = session.Stages.Any((stage) =>
            !InterviewPrepEnumNames.TryParseStageStatus(stage.Status, out var stageStatus)
            || stageStatus != InterviewPrepStageStatus.Completed);
        if (incomplete)
        {
            throw new InterviewPrepConflictException("All full-loop stages must be completed before panel debrief.");
        }
    }

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
            .Include((session) => session.CompetencyCoverages);

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var session = await query.FirstOrDefaultAsync(
            (entry) => entry.Id == sessionId && entry.UserId == userId,
            cancellationToken);

        return session ?? throw new InterviewPrepNotFoundException();
    }

    private static void BumpToken(InterviewPrepSessionEntity session) =>
        session.ConcurrencyStamp = Guid.NewGuid();

    private static InterviewPrepPanelDebriefDto MapPanelDebrief(InterviewPrepPanelDebriefArtifact artifact) =>
        new(
            artifact.OverallDebrief,
            artifact.Perspectives
                .Select((perspective) => new InterviewPrepPanelPerspectivePublicDto(
                    perspective.PersonaLabel,
                    perspective.Assessment,
                    perspective.Score))
                .ToArray(),
            artifact.EvidenceHighlights,
            artifact.Contradictions,
            artifact.MissingEvidence
                .Select((entry) => new InterviewPrepPanelMissingEvidencePublicDto(entry.Signal, entry.Reason))
                .ToArray(),
            artifact.OverallConfidence,
            artifact.Source,
            artifact.UsedAiFallback,
            artifact.GeneratedAt);

    private static InterviewPrepSessionDetailDto MapDetail(InterviewPrepSessionEntity session) =>
        InterviewPrepSessionServiceMap.MapDetail(session);

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}

/// <summary>Shared mapping helpers for full-loop service (avoids duplicating private session mapper).</summary>
internal static class InterviewPrepSessionServiceMap
{
    internal static InterviewPrepSessionDetailDto MapDetail(InterviewPrepSessionEntity session) =>
        new(
            session.Id,
            session.Status,
            session.Mode,
            session.Persona,
            session.Language,
            session.Market,
            session.ExperienceType,
            session.InteractionType,
            session.ScrapeResultId,
            session.CvDocumentId,
            session.CatalogVersion,
            session.JobTitle,
            session.CompanyName,
            !string.IsNullOrWhiteSpace(session.CvSnapshotJson),
            !string.IsNullOrWhiteSpace(session.JobSnapshotJson),
            session.FailureReason,
            session.CreatedAt,
            session.UpdatedAt,
            session.PreparedAt,
            session.StartedAt,
            session.CompletedAt,
            session.CancelledAt,
            InterviewPrepSessionService.ToETag(session.ConcurrencyStamp),
            DeserializeBrief(session.BriefJson),
            DeserializePlan(session.PlanJson, session.Mode),
            session.Stages
                .OrderBy((stage) => stage.SortOrder)
                .Select((stage) => new InterviewPrepStageDto(
                    stage.Id,
                    stage.SortOrder,
                    stage.StageType,
                    stage.Status,
                    stage.CreatedAt,
                    stage.UpdatedAt,
                    stage.CompletedAt))
                .ToArray(),
            session.Turns
                .OrderBy((turn) => turn.Sequence)
                .Select((turn) => new InterviewPrepTurnDto(
                    turn.Id,
                    turn.StageId,
                    turn.Sequence,
                    turn.Role,
                    turn.Text,
                    turn.QuestionSignature,
                    turn.CompetencyTag,
                    turn.Language,
                    turn.ClientTurnId,
                    turn.CreatedAt))
                .ToArray());

    private static InterviewPrepBriefDto? DeserializeBrief(string? briefJson)
    {
        if (string.IsNullOrWhiteSpace(briefJson))
        {
            return null;
        }

        var brief = JsonSerializer.Deserialize<InterviewBrief>(briefJson, SerializerOptions);
        return brief is null ? null : MapBrief(brief);
    }

    private static InterviewPrepPlanDto? DeserializePlan(string? planJson, string modeWire)
    {
        if (string.IsNullOrWhiteSpace(planJson))
        {
            return null;
        }

        if (InterviewPrepEnumNames.TryParseMode(modeWire, out var mode)
            && mode == InterviewPrepMode.FullLoop)
        {
            var orchestration = InterviewPrepFullLoopSerialization.DeserializeOrchestration(planJson);
            if (orchestration is not null)
            {
                return new InterviewPrepPlanDto(
                    orchestration.LoopSummary,
                    [],
                    [],
                    new InterviewPrepPlanBudgetsDto(0, 0, 0),
                    [],
                    new InterviewPrepCompletionConditionsDto([], 0, false),
                    orchestration.StageSlots
                        .Select((slot) => new InterviewPrepPlanStageDto(slot.StageKey, slot.Goal, slot.CompetencyIds))
                        .ToArray(),
                    orchestration.Source,
                    orchestration.UsedAiFallback);
            }
        }

        var plan = JsonSerializer.Deserialize<InterviewPlan>(planJson, SerializerOptions);
        return plan is null ? null : MapPlan(plan);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static InterviewPrepBriefDto MapBrief(InterviewBrief brief) =>
        new(
            brief.Summary,
            brief.Themes,
            brief.Risks,
            brief.TalkingPoints,
            brief.Unknowns
                .Select((unknown) => new InterviewPrepBriefUnknownDto(unknown.Signal, unknown.CoverageState))
                .ToArray(),
            brief.PresentCvSectionTypes,
            brief.JobTitle,
            brief.CompanyName,
            brief.Source,
            brief.UsedAiFallback);

    private static InterviewPrepPlanDto MapPlan(InterviewPlan plan) =>
        new(
            plan.PlanSummary,
            plan.Competencies
                .Select((competency) => new InterviewPrepPlanCompetencyDto(
                    competency.CompetencyId,
                    competency.DisplayName,
                    competency.Priority,
                    competency.InitialCoverageState,
                    competency.ExpectedEvidenceClassification,
                    competency.ExpectedEvidenceStrength,
                    competency.ExpectedEvidenceConfidence))
                .ToArray(),
            plan.QuestionIntents
                .Select((intent) => new InterviewPrepQuestionIntentDto(
                    intent.IntentId,
                    intent.CompetencyId,
                    intent.InternalGoal,
                    intent.SortOrder,
                    intent.ProbeBudget))
                .ToArray(),
            new InterviewPrepPlanBudgetsDto(
                plan.Budgets.MaxQuestions,
                plan.Budgets.MaxProbes,
                plan.Budgets.MaxTurns),
            plan.EvidenceExpectations
                .Select((expectation) => new InterviewPrepEvidenceExpectationDto(
                    expectation.CompetencyId,
                    expectation.Classification,
                    expectation.MinimumStrength,
                    expectation.MinimumConfidence,
                    expectation.TargetCoverageState))
                .ToArray(),
            new InterviewPrepCompletionConditionsDto(
                plan.CompletionConditions.RequiredCompetencyIds,
                plan.CompletionConditions.MinimumCoveredCompetencies,
                plan.CompletionConditions.RequireClosingTurn),
            plan.Stages
                .Select((stage) => new InterviewPrepPlanStageDto(stage.StageKey, stage.Goal, stage.CompetencyIds))
                .ToArray(),
            plan.Source,
            plan.UsedAiFallback);
}
