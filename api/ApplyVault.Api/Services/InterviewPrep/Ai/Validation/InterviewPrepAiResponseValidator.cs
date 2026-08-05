using ApplyVault.Api.Services.InterviewPrep.Ai.Contracts;

namespace ApplyVault.Api.Services.InterviewPrep.Ai.Validation;

public sealed record InterviewPrepAiValidationResult(bool IsValid, string? ErrorMessage)
{
    public static InterviewPrepAiValidationResult Ok() => new(true, null);

    public static InterviewPrepAiValidationResult Fail(string message) => new(false, message);
}

public static class InterviewPrepAiResponseValidator
{
    public static InterviewPrepAiValidationResult Validate(InterviewPrepAiOperation operation, object? response) =>
        operation switch
        {
            InterviewPrepAiOperation.CreateInterviewBrief => ValidateBrief(response as CreateInterviewBriefResponse),
            InterviewPrepAiOperation.PlanInterview => ValidatePlan(response as PlanInterviewResponse),
            InterviewPrepAiOperation.GenerateOpening => ValidateOpening(response as GenerateOpeningResponse),
            InterviewPrepAiOperation.AssessAnswer => ValidateAssess(response as AssessAnswerResponse),
            InterviewPrepAiOperation.SelectNextAction => ValidateSelect(response as SelectNextActionResponse),
            InterviewPrepAiOperation.GenerateInterviewerMessage => ValidateMessage(response as GenerateInterviewerMessageResponse),
            InterviewPrepAiOperation.SummarizeConversation => ValidateSummary(response as SummarizeConversationResponse),
            InterviewPrepAiOperation.EvaluateStage => ValidateStage(response as EvaluateStageResponse),
            InterviewPrepAiOperation.GenerateFeedback => ValidateFeedback(response as GenerateFeedbackResponse),
            InterviewPrepAiOperation.CompareAnswerRetry => ValidateCompare(response as CompareAnswerRetryResponse),
            InterviewPrepAiOperation.ExtractProfessionalStory => ValidateStories(response as ExtractProfessionalStoryResponse),
            InterviewPrepAiOperation.PlanFullLoop => ValidateFullLoop(response as PlanFullLoopResponse),
            InterviewPrepAiOperation.GeneratePanelDebrief => ValidateDebrief(response as GeneratePanelDebriefResponse),
            _ => InterviewPrepAiValidationResult.Fail($"Unknown operation {operation}.")
        };

    private static InterviewPrepAiValidationResult ValidateBrief(CreateInterviewBriefResponse? r) =>
        r is null || string.IsNullOrWhiteSpace(r.Summary)
            ? InterviewPrepAiValidationResult.Fail("Brief requires summary.")
            : InterviewPrepAiValidationResult.Ok();

    private static InterviewPrepAiValidationResult ValidatePlan(PlanInterviewResponse? r) =>
        r is null || string.IsNullOrWhiteSpace(r.PlanSummary) || r.Stages is null || r.Stages.Count == 0
            ? InterviewPrepAiValidationResult.Fail("Plan requires summary and at least one stage.")
            : r.Stages.Any(s => string.IsNullOrWhiteSpace(s.StageKey) || string.IsNullOrWhiteSpace(s.Goal))
                ? InterviewPrepAiValidationResult.Fail("Each stage requires stageKey and goal.")
                : InterviewPrepAiValidationResult.Ok();

    private static InterviewPrepAiValidationResult ValidateOpening(GenerateOpeningResponse? r) =>
        r is null || string.IsNullOrWhiteSpace(r.MessageText)
            ? InterviewPrepAiValidationResult.Fail("Opening requires messageText.")
            : InterviewPrepAiValidationResult.Ok();

    private static InterviewPrepAiValidationResult ValidateAssess(AssessAnswerResponse? r)
    {
        if (r is null || string.IsNullOrWhiteSpace(r.Summary))
        {
            return InterviewPrepAiValidationResult.Fail("Assessment requires summary.");
        }

        if (r.Score is < 0 or > 100)
        {
            return InterviewPrepAiValidationResult.Fail("Assessment score must be 0-100.");
        }

        if (r.Confidence is < 0 or > 1)
        {
            return InterviewPrepAiValidationResult.Fail("Assessment confidence must be 0-1.");
        }

        if (r.Evidence is null)
        {
            return InterviewPrepAiValidationResult.Fail("Assessment evidence list is required.");
        }

        foreach (var item in r.Evidence)
        {
            if (string.IsNullOrWhiteSpace(item.Claim) || string.IsNullOrWhiteSpace(item.EvidenceQuote))
            {
                return InterviewPrepAiValidationResult.Fail("Evidence items require claim and evidenceQuote.");
            }

            if (!IsPolarity(item.Polarity))
            {
                return InterviewPrepAiValidationResult.Fail("Evidence polarity must be positive|negative|neutral.");
            }
        }

        return InterviewPrepAiValidationResult.Ok();
    }

