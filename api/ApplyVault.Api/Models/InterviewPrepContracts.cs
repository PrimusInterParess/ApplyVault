namespace ApplyVault.Api.Models;

/// <summary>
/// Request for an ephemeral Interview Prep coach turn (ADR-0012). Not persisted.
/// </summary>
public sealed record InterviewPrepTurnRequest(
    string Mode,
    string UserMessage,
    string? LanguageMix = null,
    Guid? ScrapeResultId = null,
    IReadOnlyList<InterviewPrepPriorTurnDto>? PriorTurns = null);

public sealed record InterviewPrepPriorTurnDto(
    string Role,
    string Text,
    string? Phase = null);

/// <summary>
/// Ephemeral Interview Prep coach turn response. Not persisted.
/// </summary>
public sealed record InterviewPrepTurnResponseDto(
    string Phase,
    InterviewPrepInferenceDto Inference,
    string CoachMessage,
    InterviewPrepScorecardDto? Scorecard,
    IReadOnlyList<string> FollowUps,
    IReadOnlyList<string> DebriefBullets);

public sealed record InterviewPrepInferenceDto(
    string Role,
    string Seniority,
    string InterviewStyle,
    bool IsTechnicalContext);

public sealed record InterviewPrepScorecardDto(
    int Overall,
    string? Summary,
    IReadOnlyList<InterviewPrepScorecardDimensionDto> Dimensions);

public sealed record InterviewPrepScorecardDimensionDto(
    string Id,
    int Score,
    string Note);

/// <summary>
/// Allowed Interview Prep mode ids (case-sensitive snake_case).
/// </summary>
public static class InterviewPrepModes
{
    public const string Screening = "screening";
    public const string Behavioral = "behavioral";
    public const string RoleDomain = "role_domain";
    public const string ProblemSolving = "problem_solving";
    public const string ProcessSystems = "process_systems";
    public const string LanguagePractice = "language_practice";
    public const string FullLoop = "full_loop";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Screening,
        Behavioral,
        RoleDomain,
        ProblemSolving,
        ProcessSystems,
        LanguagePractice,
        FullLoop
    };
}

/// <summary>
/// Allowed languageMix values. Frozen: use <c>mixed</c>, not <c>en+da</c>.
/// </summary>
public static class InterviewPrepLanguageMixes
{
    public const string En = "en";
    public const string Da = "da";
    public const string Mixed = "mixed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        En,
        Da,
        Mixed
    };
}

public static class InterviewPrepPhases
{
    public const string Interview = "interview";
    public const string Debrief = "debrief";
}

public static class InterviewPrepTurnRoles
{
    public const string User = "user";
    public const string Coach = "coach";
}
