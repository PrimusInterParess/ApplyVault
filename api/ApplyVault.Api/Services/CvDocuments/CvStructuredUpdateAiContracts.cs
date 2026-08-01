using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public interface ICvStructuredUpdateAiClient
{
    Task<CvStructuredUpdateAiResult> UpdateAsync(
        CvStructuredDocumentDto current,
        string instructions,
        IReadOnlyList<Guid>? focusSectionIds = null,
        CancellationToken cancellationToken = default);
}

public sealed record CvStructuredUpdateAiResult(
    SaveCvStructuredDocumentRequest Document,
    IReadOnlyList<string> ChangeBullets);

internal sealed record CvStructuredUpdateAiResponse(
    IReadOnlyList<CvStructuredUpdateAiSection> Sections,
    IReadOnlyList<string>? ChangeBullets = null);

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
