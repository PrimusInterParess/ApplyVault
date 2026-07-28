using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

internal static class CvStructuredUpdateNormalizer
{
    public static SaveCvStructuredDocumentRequest Normalize(
        CvStructuredDocumentDto current,
        CvStructuredUpdateAiResponse response)
    {
        var knownSourceSummaryIds = current.Sections
            .SelectMany((section) => section.Entries)
            .Select((entry) => entry.SourceSummaryId)
            .OfType<Guid>()
            .ToHashSet();

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
                    .Select((entry, entryIndex) => NormalizeEntry(
                        section.SectionType,
                        entry,
                        entryIndex,
                        knownSourceSummaryIds))
                    .ToArray()))
            .Where((section) => section.Entries.Count > 0)
            .ToArray();

        return new SaveCvStructuredDocumentRequest(sections);
    }

    private static CvStructuredEntryWriteDto NormalizeEntry(
        string sectionType,
        CvStructuredUpdateAiEntry entry,
        int entryIndex,
        HashSet<Guid> knownSourceSummaryIds)
    {
        var bullets = entry.Bullets
            .Where((bullet) => !string.IsNullOrWhiteSpace(bullet))
            .Select((bullet) => bullet.Trim().TrimStart('-', '*', '•').Trim())
            .Where((bullet) => bullet.Length > 0)
            .ToArray();
        var techStack = entry.TechStack?.Trim() ?? string.Empty;

        if (CvSectionTypes.Normalize(sectionType) == CvSectionTypes.Skills && bullets.Length > 0)
        {
            if (string.IsNullOrWhiteSpace(techStack))
            {
                techStack = string.Join(", ", bullets);
            }

            bullets = [];
        }

        return new CvStructuredEntryWriteDto(
            ParseGuid(entry.Id),
            entry.Title.Trim(),
            string.IsNullOrWhiteSpace(entry.Subtitle) ? null : entry.Subtitle.Trim(),
            string.IsNullOrWhiteSpace(entry.DateRange) ? null : entry.DateRange.Trim(),
            entry.Summary?.Trim() ?? string.Empty,
            bullets,
            techStack,
            string.IsNullOrWhiteSpace(entry.Source) ? CvEntrySources.Manual : entry.Source.Trim(),
            ParseKnownSourceSummaryId(entry.SourceSummaryId, knownSourceSummaryIds),
            entryIndex);
    }

    private static Guid? ParseGuid(string? value) =>
        Guid.TryParse(value, out var id) ? id : null;

    private static Guid? ParseKnownSourceSummaryId(string? value, HashSet<Guid> knownSourceSummaryIds)
    {
        var sourceSummaryId = ParseGuid(value);

        return sourceSummaryId is not null && knownSourceSummaryIds.Contains(sourceSummaryId.Value)
            ? sourceSummaryId
            : null;
    }

    private static bool EntryHasContent(CvStructuredUpdateAiEntry entry) =>
        !string.IsNullOrWhiteSpace(entry.Title)
        || !string.IsNullOrWhiteSpace(entry.Subtitle)
        || !string.IsNullOrWhiteSpace(entry.DateRange)
        || !string.IsNullOrWhiteSpace(entry.Summary)
        || entry.Bullets.Any((bullet) => !string.IsNullOrWhiteSpace(bullet))
        || !string.IsNullOrWhiteSpace(entry.TechStack);
}
