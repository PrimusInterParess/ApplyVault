using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

/// <summary>
/// Internal PDF import orchestration (AI-first):
/// Extract lines → AI fill (when enabled) → Normalize (+ Contact reshape) → Ground Contact to source → Residual → Notice.
/// Heuristic runs only when AI is disabled or AI fails/returns empty.
/// </summary>
internal static class CvPdfImportPipeline
{
    public static async Task<CvStructuredImportPreviewDto> BuildPreviewAsync(
        byte[] pdfBytes,
        ICvPdfFullTextExtractor fullTextExtractor,
        ICvStructuredImportAiClient importAiClient,
        bool googleAiEnabled,
        CancellationToken cancellationToken = default)
    {
        using var pdfStream = new MemoryStream(pdfBytes);
        var extraction = fullTextExtractor.Extract(pdfStream);

        // AI-first: ordered lines are enough. Sectionize is only for residual / heuristic fallback.
        if (extraction.Quality == CvPdfExtractionQuality.Empty || extraction.Lines.Count == 0)
        {
            throw new InvalidOperationException(CvStructuredImportNotices.EmptyExtraction);
        }

        var extractedFullText = string.Join("\n", extraction.Lines.Select(static (line) => line.Text));
        // Defer catalog Sectionize until residual / heuristic need it (AI fills from full text).
        var rawSections = fullTextExtractor is CvPdfFullTextExtractor concreteExtractor
            ? concreteExtractor.SectionizeForFallback(extraction.Lines)
            : extraction.Sections.Count > 0
                ? extraction.Sections
                : [new CvPdfRawSection("Profile", "summary", 0, extractedFullText)];

        IReadOnlyList<CvStructuredSectionWriteDto> sections;
        IReadOnlyList<CvStructuredSectionWriteDto> baselineForNotices;
        var usedCatchAll = false;
        var usedAi = false;
        var aiAttempted = false;
        var aiFailed = false;

        if (CvStructuredImportAiGate.ShouldCallAi(googleAiEnabled, extraction.Quality))
        {
            aiAttempted = true;
            CvStructuredImportResidualPlacement.Result? aiPlacement = null;

            try
            {
                var aiResult = await importAiClient.ParseAsync(extractedFullText, cancellationToken);

                if (aiResult.Sections is null || aiResult.Sections.Count == 0)
                {
                    aiFailed = true;
                }
                else
                {
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

                    aiSections = CvStructuredImportContactGrounding.FilterToSource(aiSections, extractedFullText);

                    if (aiSections.Count > 0)
                    {
                        aiPlacement = CvStructuredImportResidualPlacement.Apply(aiSections, rawSections);
                        usedAi = true;
                    }
                    else
                    {
                        aiFailed = true;
                    }
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

            if (aiPlacement is not null)
            {
                sections = aiPlacement.Sections;
                usedCatchAll = aiPlacement.UsedCatchAll;
                baselineForNotices = sections;
            }
            else
            {
                var fallback = RunHeuristicFallback(rawSections, extractedFullText);
                sections = fallback.Sections;
                usedCatchAll = fallback.UsedCatchAll;
                baselineForNotices = sections;
            }
        }
        else
        {
            var fallback = RunHeuristicFallback(rawSections, extractedFullText);
            sections = fallback.Sections;
            usedCatchAll = fallback.UsedCatchAll;
            baselineForNotices = sections;
        }

        var notice = CvStructuredImportNotices.Build(
            extraction.Quality,
            baselineForNotices,
            sections,
            usedAi,
            aiAttempted,
            aiFailed,
            usedCatchAll);

        return new CvStructuredImportPreviewDto(sections, usedAi, notice);
    }

    private static CvStructuredImportResidualPlacement.Result RunHeuristicFallback(
        IReadOnlyList<CvPdfRawSection> rawSections,
        string extractedFullText)
    {
        var heuristicSections = CvStructuredImportNormalizer.Normalize(
            CvStructuredImportHeuristic.Parse(rawSections),
            rawSections);

        heuristicSections = CvStructuredImportContactGrounding.FilterToSource(
            heuristicSections,
            extractedFullText);

        return CvStructuredImportResidualPlacement.Apply(heuristicSections, rawSections);
    }
}
