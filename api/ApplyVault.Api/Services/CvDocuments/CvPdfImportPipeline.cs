using ApplyVault.Api.Models;
using ApplyVault.Api.Options;

namespace ApplyVault.Api.Services;

/// <summary>
/// Internal PDF import orchestration: Extract → Heuristic → gated AI → Notice.
/// </summary>
internal static class CvPdfImportPipeline
{
    public static async Task<CvStructuredImportPreviewDto> BuildPreviewAsync(
        byte[] pdfBytes,
        ICvPdfFullTextExtractor fullTextExtractor,
        ICvStructuredImportAiClient importAiClient,
        bool googleAiEnabled,
        CvImportAiOptions importAiOptions,
        CancellationToken cancellationToken = default)
    {
        using var pdfStream = new MemoryStream(pdfBytes);
        var extraction = fullTextExtractor.Extract(pdfStream);

        if (extraction.Quality == CvPdfExtractionQuality.Empty
            || extraction.Lines.Count == 0
            || extraction.Sections.Count == 0)
        {
            throw new InvalidOperationException(CvStructuredImportNotices.EmptyExtraction);
        }

        var rawSections = extraction.Sections;

        var heuristicSections = CvStructuredImportNormalizer.Normalize(
            CvStructuredImportHeuristic.Parse(rawSections),
            rawSections);

        var gate = CvStructuredImportAiGate.Decide(
            googleAiEnabled,
            extraction.Quality,
            rawSections,
            heuristicSections,
            importAiOptions);

        var sections = heuristicSections;
        var usedAi = false;
        var aiAttempted = false;
        var aiFailed = false;

        if (gate == CvStructuredImportAiGateDecision.CallAi)
        {
            aiAttempted = true;

            try
            {
                var aiInput = rawSections
                    .Select((section) => new CvImportSectionInput(section.Heading, section.NormalizedKey, section.Text))
                    .ToArray();

                var aiResult = await importAiClient.ParseAsync(aiInput, cancellationToken);

                var aiSections = CvStructuredImportNormalizer.Normalize(
                    aiResult.Sections
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
                        .ToArray(),
                    rawSections);

                if (aiSections.Count > 0)
                {
                    sections = aiSections;
                    usedAi = true;
                }
                else
                {
                    aiFailed = true;
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch
            {
                aiFailed = true;
            }
        }

        var notice = CvStructuredImportNotices.Build(
            extraction.Quality,
            heuristicSections,
            sections,
            usedAi,
            aiAttempted,
            aiFailed);

        return new CvStructuredImportPreviewDto(sections, usedAi, notice);
    }
}
