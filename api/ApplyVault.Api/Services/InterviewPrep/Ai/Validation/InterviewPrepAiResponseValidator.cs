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
            InterviewPrepAiOperation.GenerateInterviewPrepStudyBrief =>
                ValidateStudyBrief(response as GenerateInterviewPrepStudyBriefResponse),
            InterviewPrepAiOperation.GenerateAnswerReview =>
                ValidateAnswerReview(response as GenerateAnswerReviewResponse),
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

    private static InterviewPrepAiValidationResult ValidateOpening(GenerateOpeningResponse? r)
    {
        if (r is null || string.IsNullOrWhiteSpace(r.MessageText))
        {
            return InterviewPrepAiValidationResult.Fail("Opening requires messageText.");
        }

        // Opening is rapport only; the first ask_question turn follows separately.
        if (r.MessageText.Contains('?', StringComparison.Ordinal))
        {
            return InterviewPrepAiValidationResult.Fail("Opening must not ask a question (no '?').");
        }

        return InterviewPrepAiValidationResult.Ok();
    }

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

    private static InterviewPrepAiValidationResult ValidateAnswerReview(GenerateAnswerReviewResponse? r)
    {
        if (r is null || string.IsNullOrWhiteSpace(r.ModelAnswer))
        {
            return InterviewPrepAiValidationResult.Fail("Answer review requires non-empty modelAnswer.");
        }

        if (r.CoachingTips is null)
        {
            return InterviewPrepAiValidationResult.Fail("Answer review requires coachingTips (array; may be empty).");
        }

        if (r.PracticeSuggestions is null)
        {
            return InterviewPrepAiValidationResult.Fail(
                "Answer review requires practiceSuggestions (array; may be empty).");
        }

        return InterviewPrepAiValidationResult.Ok();
    }

    private static InterviewPrepAiValidationResult ValidateStudyBrief(GenerateInterviewPrepStudyBriefResponse? r)
    {
        if (r is null || r.Topics is null)
        {
            return InterviewPrepAiValidationResult.Fail("Study brief requires topics.");
        }

        if (r.Topics.Count == 0)
        {
            return InterviewPrepAiValidationResult.Fail("Study brief requires at least one topic.");
        }

        var seenPriorities = new HashSet<int>();
        foreach (var topic in r.Topics)
        {
            if (string.IsNullOrWhiteSpace(topic.Name))
            {
                return InterviewPrepAiValidationResult.Fail("Each topic requires name.");
            }

            if (!IsBriefTopicGap(topic.Gap))
            {
                return InterviewPrepAiValidationResult.Fail(
                    "Topic gap must be alreadyStrong|mustStudy|niceToHave|unclear.");
            }

            if (topic.Priority < 1)
            {
                return InterviewPrepAiValidationResult.Fail("Topic priority must be >= 1.");
            }

            if (!seenPriorities.Add(topic.Priority))
            {
                return InterviewPrepAiValidationResult.Fail("Topic priorities must be unique within the brief.");
            }

            if (topic.CoverageItems is null || topic.CoverageItems.Count == 0)
            {
                return InterviewPrepAiValidationResult.Fail(
                    "Each topic requires at least one coverageItem (syllabus leaf).");
            }

            if (topic.SampleQuestions is null)
            {
                return InterviewPrepAiValidationResult.Fail(
                    "Each topic requires sampleQuestions (array; may be empty).");
            }

            if (topic.TalkingPoints is null)
            {
                return InterviewPrepAiValidationResult.Fail(
                    "Each topic requires talkingPoints (array; may be empty).");
            }

            foreach (var item in topic.CoverageItems)
            {
                if (string.IsNullOrWhiteSpace(item.Text))
                {
                    return InterviewPrepAiValidationResult.Fail("Each coverageItem requires text.");
                }
            }

            foreach (var item in topic.SampleQuestions)
            {
                if (string.IsNullOrWhiteSpace(item.Text))
                {
                    return InterviewPrepAiValidationResult.Fail("Each sample question requires text.");
                }
            }

            foreach (var item in topic.TalkingPoints)
            {
                if (string.IsNullOrWhiteSpace(item.Text))
                {
                    return InterviewPrepAiValidationResult.Fail("Each talking point requires text.");
                }
            }
        }

        return InterviewPrepAiValidationResult.Ok();
    }

    private static bool IsBriefTopicGap(string? value) =>
        string.Equals(value, "alreadyStrong", StringComparison.Ordinal)
        || string.Equals(value, "mustStudy", StringComparison.Ordinal)
        || string.Equals(value, "niceToHave", StringComparison.Ordinal)
        || string.Equals(value, "unclear", StringComparison.Ordinal);

    private static bool IsPolarity(string? value) =>
        string.Equals(value, "positive", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "negative", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "neutral", StringComparison.OrdinalIgnoreCase);
}
