namespace ApplyVault.Api.Data;

public sealed class ScrapeResultEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset SavedAt { get; set; }

    public bool IsRejected { get; set; }

    public string? LastStatusSource { get; set; }

    public string? LastStatusKind { get; set; }

    public DateTimeOffset? LastStatusUpdatedAt { get; set; }

    public DateTimeOffset? LastStatusEmailReceivedAt { get; set; }

    public string? LastStatusEmailFrom { get; set; }

    public string? LastStatusEmailSubject { get; set; }

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

    public string? JobTitleOverride { get; set; }

    public double JobTitleConfidence { get; set; }

    public string? JobTitleReviewReason { get; set; }

    public string? CompanyName { get; set; }

    public string? CompanyNameOverride { get; set; }

    public double CompanyNameConfidence { get; set; }

    public string? CompanyNameReviewReason { get; set; }

    public string? Location { get; set; }

    public string? LocationOverride { get; set; }

    public double LocationConfidence { get; set; }

    public string? LocationReviewReason { get; set; }

    public string? JobDescription { get; set; }

    public string? JobDescriptionOverride { get; set; }

    public double JobDescriptionConfidence { get; set; }

    public string? JobDescriptionReviewReason { get; set; }

    public string? PositionSummary { get; set; }

    public string? HiringManagerName { get; set; }

    public double CaptureOverallConfidence { get; set; }

    public required string CaptureReviewStatus { get; set; }

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

    public List<UserCvProjectSummaryEntity> CvProjectSummaries { get; set; } = [];

    public UserCvDocumentEntity? CvDocument { get; set; }
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

    public string? SyncStatus { get; set; }

    public DateTimeOffset? LastSyncedAt { get; set; }

    public string? LastSyncError { get; set; }

    public string? LastHistoryId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public AppUserEntity? User { get; set; }

    public List<CalendarEventLinkEntity> CalendarEventLinks { get; set; } = [];
}

public sealed class UserCvProjectSummaryEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public long ExternalRepoId { get; set; }

    public required string FullName { get; set; }

    public required string HtmlUrl { get; set; }

    public string? PrimaryLanguage { get; set; }

    public string? Topics { get; set; }

    public required string CvTitle { get; set; }

    public required string CvSummary { get; set; }

    public required string CvBullets { get; set; }

    public required string TechStack { get; set; }

    public DateTimeOffset GeneratedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public AppUserEntity? User { get; set; }
}

public sealed class UserCvDocumentEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public required string OriginalFileName { get; set; }

    public required string ContentType { get; set; }

    public required string StorageKey { get; set; }

    public string? BaseStorageKey { get; set; }

    public long FileSizeBytes { get; set; }

    public long OriginalFileSizeBytes { get; set; }

    public DateTimeOffset UploadedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? StructuredImportedAt { get; set; }

    public string? ProfilePhotoStorageKey { get; set; }

    public string? ProfilePhotoContentType { get; set; }

    public AppUserEntity? User { get; set; }

    public List<UserCvSectionEntity> Sections { get; set; } = [];
}

public sealed class UserCvSectionEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid UserCvDocumentId { get; set; }

    public required string Heading { get; set; }

    public required string SectionType { get; set; }

    public int SortOrder { get; set; }

    public UserCvDocumentEntity? Document { get; set; }

    public List<UserCvEntryEntity> Entries { get; set; } = [];
}

public sealed class UserCvEntryEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid SectionId { get; set; }

    public required string Title { get; set; }

    public string? Subtitle { get; set; }

    public string? DateRange { get; set; }

    public required string Summary { get; set; }

    public required string BulletsJson { get; set; }

    public required string TechStack { get; set; }

    public required string Source { get; set; }

    public Guid? SourceSummaryId { get; set; }

    public int SortOrder { get; set; }

    public UserCvSectionEntity? Section { get; set; }

    public UserCvProjectSummaryEntity? SourceSummary { get; set; }
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
