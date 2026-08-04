namespace ApplyVault.Api.Data;

public sealed class InterviewPrepSessionEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public required string Mode { get; set; }

    public required string LanguageMix { get; set; }

    public required string HiringMarket { get; set; }

    public required string InterviewerProfile { get; set; }

    public required string AgendaJson { get; set; }

    public required string CurrentAgendaStep { get; set; }

    public string? InterviewerMemoryJson { get; set; }

    public string? LatestInterviewMove { get; set; }

    public Guid? ScrapeResultId { get; set; }

    public string? JobTitle { get; set; }

    public string? CompanyName { get; set; }

    public required string Status { get; set; }

    public required string Phase { get; set; }

    public string? InferenceJson { get; set; }

    public string? LatestScorecardJson { get; set; }

    public int? LatestOverallScore { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public AppUserEntity? User { get; set; }

    public ScrapeResultEntity? ScrapeResult { get; set; }

    public List<InterviewPrepSessionMessageEntity> Messages { get; set; } = [];
}

public sealed class InterviewPrepSessionMessageEntity
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public int Sequence { get; set; }

    public required string Role { get; set; }

    public required string Text { get; set; }

    public required string Phase { get; set; }

    public string? ScorecardJson { get; set; }

    public string? FollowUpsJson { get; set; }

    public string? DebriefBulletsJson { get; set; }

    public string? ModelAnswer { get; set; }

    public string? InferenceJson { get; set; }

    public string? TurnStateJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public InterviewPrepSessionEntity? Session { get; set; }
}
