namespace ApplyVault.Api.Services;

public sealed record CvImportSectionInput(string Heading, string NormalizedKey, string Text);

public sealed record CvStructuredImportEntryResult(
    string Title,
    string? Subtitle,
    string? DateRange,
    string Summary,
    IReadOnlyList<string> Bullets,
    string TechStack);

public sealed record CvStructuredImportSectionResult(
    string Heading,
    string SectionType,
    IReadOnlyList<CvStructuredImportEntryResult> Entries);

public sealed record CvStructuredImportResult(
    IReadOnlyList<CvStructuredImportSectionResult> Sections);

/// <summary>
/// Optional Gemini structuring for PDF CV import. Callers (CvStructuredImportService) must gate
/// invocation — heuristic-first; call only when GoogleAi:Enabled, extraction is not Empty, and the
/// import gate fails (or CvImportAi:ForceAi). Not an always-on import path.
/// </summary>
public interface ICvStructuredImportAiClient
{
    Task<CvStructuredImportResult> ParseAsync(
        IReadOnlyList<CvImportSectionInput> sections,
        CancellationToken cancellationToken = default);
}
