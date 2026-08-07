namespace ApplyVault.Api.Services.InterviewPrep.Coaching;

/// <summary>Private persisted coaching feedback (maps to public DTO only).</summary>
internal sealed record PersistedCoachingFeedback(
    string OverallFeedback,
    IReadOnlyList<string> CoachingTips,
    IReadOnlyList<string> PracticeSuggestions,
    string AnswerSummary,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Gaps,
    string ModelAnswer);

public static class InterviewPrepAnswerRetryStatuses
{
    public const string Reviewed = "reviewed";
    public const string Compared = "compared";
}
