using System.Text.Json;
using System.Text.Json.Serialization;
using ApplyVault.Api.Data;
using ApplyVault.Api.Infrastructure;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services.InterviewPrep.Ai;
using ApplyVault.Api.Services.InterviewPrep.Ai.Contracts;
using ApplyVault.Api.Services.InterviewPrep.Catalogs;
using ApplyVault.Api.Services.InterviewPrep.Domain;
using ApplyVault.Api.Services.InterviewPrep.FullLoop;
using ApplyVault.Api.Services.InterviewPrep.Planning;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.InterviewPrep.Runtime;

public interface IInterviewPrepAdaptiveRuntime
{
    Task StartAdaptiveAsync(
        InterviewPrepSessionEntity session,
        InterviewPrepStageEntity stage,
        CancellationToken cancellationToken = default);

    Task<(InterviewPrepTurnEntity? NextInterviewer, bool InterviewComplete)> AdvanceAfterAnswerAsync(
        InterviewPrepSessionEntity session,
        InterviewPrepStageEntity stage,
        InterviewPrepTurnEntity candidateTurn,
        InterviewPrepTurnEntity pendingQuestion,
        CancellationToken cancellationToken = default);
}

public sealed class InterviewPrepAdaptiveRuntime(
    ApplyVaultDbContext dbContext,
    IInterviewPrepAiGateway aiGateway,
    IInterviewLoopGuard loopGuard,
    IInterviewContextBuilder contextBuilder,
    IInterviewPrepQuestionBank questionBank,
    IInterviewPrepCaseRuntime caseRuntime,
    IInterviewPrepCaseCatalog caseCatalog,
    IInterviewPrepFullLoopService fullLoopService,
    IInterviewPrepDebugTraceContext debugTraceContext,
    IInterviewPrepDebugFileTraceLogger debugFileTraceLogger,
    IOptions<InterviewPrepOptions> options) : IInterviewPrepAdaptiveRuntime
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task StartAdaptiveAsync(
        InterviewPrepSessionEntity session,
        InterviewPrepStageEntity stage,
        CancellationToken cancellationToken = default)
    {
        var plan = ResolvePlan(session, stage);
        EnsureCoverageSeeded(session, plan);
        caseRuntime.EnsureSeeded(session, plan);
        var config = BuildConfig(session, stage);
        var utcNow = DateTimeOffset.UtcNow;

        ApplyStageHandoffContext(session);

        Trace(session.Id, "RUNTIME:StartAdaptive opening+firstQuestion");
        // AI calls happen before final persist of opening/first question; session already transitioned.
        var openingText = await GenerateOpeningTextAsync(session, plan, config, cancellationToken);
        Trace(session.Id,
            $"RUNTIME:openingReady len={openingText.Length} preview='{InterviewPrepDebugTraceLabels.Preview(openingText, 200)}'");
        AppendInterviewerTurn(
            session,
            stage,
            plan,
            mainQuestionIndex: 0,
            InterviewPrepRuntimeActionType.Opening,
            openingText,
            competencyTag: null,
            intentId: "opening",
            targetEvidenceKey: null,
            utcNow);

        var runtime = ReadRuntime(session);
        var first = await ResolveNextActionAndWordingAsync(
            session,
            stage,
            plan,
            config,
            runtime,
            forceAction: InterviewPrepRuntimeActionType.AskQuestion,
            cancellationToken);

        if (first is not null)
        {
            runtime.MainQuestionCount++;
            runtime.CurrentIntentId = first.IntentId;
            runtime.CurrentCompetencyId = first.CompetencyTag;
            runtime.FollowUpsForCurrentIntent = 0;
        }

        WriteRuntime(session, runtime);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<(InterviewPrepTurnEntity? NextInterviewer, bool InterviewComplete)> AdvanceAfterAnswerAsync(
        InterviewPrepSessionEntity session,
        InterviewPrepStageEntity stage,
        InterviewPrepTurnEntity candidateTurn,
        InterviewPrepTurnEntity pendingQuestion,
        CancellationToken cancellationToken = default)
    {
        var plan = ResolvePlan(session, stage);
        EnsureCoverageSeeded(session, plan);
        var config = BuildConfig(session, stage);
        var runtime = ReadRuntime(session);
        var utcNow = DateTimeOffset.UtcNow;

        // Candidate answer already persisted by caller before this method invokes AI.
        Trace(session.Id,
            $"RUNTIME:AdvanceAfterAnswer questionCompetency={InterviewPrepDebugTraceLabels.Competency(pendingQuestion.CompetencyTag)} answerLen={candidateTurn.Text?.Length ?? 0}");
        var assessment = await AssessSafeAsync(session, pendingQuestion, candidateTurn, cancellationToken);
        var progress = InterviewPrepRuntimeNames.ClassifyScore(assessment.Score);
        var progressWire = InterviewPrepRuntimeNames.ToWire(progress);
        Trace(session.Id,
            $"RUNTIME:assessed score={assessment.Score} progressClass={progressWire} summaryPreview='{InterviewPrepDebugTraceLabels.Preview(assessment.Summary, 120)}'");

        var attempt = new InterviewPrepQuestionAttemptEntity
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            InterviewerTurnId = pendingQuestion.Id,
            CandidateTurnId = candidateTurn.Id,
            IntentId = pendingQuestion.IntentId,
            CompetencyId = pendingQuestion.CompetencyTag,
            TargetEvidenceKey = pendingQuestion.TargetEvidenceKey,
            ProgressClass = progressWire,
            Score = assessment.Score,
            AssessmentJson = JsonSerializer.Serialize(assessment, SerializerOptions),
            AssessmentStatus = "complete",
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
        session.QuestionAttempts.Add(attempt);
        dbContext.InterviewPrepQuestionAttempts.Add(attempt);

        ApplyEvidenceAndCoverage(session, candidateTurn, pendingQuestion.CompetencyTag, assessment, progress, utcNow);

        if (IsCaseMode(session, stage) && plan.CaseAttachment is not null)
        {
            var caseDefinition = caseCatalog.Get(plan.CaseAttachment.CaseId);
            var caseState = caseRuntime.Read(session);
            var counters = new AdaptiveRuntimeCounters(runtime.MainQuestionCount, runtime.ConsecutiveNoProgress);
            var caseProposal = caseRuntime.TryResolveAfterAnswer(
                session,
                plan,
                caseState,
                caseDefinition,
                candidateTurn.Text,
                counters);
            caseRuntime.Write(session, caseState);

            if (caseProposal is not null)
            {
                var caseTurn = AppendInterviewerTurn(
                    session,
                    stage,
                    plan,
                    runtime.MainQuestionCount,
                    caseProposal.ActionType,
                    caseProposal.MessageText,
                    caseProposal.CompetencyId,
                    caseProposal.IntentId,
                    targetEvidenceKey: $"case:{caseProposal.IntentId}",
                    utcNow);

                if (caseProposal.ActionType is InterviewPrepRuntimeActionType.AskQuestion
                    or InterviewPrepRuntimeActionType.Probe)
                {
                    runtime.MainQuestionCount++;
                }

                if (caseRuntime.IsCaseComplete(
                        caseDefinition,
                        caseState,
                        counters with { MainQuestionCount = runtime.MainQuestionCount }))
                {
                    var caseEnd = await TransitionTowardStageEndAsync(
                        session,
                        stage,
                        plan,
                        config,
                        runtime,
                        utcNow,
                        cancellationToken);
                    runtime = ReadRuntime(session);
                    WriteRuntime(session, runtime);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    return caseEnd.Next is null ? (caseTurn, caseEnd.Complete) : caseEnd;
                }

                WriteRuntime(session, runtime);
                await dbContext.SaveChangesAsync(cancellationToken);
                return (caseTurn, false);
            }

            if (caseRuntime.IsCaseComplete(caseDefinition, caseState, counters))
            {
                var caseEnd = await TransitionTowardStageEndAsync(
                    session,
                    stage,
                    plan,
                    config,
                    runtime,
                    utcNow,
                    cancellationToken);
                runtime = ReadRuntime(session);
                WriteRuntime(session, runtime);
                await dbContext.SaveChangesAsync(cancellationToken);
                return caseEnd;
            }
        }

        if (progress == InterviewPrepProgressClass.NoProgress)
        {
            runtime.ConsecutiveNoProgress++;
        }
        else
        {
            runtime.ConsecutiveNoProgress = 0;
        }

        await UpdateConversationSummaryAsync(session, cancellationToken);

        // Persist ledger/summary before selecting next action (still no open transaction around Gemini).
        await dbContext.SaveChangesAsync(cancellationToken);

        if (ShouldForceClose(session, stage, plan, runtime))
        {
            var closing = await TransitionTowardStageEndAsync(
                session,
                stage,
                plan,
                config,
                runtime,
                utcNow,
                cancellationToken);
            runtime = ReadRuntime(session);
            WriteRuntime(session, runtime);
            await dbContext.SaveChangesAsync(cancellationToken);
            return closing;
        }

        var next = await ResolveNextActionAndWordingAsync(
            session,
            stage,
            plan,
            config,
            runtime,
            forceAction: null,
            cancellationToken);

        if (next is null)
        {
            var closing = await TransitionTowardStageEndAsync(
                session,
                stage,
                plan,
                config,
                runtime,
                utcNow,
                cancellationToken);
            runtime = ReadRuntime(session);
            WriteRuntime(session, runtime);
            await dbContext.SaveChangesAsync(cancellationToken);
            return closing;
        }

        var action = InterviewPrepRuntimeNames.NormalizeAction(next.ActionType);
        if (action is InterviewPrepRuntimeActionType.AskQuestion)
        {
            runtime.MainQuestionCount++;
            if (string.Equals(runtime.CurrentIntentId, next.IntentId, StringComparison.Ordinal))
            {
                // same intent main question counts toward repeat tracking via history
            }
            else
            {
                runtime.FollowUpsForCurrentIntent = 0;
            }

            runtime.CurrentIntentId = next.IntentId;
            runtime.CurrentCompetencyId = next.CompetencyTag;
        }
        else if (action is InterviewPrepRuntimeActionType.Probe)
        {
            runtime.FollowUpsForCurrentIntent++;
            runtime.CurrentIntentId = next.IntentId ?? runtime.CurrentIntentId;
            runtime.CurrentCompetencyId = next.CompetencyTag ?? runtime.CurrentCompetencyId;
        }
        else if (action is InterviewPrepRuntimeActionType.CandidateQuestions
            or InterviewPrepRuntimeActionType.WrapUp
            or InterviewPrepRuntimeActionType.Close
            or InterviewPrepRuntimeActionType.StageHandoff)
        {
            // Stage transitions handled inside Resolve / TransitionTowardStageEnd.
        }

        WriteRuntime(session, runtime);
        await dbContext.SaveChangesAsync(cancellationToken);

        var complete = InterviewPrepEnumNames.TryParseSessionStatus(session.Status, out var status)
            && status is InterviewPrepSessionStatus.Completing or InterviewPrepSessionStatus.Completed;
        return (next, complete);
    }

    private async Task<(InterviewPrepTurnEntity? Next, bool Complete)> TransitionTowardStageEndAsync(
        InterviewPrepSessionEntity session,
        InterviewPrepStageEntity stage,
        InterviewPlan plan,
        InterviewPrepAiSessionConfig config,
        InterviewPrepCombinedRuntimeState runtime,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        if (fullLoopService.IsFullLoopSession(session)
            && fullLoopService.GetNextPlannedStage(session) is not null
            && InterviewPrepEnumNames.TryParseStageStatus(stage.Status, out var midStatus)
            && midStatus is InterviewPrepStageStatus.CoreAssessment
                or InterviewPrepStageStatus.WarmUp
                or InterviewPrepStageStatus.Opening)
        {
            return await AutoAdvanceFullLoopStageAsync(
                session,
                stage,
                plan,
                config,
                runtime,
                utcNow,
                cancellationToken);
        }

        return await TransitionTowardCloseAsync(
            session,
            stage,
            plan,
            config,
            runtime,
            utcNow,
            cancellationToken);
    }

    private async Task<(InterviewPrepTurnEntity? Next, bool Complete)> AutoAdvanceFullLoopStageAsync(
        InterviewPrepSessionEntity session,
        InterviewPrepStageEntity stage,
        InterviewPlan plan,
        InterviewPrepAiSessionConfig config,
        InterviewPrepCombinedRuntimeState runtime,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        Trace(session.Id, "RUNTIME:fullLoop mid-stage handoff + auto-advance");

        var handoff = await ResolveNextActionAndWordingAsync(
            session,
            stage,
            plan,
            config,
            runtime,
            forceAction: InterviewPrepRuntimeActionType.StageHandoff,
            cancellationToken);

        if (handoff is null)
        {
            handoff = AppendInterviewerTurn(
                session,
                stage,
                plan,
                runtime.MainQuestionCount,
                InterviewPrepRuntimeActionType.StageHandoff,
                "Thanks — that covers what I needed for this part. I'll hand you to the next interviewer.",
                competencyTag: null,
                intentId: "stage-handoff",
                targetEvidenceKey: null,
                utcNow);
        }

        await fullLoopService.HandleStageInterviewEndAsync(session, stage, cancellationToken);

        var nextStage = fullLoopService.GetNextPlannedStage(session);
        if (nextStage is null)
        {
            WriteRuntime(session, runtime);
            return (handoff, true);
        }

        await fullLoopService.OpenPlannedStageAsync(session, nextStage, cancellationToken);
        await StartAdaptiveAsync(session, nextStage, cancellationToken);

        var latest = session.Turns
            .Where((turn) =>
                turn.StageId == nextStage.Id
                && string.Equals(
                    turn.Role,
                    InterviewPrepPersistence.Role(InterviewPrepTurnRole.Interviewer),
                    StringComparison.Ordinal))
            .OrderByDescending((turn) => turn.Sequence)
            .FirstOrDefault()
            ?? handoff;

        return (latest, false);
    }

    private async Task<(InterviewPrepTurnEntity? Next, bool Complete)> TransitionTowardCloseAsync(
        InterviewPrepSessionEntity session,
        InterviewPrepStageEntity stage,
        InterviewPlan plan,
        InterviewPrepAiSessionConfig config,
        InterviewPrepCombinedRuntimeState runtime,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        if (!InterviewPrepEnumNames.TryParseStageStatus(stage.Status, out var stageStatus))
        {
            stageStatus = InterviewPrepStageStatus.CoreAssessment;
        }

        if (stageStatus == InterviewPrepStageStatus.CoreAssessment)
        {
            InterviewPrepStageStateMachine.EnsureTransition(stageStatus, InterviewPrepStageStatus.CandidateQuestions);
            stage.Status = InterviewPrepPersistence.Status(InterviewPrepStageStatus.CandidateQuestions);
            stage.UpdatedAt = utcNow;

            var candidateQ = await ResolveNextActionAndWordingAsync(
                session,
                stage,
                plan,
                config,
                runtime,
                forceAction: InterviewPrepRuntimeActionType.CandidateQuestions,
                cancellationToken);

            if (candidateQ is not null)
            {
                WriteRuntime(session, runtime);
                return (candidateQ, false);
            }

            stageStatus = InterviewPrepStageStatus.CandidateQuestions;
        }

        if (stageStatus == InterviewPrepStageStatus.CandidateQuestions)
        {
            InterviewPrepStageStateMachine.EnsureTransition(stageStatus, InterviewPrepStageStatus.Closing);
            stage.Status = InterviewPrepPersistence.Status(InterviewPrepStageStatus.Closing);
            stage.UpdatedAt = utcNow;

            var closing = await ResolveNextActionAndWordingAsync(
                session,
                stage,
                plan,
                config,
                runtime,
                forceAction: InterviewPrepRuntimeActionType.Close,
                cancellationToken);

            // Complete only after the seeker answers Close (Closing branch below).
            WriteRuntime(session, runtime);
            return (closing, false);
        }

        if (stageStatus == InterviewPrepStageStatus.Closing)
        {
            var complete = await TryCompleteInterviewSegmentAsync(session, stage, utcNow, cancellationToken);
            return (null, complete);
        }

        var segmentComplete = await TryCompleteInterviewSegmentAsync(session, stage, utcNow, cancellationToken);
        return (null, segmentComplete);
    }

    private async Task<bool> TryCompleteInterviewSegmentAsync(
        InterviewPrepSessionEntity session,
        InterviewPrepStageEntity stage,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        if (fullLoopService.IsFullLoopSession(session))
        {
            return await fullLoopService.HandleStageInterviewEndAsync(session, stage, cancellationToken);
        }

        TransitionSessionCompleting(session, stage, utcNow);
        return true;
    }

    private void ApplyStageHandoffContext(InterviewPrepSessionEntity session)
    {
        if (!fullLoopService.IsFullLoopSession(session))
        {
            return;
        }

        var handoffs = fullLoopService.ReadStageHandoffs(session);
        if (handoffs.Count == 0)
        {
            return;
        }

        var digest = string.Join(
            " | ",
            handoffs.Select((handoff) =>
                $"{handoff.StageKey}: {Truncate(handoff.FactualSummary, 120)}"));
        session.ConversationSummary = $"Prior stage facts (no scores): {digest}";
    }

    private InterviewPlan ResolvePlan(InterviewPrepSessionEntity session, InterviewPrepStageEntity stage) =>
        fullLoopService.IsFullLoopSession(session)
            ? fullLoopService.RequireStagePlan(session, stage)
            : RequirePlan(session);

    private static void TransitionSessionCompleting(
        InterviewPrepSessionEntity session,
        InterviewPrepStageEntity stage,
        DateTimeOffset utcNow)
    {
        if (InterviewPrepEnumNames.TryParseSessionStatus(session.Status, out var status)
            && status == InterviewPrepSessionStatus.InProgress)
        {
            InterviewPrepSessionStateMachine.EnsureTransition(status, InterviewPrepSessionStatus.Completing);
            session.Status = InterviewPrepPersistence.Status(InterviewPrepSessionStatus.Completing);
        }

        if (InterviewPrepEnumNames.TryParseStageStatus(stage.Status, out var stageStatus)
            && stageStatus != InterviewPrepStageStatus.Closing
            && stageStatus != InterviewPrepStageStatus.Completed)
        {
            if (stageStatus == InterviewPrepStageStatus.CoreAssessment)
            {
                InterviewPrepStageStateMachine.EnsureTransition(stageStatus, InterviewPrepStageStatus.Closing);
                stage.Status = InterviewPrepPersistence.Status(InterviewPrepStageStatus.Closing);
                stageStatus = InterviewPrepStageStatus.Closing;
            }
            else if (stageStatus == InterviewPrepStageStatus.CandidateQuestions)
            {
                InterviewPrepStageStateMachine.EnsureTransition(stageStatus, InterviewPrepStageStatus.Closing);
                stage.Status = InterviewPrepPersistence.Status(InterviewPrepStageStatus.Closing);
                stageStatus = InterviewPrepStageStatus.Closing;
            }
        }

        stage.UpdatedAt = utcNow;
        session.UpdatedAt = utcNow;
    }

    private bool ShouldForceClose(
        InterviewPrepSessionEntity session,
        InterviewPrepStageEntity stage,
        InterviewPlan plan,
        InterviewPrepCombinedRuntimeState runtime)
    {
        if (IsCaseMode(session, stage) && plan.CaseAttachment is not null)
        {
            var caseDefinition = caseCatalog.Get(plan.CaseAttachment.CaseId);
            var caseState = caseRuntime.Read(session);
            if (!caseRuntime.IsCaseComplete(
                    caseDefinition,
                    caseState,
                    new AdaptiveRuntimeCounters(runtime.MainQuestionCount, runtime.ConsecutiveNoProgress)))
            {
                return false;
            }
        }

        if (InterviewPrepEnumNames.TryParseStageStatus(stage.Status, out var stageStatus)
            && stageStatus is InterviewPrepStageStatus.CandidateQuestions or InterviewPrepStageStatus.Closing)
        {
            return true;
        }

        if (runtime.MainQuestionCount >= Math.Max(1, plan.Budgets.MaxQuestions))
        {
            // Hard question safety only — soft TargetQuestions does not force Stage end.
            return true;
        }

        var remaining = GetRemainingCompetencyIds(session, plan);
        if (remaining.Count == 0 && runtime.MainQuestionCount > 0)
        {
            return true;
        }

        var loop = options.Value.LoopGuard;
        if (runtime.ConsecutiveNoProgress >= Math.Max(1, loop.MaxNoProgressStreak)
            && remaining.Count <= 1)
        {
            // Exhausted progress on last remaining competency — allow close with missing evidence.
            return true;
        }

        return false;
    }

    private async Task<InterviewPrepTurnEntity?> ResolveNextActionAndWordingAsync(
        InterviewPrepSessionEntity session,
        InterviewPrepStageEntity stage,
        InterviewPlan plan,
        InterviewPrepAiSessionConfig config,
        InterviewPrepCombinedRuntimeState runtime,
        InterviewPrepRuntimeActionType? forceAction,
        CancellationToken cancellationToken)
    {
        var loopOptions = EffectiveLoopGuardOptions(session);
        var covered = GetCoveredCompetencyIds(session);
        var remaining = GetRemainingCompetencyIds(session, plan);
        var recent = BuildRecentSnippets(session);

        InterviewPrepRuntimeActionType action;
        string? competencyId;
        string? intentId;
        string? targetEvidenceKey;
        string rationale;

        if (forceAction is InterviewPrepRuntimeActionType.CandidateQuestions)
        {
            action = InterviewPrepRuntimeActionType.CandidateQuestions;
            competencyId = null;
            intentId = "candidate-questions";
            targetEvidenceKey = null;
            rationale = "Transition to candidate questions.";
        }
        else if (forceAction is InterviewPrepRuntimeActionType.StageHandoff)
        {
            action = InterviewPrepRuntimeActionType.StageHandoff;
            competencyId = null;
            intentId = "stage-handoff";
            targetEvidenceKey = null;
            rationale = "Full-loop mid-stage handoff to the next interviewer.";
        }
        else if (forceAction is InterviewPrepRuntimeActionType.Close or InterviewPrepRuntimeActionType.WrapUp)
        {
            action = InterviewPrepRuntimeActionType.Close;
            competencyId = null;
            intentId = "closing";
            targetEvidenceKey = null;
            rationale = "Close the interview.";
        }
        else
        {
            var selected = await SelectActionSafeAsync(
                config,
                stage,
                session,
                recent,
                covered,
                remaining,
                cancellationToken);

            action = forceAction
                ?? InterviewPrepRuntimeNames.NormalizeAction(selected.ActionType);
            competencyId = selected.CompetencyId ?? remaining.FirstOrDefault() ?? plan.Competencies.FirstOrDefault()?.CompetencyId;
            intentId = ResolveIntentId(plan, competencyId, action);
            targetEvidenceKey = competencyId is null ? null : $"evidence:{competencyId}";
            rationale = selected.Rationale;

            // Application-owned fallbacks when AI / Loop Guard would stall.
            var proposalState = BuildRuntimeState(session, runtime, intentId, competencyId, targetEvidenceKey);
            var structural = loopGuard.EvaluateProposal(
                new InterviewLoopGuardProposal(
                    QuestionText: null,
                    CompetencyId: competencyId,
                    IntentId: intentId,
                    TargetEvidenceKey: targetEvidenceKey,
                    ActionType: action,
                    IsWordingOnly: false),
                proposalState,
                loopOptions);

            if (!structural.Accepted
                || action is InterviewPrepRuntimeActionType.AskQuestion or InterviewPrepRuntimeActionType.Probe
                    && runtime.ConsecutiveNoProgress >= Math.Max(1, loopOptions.MaxNoProgressStreak))
            {
                var fallback = ApplyDeterministicFallback(
                    session,
                    plan,
                    runtime,
                    remaining,
                    structural.Reason);
                action = fallback.Action;
                competencyId = fallback.CompetencyId;
                intentId = fallback.IntentId;
                targetEvidenceKey = fallback.TargetEvidenceKey;
                rationale = fallback.Rationale;

                if (action is InterviewPrepRuntimeActionType.CandidateQuestions
                    or InterviewPrepRuntimeActionType.WrapUp
                    or InterviewPrepRuntimeActionType.Close
                    or InterviewPrepRuntimeActionType.StageHandoff)
                {
                    return (await TransitionTowardStageEndAsync(
                        session,
                        stage,
                        plan,
                        config,
                        runtime,
                        DateTimeOffset.UtcNow,
                        cancellationToken)).Next;
                }
            }
        }

        if (action is InterviewPrepRuntimeActionType.WrapUp or InterviewPrepRuntimeActionType.Close
            && forceAction is null
            && InterviewPrepEnumNames.TryParseStageStatus(stage.Status, out var st)
            && st == InterviewPrepStageStatus.CoreAssessment)
        {
            return (await TransitionTowardStageEndAsync(
                session,
                stage,
                plan,
                config,
                runtime,
                DateTimeOffset.UtcNow,
                cancellationToken)).Next;
        }

        if (action is InterviewPrepRuntimeActionType.StageHandoff
            && forceAction is null
            && InterviewPrepEnumNames.TryParseStageStatus(stage.Status, out var handoffStatus)
            && handoffStatus == InterviewPrepStageStatus.CoreAssessment)
        {
            return (await TransitionTowardStageEndAsync(
                session,
                stage,
                plan,
                config,
                runtime,
                DateTimeOffset.UtcNow,
                cancellationToken)).Next;
        }

        var wording = await GenerateWordingWithLoopGuardAsync(
            session,
            plan,
            config,
            recent,
            action,
            competencyId,
            intentId,
            targetEvidenceKey,
            rationale,
            cancellationToken);

        if (wording is null)
        {
            return null;
        }

        var utcNow = DateTimeOffset.UtcNow;
        return AppendInterviewerTurn(
            session,
            stage,
            plan,
            runtime.MainQuestionCount,
            action,
            wording.Value.Text,
            competencyTag: competencyId,
            intentId: intentId,
            targetEvidenceKey: targetEvidenceKey,
            utcNow,
            signature: wording.Value.Signature);
    }

    private async Task<(string Text, string Signature)?> GenerateWordingWithLoopGuardAsync(
        InterviewPrepSessionEntity session,
        InterviewPlan plan,
        InterviewPrepAiSessionConfig config,
        IReadOnlyList<InterviewPrepAiTurnSnippet> recent,
        InterviewPrepRuntimeActionType action,
        string? competencyId,
        string? intentId,
        string? targetEvidenceKey,
        string rationale,
        CancellationToken cancellationToken)
    {
        var loopOptions = EffectiveLoopGuardOptions(session);
        var blocked = session.Turns
            .Where((turn) =>
                string.Equals(turn.Role, InterviewPrepPersistence.Role(InterviewPrepTurnRole.Interviewer), StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(turn.QuestionSignature))
            .Select((turn) => turn.QuestionSignature!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var topicHint = action switch
        {
            InterviewPrepRuntimeActionType.CandidateQuestions => "candidate questions",
            InterviewPrepRuntimeActionType.Close => "closing thanks",
            InterviewPrepRuntimeActionType.StageHandoff => "hand off to the next interviewer",
            _ => ResolveCompetencyTopicHint(competencyId)
        };

        string? lastText = null;
        InterviewLoopGuardDecision? lastDecision = null;

        for (var attempt = 0; attempt <= Math.Max(0, loopOptions.MaxWordingRetries); attempt++)
        {
            var message = await GenerateMessageSafeAsync(
                session,
                config,
                action,
                competencyId,
                topicHint,
                recent,
                blocked,
                cancellationToken);

            // RealisticSimulation must not leak coaching — strip coaching-like prefixes if any.
            var text = SanitizeInterviewerText(session, message.MessageText);
            var history = BuildHistory(session);
            var runtime = ReadRuntime(session);
            var state = BuildRuntimeState(session, runtime, intentId, competencyId, targetEvidenceKey);
            var decision = loopGuard.EvaluateProposal(
                new InterviewLoopGuardProposal(
                    text,
                    competencyId,
                    intentId,
                    targetEvidenceKey,
                    action,
                    IsWordingOnly: true),
                state,
                loopOptions);

            lastText = text;
            lastDecision = decision;
            if (decision.Accepted
                || fullLoopService.IsRevisitApproved(session, decision.Signature))
            {
                return (text, decision.Signature);
            }

            blocked.Add(decision.Signature);
        }

        // Deterministic fixed-bank fallback (documented: bank is fallback only, not sole driver).
        var bankFallback = TryFixedBankFallback(config, competencyId, blocked);
        if (bankFallback is not null)
        {
            var decision = loopGuard.Evaluate(bankFallback, BuildHistory(session), loopOptions);
            if (decision.Accepted)
            {
                return (bankFallback, decision.Signature);
            }
        }

        // Last resort: slightly varied close/candidate wording that Loop Guard will accept as new.
        var emergencyHint = ResolveCompetencyTopicHint(competencyId);
        var emergency = action switch
        {
            InterviewPrepRuntimeActionType.CandidateQuestions =>
                "Before we wrap up, what questions do you have for me about the role or team?",
            InterviewPrepRuntimeActionType.Close =>
                "Thank you for your time today. We'll be in touch with next steps.",
            InterviewPrepRuntimeActionType.StageHandoff =>
                "Thanks — that covers what I needed for this part. I'll hand you to the next interviewer.",
            _ => string.IsNullOrWhiteSpace(emergencyHint)
                ? "Could you share a concrete example from your recent work that we have not covered yet?"
                : $"Could you share a concrete example related to {emergencyHint} that we have not covered yet?"
        };

        var emergencyDecision = loopGuard.Evaluate(emergency, BuildHistory(session), loopOptions);
        if (emergencyDecision.Accepted)
        {
            return (emergency, emergencyDecision.Signature);
        }

        // If even emergency fails (max turns), signal completion.
        _ = lastText;
        _ = lastDecision;
        _ = plan;
        return null;
    }

    private string? TryFixedBankFallback(
        InterviewPrepAiSessionConfig config,
        string? competencyId,
        IReadOnlyList<string> blockedSignatures)
    {
        if (!InterviewPrepEnumNames.TryParseMode(config.Mode, out var mode)
            || !InterviewPrepEnumNames.TryParsePersona(config.Persona, out var persona))
        {
            return null;
        }

        var bank = questionBank.GetQuestions(mode, persona);
        foreach (var question in bank)
        {
            var signature = loopGuard.BuildSignature(question.Text);
            if (blockedSignatures.Any((b) => string.Equals(b, signature, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (competencyId is not null
                && !string.Equals(question.CompetencyTag, competencyId, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(
                    new InterviewPrepCompetencyCatalog().ResolveId(question.CompetencyTag),
                    competencyId,
                    StringComparison.Ordinal))
            {
                // Prefer matching competency but allow any unused bank entry later.
                continue;
            }

            return question.Text;
        }

        foreach (var question in bank)
        {
            var signature = loopGuard.BuildSignature(question.Text);
            if (!blockedSignatures.Any((b) => string.Equals(b, signature, StringComparison.OrdinalIgnoreCase)))
            {
                return question.Text;
            }
        }

        return null;
    }

    private (InterviewPrepRuntimeActionType Action, string? CompetencyId, string? IntentId, string? TargetEvidenceKey, string Rationale)
        ApplyDeterministicFallback(
            InterviewPrepSessionEntity session,
            InterviewPlan plan,
            InterviewPrepCombinedRuntimeState runtime,
            IReadOnlyList<string> remaining,
            string reason)
    {
        // different intent → next competency → stage handoff / candidate questions → close
        var nextCompetency = remaining
            .FirstOrDefault((id) => !string.Equals(id, runtime.CurrentCompetencyId, StringComparison.Ordinal))
            ?? remaining.FirstOrDefault()
            ?? plan.Competencies
                .Select((c) => c.CompetencyId)
                .FirstOrDefault((id) => !string.Equals(id, runtime.CurrentCompetencyId, StringComparison.Ordinal));

        if (nextCompetency is not null
            && !string.Equals(nextCompetency, runtime.CurrentCompetencyId, StringComparison.Ordinal)
            && reason is not "max_session_turns")
        {
            runtime.ConsecutiveNoProgress = 0;
            runtime.FollowUpsForCurrentIntent = 0;
            var intent = ResolveIntentId(plan, nextCompetency, InterviewPrepRuntimeActionType.AskQuestion);
            return (
                InterviewPrepRuntimeActionType.AskQuestion,
                nextCompetency,
                intent,
                $"evidence:{nextCompetency}",
                $"Loop Guard fallback ({reason}): switch competency.");
        }

        if (fullLoopService.IsFullLoopSession(session)
            && fullLoopService.GetNextPlannedStage(session) is not null)
        {
            return (
                InterviewPrepRuntimeActionType.StageHandoff,
                null,
                "stage-handoff",
                null,
                $"Loop Guard fallback ({reason}): full-loop stage handoff.");
        }

        var activeStage = fullLoopService.GetActiveInterviewStage(session);
        if (activeStage is not null
            && InterviewPrepEnumNames.TryParseStageStatus(activeStage.Status, out var stageStatus)
            && stageStatus == InterviewPrepStageStatus.CoreAssessment)
        {
            return (
                InterviewPrepRuntimeActionType.CandidateQuestions,
                null,
                "candidate-questions",
                null,
                $"Loop Guard fallback ({reason}): candidate questions.");
        }

        return (
            InterviewPrepRuntimeActionType.Close,
            null,
            "closing",
            null,
            $"Loop Guard fallback ({reason}): close.");
    }

    private async Task<string> GenerateOpeningTextAsync(
        InterviewPrepSessionEntity session,
        InterviewPlan plan,
        InterviewPrepAiSessionConfig config,
        CancellationToken cancellationToken)
    {
        if (IsCaseMode(session, null) && plan.CaseAttachment is not null)
        {
            var caseDefinition = caseCatalog.Get(plan.CaseAttachment.CaseId);
            return caseRuntime.BuildOpeningText(caseDefinition);
        }

        try
        {
            var candidateDisplayName = ResolveCandidateDisplayName(session);
            var result = await aiGateway.GenerateOpeningAsync(
                new GenerateOpeningRequest(
                    config,
                    CandidateDisplayName: candidateDisplayName,
                    RoleTitle: session.JobTitle,
                    CompanyName: session.CompanyName),
                cancellationToken);

            if (result.Succeeded && result.Value is not null && !string.IsNullOrWhiteSpace(result.Value.MessageText))
            {
                return SanitizeInterviewerText(session, result.Value.MessageText);
            }
        }
        catch (Exception)
        {
            // validated fallback below
        }

        var role = string.IsNullOrWhiteSpace(session.JobTitle) ? "this role" : session.JobTitle.Trim();
        return $"Thanks for joining. Today we'll talk about {role}. I'll start with a few questions shortly.";
    }

    private async Task<AssessAnswerResponse> AssessSafeAsync(
        InterviewPrepSessionEntity session,
        InterviewPrepTurnEntity question,
        InterviewPrepTurnEntity answer,
        CancellationToken cancellationToken)
    {
        var comparison = contextBuilder.CompareSnapshots(session.CvSnapshotJson, session.JobSnapshotJson);
        var request = new AssessAnswerRequest(
            question.Text,
            answer.Text,
            question.CompetencyTag,
            string.IsNullOrWhiteSpace(comparison.CvPlainText)
                ? null
                : new InterviewPrepAiDocumentSnapshot(session.JobTitle, comparison.CvPlainText),
            string.IsNullOrWhiteSpace(comparison.JobPlainText)
                ? null
                : new InterviewPrepAiDocumentSnapshot(session.JobTitle, comparison.JobPlainText));

        try
        {
            var result = await aiGateway.AssessAnswerAsync(request, cancellationToken);
            if (result.Succeeded && result.Value is not null)
            {
                return result.Value;
            }
        }
        catch (Exception)
        {
            // Never lose the answer — provisional assessment is OK.
        }

        return FakeDeterministicInterviewPrepAiProvider.SafeAssessFallback(request);
    }

    private async Task<SelectNextActionResponse> SelectActionSafeAsync(
        InterviewPrepAiSessionConfig config,
        InterviewPrepStageEntity stage,
        InterviewPrepSessionEntity session,
        IReadOnlyList<InterviewPrepAiTurnSnippet> recent,
        IReadOnlyList<string> covered,
        IReadOnlyList<string> remaining,
        CancellationToken cancellationToken)
    {
        if (InterviewPrepEnumNames.TryParsePersona(config.Persona, out var persona)
            && InterviewPrepBarRaiserSignals.ShouldPrioritizeConsistencyProbe(persona, session))
        {
            var signal = InterviewPrepBarRaiserSignals.DetectContradictions(session)[0];
            return new SelectNextActionResponse(
                "probe",
                signal.CompetencyId,
                "BarRaiser: calmly explore narrative consistency (no trick questions).");
        }

        var stageKey = InterviewPrepEnumNames.TryParseStageStatus(stage.Status, out var status)
            ? InterviewPrepEnumNames.ToWire(status)
            : "coreAssessment";

        try
        {
            var result = await aiGateway.SelectNextActionAsync(
                new SelectNextActionRequest(config, stageKey, recent, covered, remaining),
                cancellationToken);
            if (result.Succeeded && result.Value is not null)
            {
                return result.Value;
            }
        }
        catch (Exception)
        {
        }

        if (remaining.Count > 0)
        {
            return new SelectNextActionResponse("ask_question", remaining[0], "Deterministic remaining competency.");
        }

        return new SelectNextActionResponse("wrap_up", null, "Deterministic wrap-up.");
    }

    private async Task<GenerateInterviewerMessageResponse> GenerateMessageSafeAsync(
        InterviewPrepSessionEntity session,
        InterviewPrepAiSessionConfig config,
        InterviewPrepRuntimeActionType action,
        string? competencyId,
        string? topicHint,
        IReadOnlyList<InterviewPrepAiTurnSnippet> recent,
        IReadOnlyList<string> blocked,
        CancellationToken cancellationToken)
    {
        var actionWire = action switch
        {
            InterviewPrepRuntimeActionType.Probe => "probe",
            InterviewPrepRuntimeActionType.CandidateQuestions => "candidate_questions",
            InterviewPrepRuntimeActionType.Close or InterviewPrepRuntimeActionType.WrapUp => "wrap_up",
            InterviewPrepRuntimeActionType.StageHandoff => "stage_handoff",
            InterviewPrepRuntimeActionType.Opening => "opening",
            InterviewPrepRuntimeActionType.DiscloseFact => "disclose_fact",
            InterviewPrepRuntimeActionType.OfferHint => "offer_hint",
            InterviewPrepRuntimeActionType.IntroduceComplication => "introduce_complication",
            _ => "ask_question"
        };

        var request = new GenerateInterviewerMessageRequest(
            config,
            actionWire,
            competencyId,
            topicHint,
            recent,
            blocked,
            ResolveCandidateDisplayName(session));

        try
        {
            var result = await aiGateway.GenerateInterviewerMessageAsync(request, cancellationToken);
            if (result.Succeeded && result.Value is not null && !string.IsNullOrWhiteSpace(result.Value.MessageText))
            {
                return result.Value;
            }
        }
        catch (Exception)
        {
        }

        return FakeDeterministicInterviewPrepAiProvider.SafeMessageFallback(request);
    }

    private async Task UpdateConversationSummaryAsync(
        InterviewPrepSessionEntity session,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await aiGateway.SummarizeConversationAsync(
                new SummarizeConversationRequest(BuildRecentSnippets(session, take: 12)),
                cancellationToken);
            if (result.Succeeded && result.Value is not null && !string.IsNullOrWhiteSpace(result.Value.Summary))
            {
                session.ConversationSummary = result.Value.Summary;
                return;
            }
        }
        catch (Exception)
        {
        }

        var count = session.Turns.Count;
        session.ConversationSummary = $"Conversation with {count} turn(s).";
    }

    private void ApplyEvidenceAndCoverage(
        InterviewPrepSessionEntity session,
        InterviewPrepTurnEntity candidateTurn,
        string? competencyId,
        AssessAnswerResponse assessment,
        InterviewPrepProgressClass progress,
        DateTimeOffset utcNow)
    {
        var resolvedCompetency = competencyId ?? "communication";
        var classification = progress switch
        {
            InterviewPrepProgressClass.StrongProgress => InterviewEvidenceClassification.Observed,
            InterviewPrepProgressClass.PartialProgress => InterviewEvidenceClassification.Claimed,
            _ => InterviewEvidenceClassification.Absent
        };
        var strength = progress switch
        {
            InterviewPrepProgressClass.StrongProgress => InterviewEvidenceStrength.Strong,
            InterviewPrepProgressClass.PartialProgress => InterviewEvidenceStrength.Moderate,
            _ => InterviewEvidenceStrength.Weak
        };
        var confidence = assessment.Confidence >= 0.75
            ? InterviewEvidenceConfidence.High
            : assessment.Confidence >= 0.4
                ? InterviewEvidenceConfidence.Medium
                : InterviewEvidenceConfidence.Low;

        foreach (var item in assessment.Evidence.Take(5))
        {
            var entity = new InterviewPrepEvidenceItemEntity
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                CandidateTurnId = candidateTurn.Id,
                CompetencyId = resolvedCompetency,
                Classification = InterviewPrepCatalogNames.ToWire(classification),
                Strength = InterviewPrepCatalogNames.ToWire(strength),
                Confidence = InterviewPrepCatalogNames.ToWire(confidence),
                Claim = Truncate(item.Claim, 1024),
                EvidenceQuote = Truncate(item.EvidenceQuote, 4000),
                Polarity = Truncate(item.Polarity, 32),
                CreatedAt = utcNow
            };
            session.EvidenceItems.Add(entity);
            dbContext.InterviewPrepEvidenceItems.Add(entity);
        }

        var coverage = session.CompetencyCoverages
            .FirstOrDefault((entry) =>
                string.Equals(entry.CompetencyId, resolvedCompetency, StringComparison.Ordinal));
        if (coverage is null)
        {
            coverage = new InterviewPrepCompetencyCoverageEntity
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                CompetencyId = resolvedCompetency,
                CoverageState = InterviewPrepCatalogNames.ToWire(InterviewCoverageState.NotStarted),
                UpdatedAt = utcNow
            };
            session.CompetencyCoverages.Add(coverage);
            dbContext.InterviewPrepCompetencyCoverages.Add(coverage);
        }

        coverage.AttemptCount++;
        coverage.EvidenceCount = session.EvidenceItems.Count((item) =>
            string.Equals(item.CompetencyId, resolvedCompetency, StringComparison.Ordinal));
        coverage.LastProgressClass = InterviewPrepRuntimeNames.ToWire(progress);
        coverage.CoverageState = InterviewPrepCatalogNames.ToWire(progress switch
        {
            InterviewPrepProgressClass.StrongProgress => InterviewCoverageState.Covered,
            InterviewPrepProgressClass.PartialProgress => InterviewCoverageState.InProgress,
            _ => InterviewCoverageState.GapsRemain
        });
        coverage.UpdatedAt = utcNow;
    }

    private void EnsureCoverageSeeded(InterviewPrepSessionEntity session, InterviewPlan plan)
    {
        var competencies = plan.Competencies;
        if (competencies is null || competencies.Count == 0)
        {
            return;
        }

        var utcNow = DateTimeOffset.UtcNow;
        foreach (var competency in competencies)
        {
            if (session.CompetencyCoverages.Any((entry) =>
                    string.Equals(entry.CompetencyId, competency.CompetencyId, StringComparison.Ordinal)))
            {
                continue;
            }

            var coverage = new InterviewPrepCompetencyCoverageEntity
            {
                Id = Guid.NewGuid(),
                SessionId = session.Id,
                CompetencyId = competency.CompetencyId,
                CoverageState = competency.InitialCoverageState,
                UpdatedAt = utcNow
            };
            session.CompetencyCoverages.Add(coverage);
            dbContext.InterviewPrepCompetencyCoverages.Add(coverage);
        }
    }

    private InterviewPrepTurnEntity AppendInterviewerTurn(
        InterviewPrepSessionEntity session,
        InterviewPrepStageEntity stage,
        InterviewPlan plan,
        int mainQuestionIndex,
        InterviewPrepRuntimeActionType actionType,
        string text,
        string? competencyTag,
        string? intentId,
        string? targetEvidenceKey,
        DateTimeOffset utcNow,
        string? signature = null)
    {
        var resolvedSignature = signature ?? loopGuard.BuildSignature(text);
        var turn = new InterviewPrepTurnEntity
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            StageId = stage.Id,
            Sequence = NextSequence(session),
            Role = InterviewPrepPersistence.Role(InterviewPrepTurnRole.Interviewer),
            Text = text,
            QuestionSignature = resolvedSignature,
            CompetencyTag = competencyTag,
            IntentId = intentId,
            ActionType = InterviewPrepRuntimeNames.ToWire(actionType),
            TargetEvidenceKey = targetEvidenceKey,
            Language = ResolveTurnLanguageWire(session, plan, mainQuestionIndex, actionType),
            CreatedAt = utcNow
        };
        session.Turns.Add(turn);
        dbContext.InterviewPrepTurns.Add(turn);
        return turn;
    }

    private static string ResolveTurnLanguageWire(
        InterviewPrepSessionEntity session,
        InterviewPlan plan,
        int mainQuestionIndex,
        InterviewPrepRuntimeActionType actionType)
    {
        if (!InterviewPrepEnumNames.TryParseLanguage(session.Language, out var sessionLanguage))
        {
            sessionLanguage = InterviewPrepLanguage.English;
        }

        InterviewPrepEnumNames.TryParseMode(session.Mode, out var mode);
        var index = actionType == InterviewPrepRuntimeActionType.Opening ? 0 : mainQuestionIndex;
        var resolved = InterviewPrepTurnLanguage.ResolveForMainQuestion(
            sessionLanguage,
            plan.LanguageAllocation,
            index);

        if (sessionLanguage == InterviewPrepLanguage.English
            && mode != InterviewPrepMode.LanguagePractice
            && marketIsGeneral(session))
        {
            return InterviewPrepEnumNames.ToWire(InterviewPrepLanguage.English);
        }

        return InterviewPrepEnumNames.ToWire(resolved);

        static bool marketIsGeneral(InterviewPrepSessionEntity s) =>
            InterviewPrepEnumNames.TryParseMarket(s.Market, out var market)
            && market == InterviewPrepMarket.General;
    }

    private static InterviewLoopGuardRuntimeState BuildRuntimeState(
        InterviewPrepSessionEntity session,
        InterviewPrepCombinedRuntimeState runtime,
        string? intentId,
        string? competencyId,
        string? targetEvidenceKey)
    {
        var history = BuildHistory(session);
        var intentRepeat = string.IsNullOrWhiteSpace(intentId)
            ? 0
            : history.Count((item) => string.Equals(item.IntentId, intentId, StringComparison.Ordinal));
        var targetRepeat = string.IsNullOrWhiteSpace(targetEvidenceKey)
            ? 0
            : history.Count((item) =>
                string.Equals(item.TargetEvidenceKey, targetEvidenceKey, StringComparison.Ordinal));

        var consecutiveSame = 0;
        foreach (var item in history.Reverse())
        {
            if (string.IsNullOrWhiteSpace(competencyId)
                || !string.Equals(item.CompetencyTag, competencyId, StringComparison.Ordinal))
            {
                break;
            }

            consecutiveSame++;
        }

        return new InterviewLoopGuardRuntimeState(
            history,
            runtime.MainQuestionCount,
            runtime.ConsecutiveNoProgress,
            runtime.FollowUpsForCurrentIntent,
            intentRepeat,
            targetRepeat,
            consecutiveSame);
    }

    private static IReadOnlyList<InterviewLoopGuardHistoryItem> BuildHistory(InterviewPrepSessionEntity session) =>
        session.Turns
            .Where((turn) =>
                string.Equals(turn.Role, InterviewPrepPersistence.Role(InterviewPrepTurnRole.Interviewer), StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(turn.QuestionSignature))
            .OrderBy((turn) => turn.Sequence)
            .Select((turn) => new InterviewLoopGuardHistoryItem(
                turn.QuestionSignature!,
                turn.Text,
                turn.CompetencyTag,
                turn.IntentId,
                turn.TargetEvidenceKey))
            .ToArray();

    private static IReadOnlyList<InterviewPrepAiTurnSnippet> BuildRecentSnippets(
        InterviewPrepSessionEntity session,
        int take = 8) =>
        session.Turns
            .OrderByDescending((turn) => turn.Sequence)
            .Take(take)
            .Reverse()
            .Select((turn) => new InterviewPrepAiTurnSnippet(turn.Role, Truncate(turn.Text, 400), turn.CompetencyTag))
            .ToArray();

    private static IReadOnlyList<string> GetCoveredCompetencyIds(InterviewPrepSessionEntity session) =>
        session.CompetencyCoverages
            .Where((coverage) =>
                string.Equals(coverage.CoverageState, InterviewPrepCatalogNames.ToWire(InterviewCoverageState.Covered), StringComparison.Ordinal)
                || string.Equals(coverage.CoverageState, InterviewPrepCatalogNames.ToWire(InterviewCoverageState.InProgress), StringComparison.Ordinal))
            .Select((coverage) => coverage.CompetencyId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> GetRemainingCompetencyIds(
        InterviewPrepSessionEntity session,
        InterviewPlan plan)
    {
        var covered = session.CompetencyCoverages
            .Where((coverage) =>
                string.Equals(
                    coverage.CoverageState,
                    InterviewPrepCatalogNames.ToWire(InterviewCoverageState.Covered),
                    StringComparison.Ordinal))
            .Select((coverage) => coverage.CompetencyId)
            .ToHashSet(StringComparer.Ordinal);

        return plan.Competencies
            .Select((competency) => competency.CompetencyId)
            .Where((id) => !covered.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string? ResolveIntentId(
        InterviewPlan plan,
        string? competencyId,
        InterviewPrepRuntimeActionType action)
    {
        if (action == InterviewPrepRuntimeActionType.CandidateQuestions)
        {
            return "candidate-questions";
        }

        if (action == InterviewPrepRuntimeActionType.StageHandoff)
        {
            return "stage-handoff";
        }

        if (action is InterviewPrepRuntimeActionType.Close or InterviewPrepRuntimeActionType.WrapUp)
        {
            return "closing";
        }

        if (string.IsNullOrWhiteSpace(competencyId))
        {
            return plan.QuestionIntents.OrderBy((intent) => intent.SortOrder).FirstOrDefault()?.IntentId;
        }

        return plan.QuestionIntents
            .Where((intent) => string.Equals(intent.CompetencyId, competencyId, StringComparison.Ordinal))
            .OrderBy((intent) => intent.SortOrder)
            .Select((intent) => intent.IntentId)
            .FirstOrDefault()
            ?? $"intent-{competencyId}";
    }

    private static string SanitizeInterviewerText(InterviewPrepSessionEntity session, string text)
    {
        var trimmed = text.Trim();
        var displayName = ResolveCandidateDisplayName(session);
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            trimmed = trimmed.Replace("[Candidate Name]", displayName, StringComparison.OrdinalIgnoreCase);
            trimmed = trimmed.Replace("[candidate name]", displayName, StringComparison.OrdinalIgnoreCase);
        }

        if (!InterviewPrepEnumNames.TryParseExperienceType(session.ExperienceType, out var experience)
            || experience != InterviewPrepExperienceType.RealisticSimulation)
        {
            return trimmed;
        }

        // Do not leak coaching in realistic simulation.
        if (trimmed.Contains("coaching tip", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Tip:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Coach:", StringComparison.OrdinalIgnoreCase))
        {
            return "Could you walk me through a recent example in more detail?";
        }

        return trimmed;
    }

    private InterviewPrepAiSessionConfig BuildConfig(
        InterviewPrepSessionEntity session,
        InterviewPrepStageEntity stage) =>
        fullLoopService.IsFullLoopSession(session)
            ? fullLoopService.BuildStageConfig(session, stage)
            : BuildSessionConfig(session);

    private static string? ResolveCandidateDisplayName(InterviewPrepSessionEntity session) =>
        InterviewPrepCandidateDisplayNameResolver.TryResolveFromCvSnapshotJson(session.CvSnapshotJson);

    private static string? ResolveCompetencyTopicHint(string? competencyId)
    {
        var catalog = new InterviewPrepCompetencyCatalog();
        return catalog.TryGet(competencyId, out var definition)
            ? definition.DisplayName
            : null;
    }

    private static InterviewPrepAiSessionConfig BuildSessionConfig(InterviewPrepSessionEntity session) =>
        new(
            session.Mode,
            session.Persona,
            session.Language,
            session.Market,
            session.ExperienceType,
            session.InteractionType);

    private static InterviewPlan RequirePlan(InterviewPrepSessionEntity session) =>
        TryReadPlan(session)
        ?? throw new InterviewPrepValidationException("Session plan is missing. Prepare the session first.");

    private static InterviewPlan? TryReadPlan(InterviewPrepSessionEntity session)
    {
        if (string.IsNullOrWhiteSpace(session.PlanJson))
        {
            return null;
        }

        return JsonSerializer.Deserialize<InterviewPlan>(session.PlanJson, SerializerOptions);
    }

    private static InterviewPrepCombinedRuntimeState ReadRuntime(InterviewPrepSessionEntity session) =>
        InterviewPrepCombinedRuntimeState.Read(session.RuntimeStateJson);

    private static void WriteRuntime(InterviewPrepSessionEntity session, InterviewPrepCombinedRuntimeState runtime) =>
        InterviewPrepCombinedRuntimeState.Write(session, runtime);

    private bool IsCaseMode(InterviewPrepSessionEntity session, InterviewPrepStageEntity? stage)
    {
        if (fullLoopService.IsFullLoopSession(session) && stage is not null)
        {
            var bundle = InterviewPrepFullLoopSerialization.DeserializeStageBundle(stage.PlanJson);
            return bundle?.Mode == InterviewPrepMode.ProblemSolvingCase;
        }

        return InterviewPrepEnumNames.TryParseMode(session.Mode, out var mode)
            && mode == InterviewPrepMode.ProblemSolvingCase;
    }

    private static int NextSequence(InterviewPrepSessionEntity session) =>
        session.Turns.Count == 0 ? 1 : session.Turns.Max((turn) => turn.Sequence) + 1;

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private InterviewPrepLoopGuardOptions EffectiveLoopGuardOptions(InterviewPrepSessionEntity session)
    {
        var configured = options.Value.LoopGuard;
        if (!fullLoopService.IsFullLoopSession(session))
        {
            return configured;
        }

        return new InterviewPrepLoopGuardOptions
        {
            NearDuplicateThreshold = configured.NearDuplicateThreshold,
            MaxExactRetries = configured.MaxExactRetries,
            MaxSessionTurns = Math.Max(1, configured.MaxSessionTurnsFullLoop),
            MaxConsecutiveSameCompetency = configured.MaxConsecutiveSameCompetency,
            MaxIntentRepeats = configured.MaxIntentRepeats,
            MaxFollowUpsPerIntent = configured.MaxFollowUpsPerIntent,
            MaxNoProgressStreak = configured.MaxNoProgressStreak,
            MaxTargetEvidenceRepeats = configured.MaxTargetEvidenceRepeats,
            MaxWordingRetries = configured.MaxWordingRetries
        };
    }

    private void Trace(Guid sessionId, string line)
    {
        if (debugTraceContext.CurrentSessionId == sessionId)
        {
            debugFileTraceLogger.Log(sessionId, line);
        }
    }
}
