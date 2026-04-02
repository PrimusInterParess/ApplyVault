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
