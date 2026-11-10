using ApplyVault.Api.Models;
using ApplyVault.Api.Services.CvSectionCatalog;

namespace ApplyVault.Api.Services;

/// <summary>
/// Extract → AI parser (strict slots) or contact-only fallback → FixedSectionLayout + Unplaced.
/// When PdfPig finds no text and Gemini is on, the original PDF is sent to the model (ADR-0032).
/// </summary>
internal static class CvPdfImportPipeline
{
    public const string ImportedWithAiNotice = "Imported with AI.";
    public const string ContactOnlyNotice =
        "AI unavailable — contact placed, the rest is in Unplaced.";

    public static async Task<CvStructuredImportPreviewDto> BuildPreviewAsync(
        byte[] pdfBytes,
        ICvPdfFullTextExtractor fullTextExtractor,
        ICvStructuredImportAiClient importAiClient,
        ICvSectionCatalog catalog,
        bool googleAiEnabled,
        CancellationToken cancellationToken = default)
    {
        using var pdfStream = new MemoryStream(pdfBytes);
        var extraction = fullTextExtractor.Extract(pdfStream);

        if (extraction.Quality == CvPdfExtractionQuality.Empty || extraction.Lines.Count == 0)
        {
            if (googleAiEnabled)
            {
                var pdfPreview = await TryParseAttachedPdfAsync(
                    pdfBytes,
                    importAiClient,
                    catalog,
                    cancellationToken);

                if (pdfPreview is not null)
                {
                    return pdfPreview;
                }
            }

            throw new InvalidOperationException(CvStructuredImportNotices.EmptyExtraction);
        }

        var lines = extraction.Lines.Select(static (line) => line.Text).ToArray();
        var extractedFullText = string.Join("\n", lines);

        if (CvStructuredImportAiGate.ShouldCallAi(googleAiEnabled, extraction.Quality))
        {
            try
            {
                var aiResult = await importAiClient.ParseAsync(extractedFullText, cancellationToken);
                var preview = TryMapAiResult(aiResult, catalog, extractedFullText, lines);

                if (preview is not null)
                {
                    return preview;
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch
            {
                // Contact-only fallback below.
            }
        }

        var fallback = ContactOnlyCvParser.Parse(lines, catalog);
        return new CvStructuredImportPreviewDto(
            fallback.Sections,
            false,
            ContactOnlyNotice,
            fallback.Unplaced);
    }

    private static async Task<CvStructuredImportPreviewDto?> TryParseAttachedPdfAsync(
        byte[] pdfBytes,
        ICvStructuredImportAiClient importAiClient,
        ICvSectionCatalog catalog,
        CancellationToken cancellationToken)
    {
        try
        {
            var aiResult = await importAiClient.ParsePdfAsync(pdfBytes, cancellationToken);
            return TryMapAiResult(aiResult, catalog, extractedFullText: null, lines: []);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static CvStructuredImportPreviewDto? TryMapAiResult(
        CvStructuredImportResult aiResult,
        ICvSectionCatalog catalog,
        string? extractedFullText,
        IReadOnlyList<string> lines)
    {
        if (aiResult.Sections is not { Count: > 0 })
        {
            return null;
        }

        var write = aiResult.Sections
            .Select((section, index) => new CvStructuredSectionWriteDto(
                null,
                section.Heading,
                CvSectionTypes.Normalize(section.SectionType),
                index,
                section.Entries
                    .Select((entry, entryIndex) => new CvStructuredEntryWriteDto(
                        null,
                        entry.Title,
                        entry.Subtitle,
                        entry.DateRange,
                        entry.Summary,
                        entry.Bullets,
                        entry.TechStack,
                        CvEntrySources.Import,
                        null,
                        entryIndex))
                    .ToArray()))
            .ToArray();

        if (!string.IsNullOrWhiteSpace(extractedFullText))
        {
            write = CvStructuredImportContactGrounding.FilterToSource(write, extractedFullText).ToArray();
        }

        var sections = FixedSectionLayout.Normalize(write, catalog);
        var unplaced = lines.Count == 0
            ? []
            : CvStructuredImportCoverageAudit.CollectMissingSourceLines(lines, sections);

        return new CvStructuredImportPreviewDto(
            sections,
            true,
            ImportedWithAiNotice,
            unplaced);
    }
}
