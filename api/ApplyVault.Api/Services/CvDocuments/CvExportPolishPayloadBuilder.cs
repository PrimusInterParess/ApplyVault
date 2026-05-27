using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

internal static class CvExportPolishPayloadBuilder
{
    public static IReadOnlyList<CvExportSectionInput> FromDocument(CvStructuredDocumentDto document) =>
        document.Sections
            .OrderBy((section) => section.SortOrder)
            .Select(FromSection)
            .ToArray();

    private static CvExportSectionInput FromSection(CvStructuredSectionDto section) =>
        new(
            section.Heading,
            CvSectionTypes.Normalize(section.SectionType),
            section.SortOrder,
            section.Entries
                .OrderBy((entry) => entry.SortOrder)
                .Select(FromEntry)
                .ToArray());

    private static CvExportPolishEntryInput FromEntry(CvStructuredEntryDto entry) =>
        new(
            entry.Title,
            entry.Subtitle,
            entry.DateRange,
            entry.Summary,
            entry.Bullets,
            entry.TechStack);
}
