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
            (section) => section,
            StringComparer.OrdinalIgnoreCase);

        var sections = result.Sections
            .Select((section, index) =>
            {
                sourceSections.TryGetValue(section.Heading, out var sourceSection);
                var sourceEntries = sourceSection?.Entries
                    .OrderBy((entry) => entry.SortOrder)
                    .ToArray() ?? [];
                var sortOrder = sourceSection?.SortOrder ?? index;

                return new CvExportSection(
                    CvExportTextNormalizer.Field(section.Heading),
                    CvSectionTypes.Normalize(section.SectionType),
                    sortOrder,
                    section.Entries
                        .Select((entry, entryIndex) => PreserveSourceEntryLinks(
                            FromImportEntry(entry),
                            sourceEntries.ElementAtOrDefault(entryIndex)))
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

    private static CvExportEntry FromImportEntry(CvStructuredImportEntryResult entry) =>
        new(
            CvExportTextNormalizer.Field(entry.Title),
            NullIfEmpty(entry.Subtitle),
            NullIfEmpty(entry.DateRange),
            CvExportTextNormalizer.Field(entry.Summary),
            CvExportTextNormalizer.Bullets(entry.Bullets ?? []),
            CvExportTextNormalizer.Field(entry.TechStack));

    private static CvExportEntry PreserveSourceEntryLinks(
        CvExportEntry entry,
        CvStructuredEntryDto? sourceEntry) =>
        sourceEntry is null
            ? entry
            : entry with
            {
                Title = PreserveLinkMarkup(entry.Title, sourceEntry.Title),
                Subtitle = PreserveOptionalLinkMarkup(entry.Subtitle, sourceEntry.Subtitle)
            };

    private static string? PreserveOptionalLinkMarkup(string? value, string? sourceValue) =>
        value is null ? null : PreserveLinkMarkup(value, sourceValue);

    private static string PreserveLinkMarkup(string value, string? sourceValue)
    {
        var source = CvExportTextNormalizer.Field(sourceValue);

        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(source))
        {
            return value;
        }

        var valueRuns = CvExportInlineParser.ParseRuns(value);

        if (valueRuns.Any((run) => run.LinkUrl is not null))
        {
            return value;
        }

        var sourceRuns = CvExportInlineParser.ParseRuns(source);

        if (!sourceRuns.Any((run) => run.LinkUrl is not null))
        {
            return value;
        }

        var sourcePlainText = string.Concat(sourceRuns.Select((run) => run.Text));

        return string.Equals(value, sourcePlainText, StringComparison.OrdinalIgnoreCase)
            ? source
            : value;
    }

    private static string? NullIfEmpty(string? value)
    {
        var normalized = CvExportTextNormalizer.Field(value);
        return normalized.Length == 0 ? null : normalized;
    }
}
