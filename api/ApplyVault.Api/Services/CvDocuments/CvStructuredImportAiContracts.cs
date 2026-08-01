namespace ApplyVault.Api.Services;

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
/// Gemini structuring for PDF CV import (AI-first when GoogleAi:Enabled).
/// Call with full ordered extracted text (join extract lines with \n).
/// Heuristic remains a thin fallback when AI is off or fails (backend orchestration).
/// </summary>
public interface ICvStructuredImportAiClient
{
    /// <summary>
    /// Structure a CV from full ordered extracted text (join extract lines with \n).
    /// </summary>
    Task<CvStructuredImportResult> ParseAsync(
        string extractedFullText,
        CancellationToken cancellationToken = default);
}
