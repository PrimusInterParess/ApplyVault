using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public interface ICvStructuredUpdateAiClient
{
    Task<SaveCvStructuredDocumentRequest> UpdateAsync(
        CvStructuredDocumentDto current,
        string instructions,
        IReadOnlyList<Guid>? focusSectionIds = null,
        CancellationToken cancellationToken = default);
}

internal sealed record CvStructuredUpdateAiResponse(
    IReadOnlyList<CvStructuredUpdateAiSection> Sections);

internal sealed record CvStructuredUpdateAiSection(
    string? Id,
    string Heading,
    string SectionType,
    int SortOrder,
    IReadOnlyList<CvStructuredUpdateAiEntry> Entries);

internal sealed record CvStructuredUpdateAiEntry(
    string? Id,
    string Title,
    string? Subtitle,
    string? DateRange,
    string Summary,
    IReadOnlyList<string> Bullets,
    string TechStack,
    string? Source,
    string? SourceSummaryId,
    int SortOrder);
