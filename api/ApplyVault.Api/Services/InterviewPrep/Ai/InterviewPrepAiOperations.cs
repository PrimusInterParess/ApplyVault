namespace ApplyVault.Api.Services.InterviewPrep.Ai;

public enum InterviewPrepAiOperation
{
    CreateInterviewBrief,
    PlanInterview,
    GenerateOpening,
    AssessAnswer,
    SelectNextAction,
    GenerateInterviewerMessage,
    SummarizeConversation,
    EvaluateStage,
    GenerateFeedback,
    CompareAnswerRetry,
    ExtractProfessionalStory,
    PlanFullLoop,
    GeneratePanelDebrief,
    /// <summary>Durable study Interview Prep brief (ADR-0025) — not the session prepare brief.</summary>
    GenerateInterviewPrepStudyBrief,
    /// <summary>Answer review Model answer + delivery tips (ADR-0026) — distinct from session GenerateFeedback.</summary>
    GenerateAnswerReview
}

public static class InterviewPrepAiOperationNames
{
    public static string ToName(InterviewPrepAiOperation operation) => operation.ToString();
}
