using ApplyVault.Api.Infrastructure;
using ApplyVault.Api.Services.InterviewPrep.Ai.Contracts;

namespace ApplyVault.Api.Services.InterviewPrep.Ai;

internal static class InterviewPrepAiGatewayDebugTrace
{
    public static void LogRequestSummary(
        IInterviewPrepDebugFileTraceLogger trace,
        Guid sessionId,
        InterviewPrepAiOperation operation,
        object request)
    {
        switch (request)
        {
            case CreateInterviewBriefRequest create:
                trace.Log(sessionId,
                    $"AI {InterviewPrepDebugTraceLabels.AiOperation(operation)} request config={InterviewPrepDebugTraceLabels.SessionConfigLine(create.Config)} hasCv={create.CvSnapshot is not null} cvLen={create.CvSnapshot?.Text?.Length ?? 0} hasJob={create.JobSnapshot is not null} jobLen={create.JobSnapshot?.Text?.Length ?? 0}");
                return;

            case PlanInterviewRequest plan:
                trace.Log(sessionId,
                    $"AI {InterviewPrepDebugTraceLabels.AiOperation(operation)} request config={InterviewPrepDebugTraceLabels.SessionConfigLine(plan.Config)} themesCount={plan.Brief.Themes.Count} briefSourcePreview='{InterviewPrepDebugTraceLabels.Preview(plan.Brief.Summary, 120)}'");
                return;

            case PlanFullLoopRequest loop:
                trace.Log(sessionId,
                    $"AI {InterviewPrepDebugTraceLabels.AiOperation(operation)} request config={InterviewPrepDebugTraceLabels.SessionConfigLine(loop.Config)} themesCount={loop.Brief.Themes.Count}");
                return;

            case GenerateOpeningRequest opening:
                trace.Log(sessionId,
                    $"AI {InterviewPrepDebugTraceLabels.AiOperation(operation)} request config={InterviewPrepDebugTraceLabels.SessionConfigLine(opening.Config)} candidateDisplayName='{InterviewPrepDebugTraceLabels.Preview(opening.CandidateDisplayName, 60)}' role='{InterviewPrepDebugTraceLabels.Preview(opening.RoleTitle, 80)}' company='{InterviewPrepDebugTraceLabels.Preview(opening.CompanyName, 60)}'");
                return;

            case AssessAnswerRequest assess:
                trace.Log(sessionId,
                    $"AI {InterviewPrepDebugTraceLabels.AiOperation(operation)} competency={InterviewPrepDebugTraceLabels.Competency(assess.CompetencyId)} qLen={assess.QuestionText?.Length ?? 0} aLen={assess.AnswerText?.Length ?? 0}");
                return;

            case SelectNextActionRequest select:
                trace.Log(sessionId,
                    $"AI {InterviewPrepDebugTraceLabels.AiOperation(operation)} stage={InterviewPrepDebugTraceLabels.Preview(select.CurrentStageKey, 40)} remaining={select.RemainingCompetencyIds.Count} covered={select.CoveredCompetencyIds.Count} recentTurns={select.RecentTurns.Count}");
                return;

            case GenerateInterviewerMessageRequest msg:
                trace.Log(sessionId,
                    $"AI {InterviewPrepDebugTraceLabels.AiOperation(operation)} action={msg.ActionType} competency={InterviewPrepDebugTraceLabels.Competency(msg.CompetencyId)} candidateDisplayName='{InterviewPrepDebugTraceLabels.Preview(msg.CandidateDisplayName, 60)}' blockedSignatures={msg.BlockedQuestionSignatures.Count}");
                return;

            case SummarizeConversationRequest summary:
                trace.Log(sessionId,
                    $"AI {InterviewPrepDebugTraceLabels.AiOperation(operation)} turns={summary.Turns.Count}");
                return;

            case EvaluateStageRequest stage:
                trace.Log(sessionId,
                    $"AI {InterviewPrepDebugTraceLabels.AiOperation(operation)} stageKey={InterviewPrepDebugTraceLabels.Preview(stage.StageKey, 40)} turns={stage.StageTurns.Count}");
                return;

            case GenerateFeedbackRequest feedback:
                trace.Log(sessionId,
                    $"AI {InterviewPrepDebugTraceLabels.AiOperation(operation)} gaps={feedback.Gaps.Count}");
                return;

            default:
                trace.Log(sessionId, $"AI {InterviewPrepDebugTraceLabels.AiOperation(operation)} requestType={request.GetType().Name}");
                return;
        }
    }

