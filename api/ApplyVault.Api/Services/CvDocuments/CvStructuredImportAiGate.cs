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

    public static CvStructuredImportAiGateDecision Decide(
        bool googleAiEnabled,
        CvPdfExtractionQuality extractionQuality,
        IReadOnlyList<CvPdfRawSection> rawSections,
        IReadOnlyList<CvStructuredSectionWriteDto> heuristicSections,
        CvImportAiOptions importAiOptions)
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

        if (extractionQuality == CvPdfExtractionQuality.Sparse)
        {
            return CvStructuredImportAiGateDecision.CallAi;
        }

        if (IsLowHeuristicConfidence(rawSections, heuristicSections, importAiOptions))
        {
            return CvStructuredImportAiGateDecision.CallAi;
        }

        return CvStructuredImportAiGateDecision.SkipAi;
    }

    private static bool IsLowHeuristicConfidence(
        IReadOnlyList<CvPdfRawSection> rawSections,
        IReadOnlyList<CvStructuredSectionWriteDto> heuristicSections,
        CvImportAiOptions importAiOptions)
    {
        var matchedCatalogHeadings = rawSections.Count(static (section) =>
            !section.Heading.Equals("Profile", StringComparison.OrdinalIgnoreCase));

        var bodyChars = rawSections.Sum(static (section) => section.Text.Length);

        // Only the default Profile/Summary bucket with a large body → weak structure.
        if (matchedCatalogHeadings == 0 && bodyChars >= importAiOptions.LowConfidenceMinBodyChars)
        {
            return true;
        }

        var rawBlob = string.Join('\n', rawSections.Select(static (section) => $"{section.Heading}\n{section.Text}"));

        if (ContainsCue(rawBlob, ExperienceCues)
            && !HasSectionType(heuristicSections, CvSectionTypes.Experience))
        {
            return true;
        }

        if (ContainsCue(rawBlob, EducationCues)
            && !HasSectionType(heuristicSections, CvSectionTypes.Education))
        {
            return true;
        }

        if (ContainsCue(rawBlob, ProjectsCues)
            && !HasSectionType(heuristicSections, CvSectionTypes.Projects))
        {
            return true;
        }

        return false;
    }

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