    private static InterviewPrepAiValidationResult ValidateSelect(SelectNextActionResponse? r) =>
        r is null || string.IsNullOrWhiteSpace(r.ActionType) || string.IsNullOrWhiteSpace(r.Rationale)
            ? InterviewPrepAiValidationResult.Fail("SelectNextAction requires actionType and rationale.")
            : InterviewPrepAiValidationResult.Ok();

    private static InterviewPrepAiValidationResult ValidateMessage(GenerateInterviewerMessageResponse? r) =>
        r is null || string.IsNullOrWhiteSpace(r.MessageText) || string.IsNullOrWhiteSpace(r.Intent)
            ? InterviewPrepAiValidationResult.Fail("Interviewer message requires messageText and intent.")
            : InterviewPrepAiValidationResult.Ok();

    private static InterviewPrepAiValidationResult ValidateSummary(SummarizeConversationResponse? r) =>
        r is null || string.IsNullOrWhiteSpace(r.Summary)
            ? InterviewPrepAiValidationResult.Fail("Conversation summary is required.")
            : InterviewPrepAiValidationResult.Ok();

    private static InterviewPrepAiValidationResult ValidateStage(EvaluateStageResponse? r) =>
        r is null || string.IsNullOrWhiteSpace(r.Summary) || r.Score is < 0 or > 100
            ? InterviewPrepAiValidationResult.Fail("Stage evaluation requires summary and score 0-100.")
            : InterviewPrepAiValidationResult.Ok();

    private static InterviewPrepAiValidationResult ValidateFeedback(GenerateFeedbackResponse? r) =>
        r is null || string.IsNullOrWhiteSpace(r.OverallFeedback)
            ? InterviewPrepAiValidationResult.Fail("Feedback requires overallFeedback.")
            : InterviewPrepAiValidationResult.Ok();

    private static InterviewPrepAiValidationResult ValidateCompare(CompareAnswerRetryResponse? r) =>
        r is null || string.IsNullOrWhiteSpace(r.ComparisonSummary)
            ? InterviewPrepAiValidationResult.Fail("Compare requires comparisonSummary.")
            : InterviewPrepAiValidationResult.Ok();

    private static InterviewPrepAiValidationResult ValidateStories(ExtractProfessionalStoryResponse? r)
    {
        if (r?.Stories is null)
        {
            return InterviewPrepAiValidationResult.Fail("Stories list is required.");
        }

        foreach (var story in r.Stories)
        {
            if (string.IsNullOrWhiteSpace(story.Title) || string.IsNullOrWhiteSpace(story.Narrative))
            {
                return InterviewPrepAiValidationResult.Fail("Each story requires title and narrative.");
            }
        }

        return InterviewPrepAiValidationResult.Ok();
    }

    private static InterviewPrepAiValidationResult ValidateFullLoop(PlanFullLoopResponse? r) =>
        r is null || string.IsNullOrWhiteSpace(r.LoopSummary) || r.Stages is null || r.Stages.Count == 0
            ? InterviewPrepAiValidationResult.Fail("Full loop requires summary and stages.")
            : InterviewPrepAiValidationResult.Ok();

    private static InterviewPrepAiValidationResult ValidateDebrief(GeneratePanelDebriefResponse? r)
    {
        if (r is null || string.IsNullOrWhiteSpace(r.OverallDebrief) || r.Perspectives is null || r.Perspectives.Count == 0)
        {
            return InterviewPrepAiValidationResult.Fail("Panel debrief requires overallDebrief and perspectives.");
        }

        foreach (var p in r.Perspectives)
        {
            if (string.IsNullOrWhiteSpace(p.PersonaLabel) || string.IsNullOrWhiteSpace(p.Assessment) || p.Score is < 0 or > 100)
            {
                return InterviewPrepAiValidationResult.Fail("Each perspective requires personaLabel, assessment, score 0-100.");
            }
        }

        return InterviewPrepAiValidationResult.Ok();
    }

    private static bool IsPolarity(string? value) =>
        string.Equals(value, "positive", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "negative", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "neutral", StringComparison.OrdinalIgnoreCase);
}
