namespace ApplyVault.Api.Services.InterviewPrep.Domain;

public static class InterviewPrepSessionStateMachine
{
    private static readonly HashSet<(InterviewPrepSessionStatus From, InterviewPrepSessionStatus To)> Allowed =
    [
        (InterviewPrepSessionStatus.Created, InterviewPrepSessionStatus.Preparing),
        (InterviewPrepSessionStatus.Preparing, InterviewPrepSessionStatus.Ready),
        (InterviewPrepSessionStatus.Preparing, InterviewPrepSessionStatus.Failed),
        (InterviewPrepSessionStatus.Ready, InterviewPrepSessionStatus.InProgress),
        (InterviewPrepSessionStatus.InProgress, InterviewPrepSessionStatus.Paused),
        (InterviewPrepSessionStatus.Paused, InterviewPrepSessionStatus.InProgress),
        (InterviewPrepSessionStatus.InProgress, InterviewPrepSessionStatus.Completing),
        (InterviewPrepSessionStatus.Completing, InterviewPrepSessionStatus.Completed),
        (InterviewPrepSessionStatus.InProgress, InterviewPrepSessionStatus.Failed),
        (InterviewPrepSessionStatus.Preparing, InterviewPrepSessionStatus.Cancelled),
        (InterviewPrepSessionStatus.Created, InterviewPrepSessionStatus.Cancelled),
        (InterviewPrepSessionStatus.Ready, InterviewPrepSessionStatus.Cancelled),
        (InterviewPrepSessionStatus.InProgress, InterviewPrepSessionStatus.Cancelled),
        (InterviewPrepSessionStatus.Paused, InterviewPrepSessionStatus.Cancelled),
        (InterviewPrepSessionStatus.Completing, InterviewPrepSessionStatus.Cancelled)
    ];

    public static bool CanTransition(InterviewPrepSessionStatus from, InterviewPrepSessionStatus to) =>
        Allowed.Contains((from, to));

    public static void EnsureTransition(InterviewPrepSessionStatus from, InterviewPrepSessionStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InterviewPrepConflictException(
                $"Illegal session transition from {InterviewPrepEnumNames.ToWire(from)} to {InterviewPrepEnumNames.ToWire(to)}.");
        }
    }
}

public static class InterviewPrepStageStateMachine
{
    private static readonly HashSet<(InterviewPrepStageStatus From, InterviewPrepStageStatus To)> Allowed =
    [
        (InterviewPrepStageStatus.Planned, InterviewPrepStageStatus.Opening),
        (InterviewPrepStageStatus.Opening, InterviewPrepStageStatus.WarmUp),
        (InterviewPrepStageStatus.Opening, InterviewPrepStageStatus.CoreAssessment),
        (InterviewPrepStageStatus.WarmUp, InterviewPrepStageStatus.CoreAssessment),
        (InterviewPrepStageStatus.CoreAssessment, InterviewPrepStageStatus.CandidateQuestions),
        (InterviewPrepStageStatus.CoreAssessment, InterviewPrepStageStatus.Closing),
        // Mid-loop Full-loop handoff skips CandidateQuestions/Close (ADR-0024).
        (InterviewPrepStageStatus.CoreAssessment, InterviewPrepStageStatus.AssessmentPending),
        (InterviewPrepStageStatus.CandidateQuestions, InterviewPrepStageStatus.Closing),
        (InterviewPrepStageStatus.Closing, InterviewPrepStageStatus.AssessmentPending),
        (InterviewPrepStageStatus.AssessmentPending, InterviewPrepStageStatus.Assessed),
        (InterviewPrepStageStatus.Assessed, InterviewPrepStageStatus.Completed),
        (InterviewPrepStageStatus.Closing, InterviewPrepStageStatus.Completed),
        (InterviewPrepStageStatus.AssessmentPending, InterviewPrepStageStatus.Completed)
    ];

    public static bool CanTransition(InterviewPrepStageStatus from, InterviewPrepStageStatus to) =>
        Allowed.Contains((from, to));

    public static void EnsureTransition(InterviewPrepStageStatus from, InterviewPrepStageStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InterviewPrepConflictException(
                $"Illegal stage transition from {InterviewPrepEnumNames.ToWire(from)} to {InterviewPrepEnumNames.ToWire(to)}.");
        }
    }
}

public sealed class InterviewPrepConflictException(string message) : Exception(message)
{
    public string ErrorCode { get; init; } = "interview_prep_conflict";

    public Guid? ExistingBriefId { get; init; }
}

public sealed class InterviewPrepValidationException(string message) : Exception(message)
{
    public string? ErrorCode { get; init; }
}

public sealed class InterviewPrepNotFoundException() : Exception("Interview prep session was not found.");

public sealed class InterviewPrepAiUnavailableException(string message) : Exception(message)
{
    public string ErrorCode { get; init; } = "interview_prep_brief_ai_unavailable";
}
