using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

internal static class CvStructuredUpdateNormalizer
{
    public static SaveCvStructuredDocumentRequest Normalize(CvStructuredUpdateAiResponse response)
    {
        var sections = response.Sections
            .Where((section) => !string.IsNullOrWhiteSpace(section.Heading))
            .OrderBy((section) => section.SortOrder)
            .Select((section, sectionIndex) => new CvStructuredSectionWriteDto(
                ParseGuid(section.Id),
                section.Heading.Trim(),
                CvSectionTypes.Normalize(section.SectionType),
                sectionIndex,
                section.Entries
                    .Where(EntryHasContent)
                    .OrderBy((entry) => entry.SortOrder)
                    .Select((entry, entryIndex) => new CvStructuredEntryWriteDto(
                        ParseGuid(entry.Id),
                        entry.Title.Trim(),
                        string.IsNullOrWhiteSpace(entry.Subtitle) ? null : entry.Subtitle.Trim(),
                        string.IsNullOrWhiteSpace(entry.DateRange) ? null : entry.DateRange.Trim(),
                        entry.Summary?.Trim() ?? string.Empty,
                        entry.Bullets
                            .Where((bullet) => !string.IsNullOrWhiteSpace(bullet))
                            .Select((bullet) => bullet.Trim().TrimStart('-', '*', '•').Trim())
                            .Where((bullet) => bullet.Length > 0)
                            .ToArray(),
                        entry.TechStack?.Trim() ?? string.Empty,
                        string.IsNullOrWhiteSpace(entry.Source) ? CvEntrySources.Manual : entry.Source.Trim(),
                        ParseGuid(entry.SourceSummaryId),
                        entryIndex))
                    .ToArray()))
            .Where((section) => section.Entries.Count > 0)
            .ToArray();

        return new SaveCvStructuredDocumentRequest(sections);
    }

    private static Guid? ParseGuid(string? value) =>
        Guid.TryParse(value, out var id) ? id : null;

    private static bool EntryHasContent(CvStructuredUpdateAiEntry entry) =>
        !string.IsNullOrWhiteSpace(entry.Title)
        || !string.IsNullOrWhiteSpace(entry.Subtitle)
        || !string.IsNullOrWhiteSpace(entry.DateRange)
        || !string.IsNullOrWhiteSpace(entry.Summary)
        || entry.Bullets.Any((bullet) => !string.IsNullOrWhiteSpace(bullet))
        || !string.IsNullOrWhiteSpace(entry.TechStack);
}
