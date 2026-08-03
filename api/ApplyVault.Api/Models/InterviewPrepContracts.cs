namespace ApplyVault.Api.Models;

/// <summary>
/// Request for an Interview Prep coach turn (ADR-0012 / ADR-0016).
/// Without <see cref="SessionId"/> the turn is ephemeral; with it, durable.
/// </summary>
public sealed record InterviewPrepTurnRequest(
    string Mode,
    string UserMessage,
    string? LanguageMix = null,
    Guid? ScrapeResultId = null,
    IReadOnlyList<InterviewPrepPriorTurnDto>? PriorTurns = null,
    string? HiringMarket = null,
    Guid? SessionId = null);

public sealed record InterviewPrepPriorTurnDto(
    string Role,
    string Text,
    string? Phase = null);

/// <summary>
/// Interview Prep coach turn response. Durably persisted when request includes sessionId.
/// </summary>
public sealed record InterviewPrepTurnResponseDto(
    string Phase,
    InterviewPrepInferenceDto Inference,
    string CoachMessage,
    InterviewPrepScorecardDto? Scorecard,
    IReadOnlyList<string> FollowUps,
    IReadOnlyList<string> DebriefBullets,
    string? ModelAnswer = null,
    Guid? SessionId = null);

public sealed record InterviewPrepCreateSessionRequest(
    string Mode,
    string? LanguageMix = null,
    string? HiringMarket = null,
    Guid? ScrapeResultId = null);

public sealed record InterviewPrepSessionSummaryDto(
    Guid Id,
    string Mode,
    string LanguageMix,
    string HiringMarket,
    Guid? ScrapeResultId,
    string? JobTitle,
    string? CompanyName,
    string Status,
    string Phase,
    int? LatestOverallScore,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);

public sealed record InterviewPrepSessionListResponseDto(
    IReadOnlyList<InterviewPrepSessionSummaryDto> Items,
    int TotalCount);

public sealed record InterviewPrepSessionDetailDto(
    Guid Id,
    string Mode,
    string LanguageMix,
    string HiringMarket,
    Guid? ScrapeResultId,
    string? JobTitle,
    string? CompanyName,
    string Status,
    string Phase,
    int? LatestOverallScore,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<InterviewPrepSessionMessageDto> Messages);

public sealed record InterviewPrepSessionMessageDto(
    Guid Id,
    int Sequence,
    string Role,
    string Text,
    string Phase,
    InterviewPrepScorecardDto? Scorecard,
    IReadOnlyList<string> FollowUps,
    IReadOnlyList<string> DebriefBullets,
    string? ModelAnswer,
    InterviewPrepInferenceDto? Inference,
    DateTimeOffset CreatedAt);

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

/// <summary>
/// Allowed hiringMarket values (ADR-0013). Orthogonal to languageMix.
/// </summary>
public static class InterviewPrepHiringMarkets
{
    public const string General = "general";
    public const string Dk = "dk";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        General,
        Dk
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

public static class InterviewPrepSessionStatuses
{
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
}
