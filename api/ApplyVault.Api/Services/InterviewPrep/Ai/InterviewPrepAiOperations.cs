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
    GeneratePanelDebrief
}

public static class InterviewPrepAiOperationNames
{
    public static string ToName(InterviewPrepAiOperation operation) => operation.ToString();
}