    public static void LogResponseOutput(
        IInterviewPrepDebugFileTraceLogger trace,
        Guid sessionId,
        InterviewPrepAiOperation operation,
        object parsed,
        bool usedFallback,
        int attempt)
    {
        var op = InterviewPrepDebugTraceLabels.AiOperation(operation);
        var fallbackTag = usedFallback ? " usedFallback=true" : string.Empty;

        switch (parsed)
        {
            case CreateInterviewBriefResponse brief:
                trace.Log(sessionId,
                    $"AI Output {op}{fallbackTag} attempt={attempt} summaryPreview='{InterviewPrepDebugTraceLabels.Preview(brief.Summary, 220)}' themesCount={brief.Themes.Count}");
                return;

            case PlanInterviewResponse plan:
                trace.Log(sessionId,
                    $"AI Output {op}{fallbackTag} attempt={attempt} planSummaryPreview='{InterviewPrepDebugTraceLabels.Preview(plan.PlanSummary, 200)}' stagesCount={plan.Stages.Count}");
                return;

            case PlanFullLoopResponse loop:
                trace.Log(sessionId,
                    $"AI Output {op}{fallbackTag} attempt={attempt} loopSummaryPreview='{InterviewPrepDebugTraceLabels.Preview(loop.LoopSummary, 200)}' stagesCount={loop.Stages.Count} coverageGoals={loop.CoverageGoals.Count}");
                return;

            case GenerateOpeningResponse opening:
                trace.Log(sessionId,
                    $"AI Output {op}{fallbackTag} attempt={attempt} tone={opening.Tone} textPreview='{InterviewPrepDebugTraceLabels.Preview(opening.MessageText, 240)}'");
                return;

            case SelectNextActionResponse select:
                trace.Log(sessionId,
                    $"AI Output {op}{fallbackTag} attempt={attempt} actionType={select.ActionType} competency={InterviewPrepDebugTraceLabels.Competency(select.CompetencyId)} rationalePreview='{InterviewPrepDebugTraceLabels.Preview(select.Rationale, 160)}'");
                return;

            case GenerateInterviewerMessageResponse msg:
                trace.Log(sessionId,
                    $"AI Output {op}{fallbackTag} attempt={attempt} intent={msg.Intent} competency={InterviewPrepDebugTraceLabels.Competency(msg.CompetencyId)} textPreview='{InterviewPrepDebugTraceLabels.Preview(msg.MessageText, 260)}'");
                return;

            case AssessAnswerResponse assess:
                trace.Log(sessionId,
                    $"AI Output {op}{fallbackTag} attempt={attempt} score={assess.Score} confidence={assess.Confidence:0.###} summaryPreview='{InterviewPrepDebugTraceLabels.Preview(assess.Summary, 160)}'");
                return;

            case SummarizeConversationResponse summary:
                trace.Log(sessionId,
                    $"AI Output {op}{fallbackTag} attempt={attempt} summaryPreview='{InterviewPrepDebugTraceLabels.Preview(summary.Summary, 200)}'");
                return;

            case EvaluateStageResponse stage:
                trace.Log(sessionId,
                    $"AI Output {op}{fallbackTag} attempt={attempt} score={stage.Score} summaryPreview='{InterviewPrepDebugTraceLabels.Preview(stage.Summary, 160)}'");
                return;

            case GenerateFeedbackResponse feedback:
                trace.Log(sessionId,
                    $"AI Output {op}{fallbackTag} attempt={attempt} feedbackPreview='{InterviewPrepDebugTraceLabels.Preview(feedback.OverallFeedback, 200)}'");
                return;

            default:
                trace.Log(sessionId, $"AI Output {op}{fallbackTag} attempt={attempt} type={parsed.GetType().Name}");
                return;
        }
    }
}
