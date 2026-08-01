using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

internal static class CvStructuredImportNotices
{
    public const string EmptyExtraction =
        "No readable text was found in this PDF. Upload a text-based PDF rather than a scanned image.";

    public const string IncompleteReview =
        "Import may be incomplete — review sections in the CV builder.";

    public static string? Build(
        CvPdfExtractionQuality extractionQuality,
        IReadOnlyList<CvStructuredSectionWriteDto> heuristicSections,
        IReadOnlyList<CvStructuredSectionWriteDto> finalSections,
        bool usedAi,
        bool aiAttempted,
        bool aiFailed)
    {
        if (extractionQuality == CvPdfExtractionQuality.Empty)
        {
            return EmptyExtraction;
        }

        if (finalSections.Count == 0)
        {
            return IncompleteReview;
        }

        var heuristicWeak = IsWeak(heuristicSections, extractionQuality);

        // Sparse extract with weak structure and AI skipped or failed → ask user to review.
        if (extractionQuality == CvPdfExtractionQuality.Sparse
            && heuristicWeak
            && (!aiAttempted || aiFailed || !usedAi))
        {
            return IncompleteReview;
        }

        // AI failed and heuristic is also weak → high-signal review notice.
        if (aiFailed && heuristicWeak)
        {
            return IncompleteReview;
        }

        // Quiet success (including when AI was used — D4 omit "AI assisted").
        _ = usedAi;
        return null;
    }

    private static bool IsWeak(
        IReadOnlyList<CvStructuredSectionWriteDto> sections,
        CvPdfExtractionQuality extractionQuality)
    {
        if (sections.Count == 0)
        {
            return true;
        }

        if (extractionQuality == CvPdfExtractionQuality.Sparse)
        {
            return true;
        }

        var typed = sections.Count((section) =>
            !section.SectionType.Equals(CvSectionTypes.Summary, StringComparison.OrdinalIgnoreCase)
            && !section.SectionType.Equals(CvSectionTypes.Custom, StringComparison.OrdinalIgnoreCase));

        return typed == 0 && sections.Count <= 2;
    }
}
