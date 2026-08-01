using ApplyVault.Api.Models;
using ApplyVault.Api.Options;

namespace ApplyVault.Api.Services;

internal enum CvStructuredImportAiGateDecision
{
    SkipAi = 0,
    CallAi = 1
}

internal static class CvStructuredImportAiGate
{
    private static readonly string[] ExperienceCues =
    [
        "experience",
        "work history",
        "employment",
        "career history"
    ];

    private static readonly string[] EducationCues =
    [
        "education",
        "academic",
        "degrees"
    ];

    private static readonly string[] ProjectsCues =
    [
        "projects",
        "personal projects",
        "side projects"
    ];

    /// <summary>
    /// Residual ratio (before Custom spill) at/above which placement is considered weak when
    /// expected typed sections are also missing.
    /// </summary>
    internal const double WeakResidualRatio = 0.25;

    internal const int WeakResidualMinLines = 3;

    public static CvStructuredImportAiGateDecision Decide(
        bool googleAiEnabled,
        CvPdfExtractionQuality extractionQuality,
        IReadOnlyList<CvPdfRawSection> rawSections,
        IReadOnlyList<CvStructuredSectionWriteDto> heuristicSections,
        CvImportAiOptions importAiOptions,
        CvStructuredImportResidualPlacement.Result? residual = null)
    {
        if (!googleAiEnabled || extractionQuality == CvPdfExtractionQuality.Empty)
        {
            return CvStructuredImportAiGateDecision.SkipAi;
        }

        if (importAiOptions.ForceAi)
        {
            return CvStructuredImportAiGateDecision.CallAi;
        }

        if (heuristicSections.Count == 0)
        {
            return CvStructuredImportAiGateDecision.CallAi;
        }

        var typedCount = CountTypedSections(heuristicSections);

        // Sparse extract only when typed structure is also thin.
        if (extractionQuality == CvPdfExtractionQuality.Sparse && typedCount <= 1)
        {
            return CvStructuredImportAiGateDecision.CallAi;
        }

        if (IsLowHeuristicConfidence(rawSections, heuristicSections, importAiOptions, residual))
        {
            return CvStructuredImportAiGateDecision.CallAi;
        }

        return CvStructuredImportAiGateDecision.SkipAi;
    }

    private static bool IsLowHeuristicConfidence(
        IReadOnlyList<CvPdfRawSection> rawSections,
        IReadOnlyList<CvStructuredSectionWriteDto> heuristicSections,
        CvImportAiOptions importAiOptions,
        CvStructuredImportResidualPlacement.Result? residual)
    {
        var bodyChars = rawSections.Sum(static (section) => section.Text.Length);
        var typedCount = CountTypedSections(heuristicSections);

        // Only Summary/Custom with a large body and no Experience when cues present → weak.
        if (typedCount == 0 && bodyChars >= importAiOptions.LowConfidenceMinBodyChars)
        {
            var rawBlob = string.Join('\n', rawSections.Select(static (section) => $"{section.Heading}\n{section.Text}"));
            if (ContainsCue(rawBlob, ExperienceCues)
                && !HasSectionType(heuristicSections, CvSectionTypes.Experience))
            {
                return true;
            }

            // Large untyped body with no catalog-matched structure beyond Profile.
            var nonProfileRaw = rawSections.Count(static (section) =>
                !section.Heading.Equals("Profile", StringComparison.OrdinalIgnoreCase)
                && !section.Heading.Equals("Summary", StringComparison.OrdinalIgnoreCase));

            if (nonProfileRaw == 0)
            {
                return true;
            }
        }

        var rawText = string.Join('\n', rawSections.Select(static (section) => $"{section.Heading}\n{section.Text}"));

        if (ContainsCue(rawText, ExperienceCues)
            && !HasSectionType(heuristicSections, CvSectionTypes.Experience))
        {
            // Call AI when experience cues exist but typed Experience is missing —
            // unless residual was large and already parked (still weak structure).
            return true;
        }

        if (ContainsCue(rawText, EducationCues)
            && !HasSectionType(heuristicSections, CvSectionTypes.Education))
        {
            return true;
        }

        if (ContainsCue(rawText, ProjectsCues)
            && !HasSectionType(heuristicSections, CvSectionTypes.Projects))
        {
            return true;
        }

        // Placement weak: large residual ratio before spill AND missing expected typed sections.
        if (residual is not null
            && residual.ConsideredSourceLineCount > 0
            && IsLargeResidual(residual)
            && typedCount == 0)
        {
            return true;
        }

        // Do not call AI solely because Custom catch-all was used after a successful residual spill.
        _ = residual?.UsedCatchAll;

        return false;
    }

    private static bool IsLargeResidual(CvStructuredImportResidualPlacement.Result residual)
    {
        if (residual.ResidualLineCountBeforeSpill >= WeakResidualMinLines)
        {
            return true;
        }

        var ratio = residual.ResidualLineCountBeforeSpill / (double)Math.Max(1, residual.ConsideredSourceLineCount);
        return ratio >= WeakResidualRatio;
    }

    private static int CountTypedSections(IReadOnlyList<CvStructuredSectionWriteDto> sections) =>
        sections.Count((section) =>
            !section.SectionType.Equals(CvSectionTypes.Summary, StringComparison.OrdinalIgnoreCase)
            && !section.SectionType.Equals(CvSectionTypes.Custom, StringComparison.OrdinalIgnoreCase));

    private static bool HasSectionType(
        IReadOnlyList<CvStructuredSectionWriteDto> sections,
        string sectionType) =>
        sections.Any((section) =>
            section.SectionType.Equals(sectionType, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsCue(string text, IReadOnlyList<string> cues)
    {
        foreach (var cue in cues)
        {
            if (text.Contains(cue, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
