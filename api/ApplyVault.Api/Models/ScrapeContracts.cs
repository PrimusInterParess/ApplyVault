namespace ApplyVault.Api.Models;

public sealed record HiringManagerContactDto(
    string Type,
    string Value,
    string? Label
);

public sealed record JobDetailsDto(
    string SourceHostname,
    string DetectedPageType,
    string? JobTitle,
    string? CompanyName,
    string? Location,
    string? JobDescription,
    string? PositionSummary,
    string? HiringManagerName,
    IReadOnlyList<HiringManagerContactDto> HiringManagerContacts
);

public sealed record ScrapeResultDto(
    string Title,
    string Url,
    string Text,
    int TextLength,
    string ExtractedAt,
    JobDetailsDto JobDetails
);

public sealed record SavedScrapeResult(
    Guid Id,
    DateTimeOffset SavedAt,
    bool IsRejected,
    DateOnly? InterviewDate,
    InterviewEventDto? InterviewEvent,
    IReadOnlyList<CalendarEventLinkDto> CalendarEvents,
    ScrapeResultDto Payload
);

public sealed record SaveScrapeResultResponse(
    Guid Id,
    DateTimeOffset SavedAt
);

public sealed record UpdateScrapeResultRejectionRequest(
    bool IsRejected
);

public sealed record UpdateScrapeResultDescriptionRequest(
    string Description
);

public sealed record UpdateScrapeResultInterviewDateRequest(
    DateOnly? InterviewDate
);

public sealed record InterviewEventDto(
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string TimeZone,
    string? Location,
    string? Notes
);

public sealed record UpdateInterviewEventRequest(
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string TimeZone,
    string? Location,
    string? Notes
);

public sealed record CalendarEventLinkDto(
    Guid Id,
    Guid ConnectedAccountId,
    string Provider,
    string ExternalEventId,
    string? ExternalEventUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record ConnectedCalendarAccountDto(
    Guid Id,
    string Provider,
    string ProviderUserId,
    string? Email,
    string? DisplayName,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public sealed record CurrentUserDto(
    Guid Id,
    string SupabaseUserId,
    string? Email,
    string? DisplayName
);

public sealed record CalendarAuthorizationStartRequest(
    string? ReturnUrl
);

public sealed record CalendarAuthorizationStartResponse(
    string AuthorizationUrl
);

public sealed record CreateCalendarEventRequest(
    Guid ConnectedAccountId
);
