namespace ApplyVault.Api.Models;

public sealed record CreateCoverLetterRequest(string? Note);

public sealed record CoverLetterDto(
    Guid ScrapeResultId,
    string Letter,
    DateTimeOffset UpdatedAt);
