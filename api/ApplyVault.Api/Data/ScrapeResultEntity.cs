namespace ApplyVault.Api.Data;

public sealed class ScrapeResultEntity
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public DateTimeOffset SavedAt { get; set; }

    public bool IsRejected { get; set; }

    public DateOnly? InterviewDate { get; set; }

    public bool IsDeleted { get; set; }

    public required string Title { get; set; }

    public required string Url { get; set; }

    public required string Text { get; set; }

    public int TextLength { get; set; }

    public required string ExtractedAt { get; set; }

    public required string SourceHostname { get; set; }

    public required string DetectedPageType { get; set; }

    public string? JobTitle { get; set; }

    public string? CompanyName { get; set; }

    public string? Location { get; set; }

    public string? JobDescription { get; set; }

    public string? PositionSummary { get; set; }

    public string? HiringManagerName { get; set; }

    public AppUserEntity? User { get; set; }

    public InterviewEventEntity? InterviewEvent { get; set; }

    public List<ScrapeResultContactEntity> HiringManagerContacts { get; set; } = [];

    public List<CalendarEventLinkEntity> CalendarEventLinks { get; set; } = [];
}

public sealed class ScrapeResultContactEntity
{
    public int Id { get; set; }

    public Guid ScrapeResultId { get; set; }

    public required string Type { get; set; }

    public required string Value { get; set; }

    public string? Label { get; set; }
}

public sealed class AppUserEntity
{
    public Guid Id { get; set; }

    public required string SupabaseUserId { get; set; }

    public string? Email { get; set; }

    public string? DisplayName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public List<ScrapeResultEntity> ScrapeResults { get; set; } = [];

    public List<ConnectedAccountEntity> ConnectedAccounts { get; set; } = [];
}

public sealed class ConnectedAccountEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public required string Provider { get; set; }

    public required string ProviderUserId { get; set; }

    public string? Email { get; set; }

    public string? DisplayName { get; set; }

    public required string AccessToken { get; set; }

    public string? RefreshToken { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public AppUserEntity? User { get; set; }

    public List<CalendarEventLinkEntity> CalendarEventLinks { get; set; } = [];
}

public sealed class InterviewEventEntity
{
    public Guid ScrapeResultId { get; set; }

    public DateTimeOffset StartUtc { get; set; }

    public DateTimeOffset EndUtc { get; set; }

    public required string TimeZone { get; set; }

    public string? Location { get; set; }

    public string? Notes { get; set; }

    public ScrapeResultEntity? ScrapeResult { get; set; }
}

public sealed class CalendarEventLinkEntity
{
    public Guid Id { get; set; }

    public Guid ScrapeResultId { get; set; }

    public Guid ConnectedAccountId { get; set; }

    public required string Provider { get; set; }

    public required string ExternalEventId { get; set; }

    public string? ExternalEventUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ScrapeResultEntity? ScrapeResult { get; set; }

    public ConnectedAccountEntity? ConnectedAccount { get; set; }
}
