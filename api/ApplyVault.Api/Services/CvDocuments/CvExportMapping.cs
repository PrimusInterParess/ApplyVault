using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

internal static class CvExportMapping
{
    public static CvExportRenderRequest FromStructuredDocument(
        CvStructuredDocumentDto document,
        byte[]? profilePhotoBytes,
        string? profilePhotoContentType)
    {
        var hasPhoto = profilePhotoBytes is { Length: > 0 };

        return new CvExportRenderRequest(
            CvExportLayoutDefaults.Document(hasPhoto),
            document.Sections
                .OrderBy((section) => section.SortOrder)
                .Select(FromStructuredSection)
                .ToArray(),
            profilePhotoBytes,
            profilePhotoContentType);
    }

    public static CvExportRenderRequest FromImportResult(
        CvStructuredImportResult result,
        CvStructuredDocumentDto sourceDocument,
        byte[]? profilePhotoBytes,
        string? profilePhotoContentType)
    {
        var hasPhoto = profilePhotoBytes is { Length: > 0 };
        var documentLayout = CvExportLayoutResolver.ResolveDocument(null, hasPhoto);

        var sourceSections = sourceDocument.Sections.ToDictionary(
            (section) => section.Heading,
            (section) => section.SortOrder,
            StringComparer.OrdinalIgnoreCase);

        var sections = result.Sections
            .Select((section, index) =>
            {
                var sortOrder = sourceSections.TryGetValue(section.Heading, out var order) ? order : index;

                return new CvExportSection(
                    CvExportTextNormalizer.Field(section.Heading),
                    CvSectionTypes.Normalize(section.SectionType),
                    sortOrder,
                    section.Entries
                        .Select((entry, entryIndex) => FromImportEntry(entry, entryIndex))
                        .ToArray());
            })
            .OrderBy((section) => section.SortOrder)
            .ToArray();

        return new CvExportRenderRequest(documentLayout, sections, profilePhotoBytes, profilePhotoContentType);
    }

    private static CvExportSection FromStructuredSection(CvStructuredSectionDto section) =>
        new(
            CvExportTextNormalizer.Field(section.Heading),
            CvSectionTypes.Normalize(section.SectionType),
            section.SortOrder,
            section.Entries
                .OrderBy((entry) => entry.SortOrder)
                .Select(FromStructuredEntry)
                .ToArray());

    private static CvExportEntry FromStructuredEntry(CvStructuredEntryDto entry) =>
        new(
            CvExportTextNormalizer.Field(entry.Title),
            NullIfEmpty(entry.Subtitle),
            NullIfEmpty(entry.DateRange),
            CvExportTextNormalizer.Field(entry.Summary),
            CvExportTextNormalizer.Bullets(entry.Bullets),
            CvExportTextNormalizer.Field(entry.TechStack));

    private static CvExportEntry FromImportEntry(CvStructuredImportEntryResult entry, int sortOrder) =>
        new(
            CvExportTextNormalizer.Field(entry.Title),
            NullIfEmpty(entry.Subtitle),
            NullIfEmpty(entry.DateRange),
            CvExportTextNormalizer.Field(entry.Summary),
            CvExportTextNormalizer.Bullets(entry.Bullets ?? []),
            CvExportTextNormalizer.Field(entry.TechStack));

    private static string? NullIfEmpty(string? value)
    {
        var normalized = CvExportTextNormalizer.Field(value);
        return normalized.Length == 0 ? null : normalized;
    }
}
