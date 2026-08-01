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

    /// <summary>
    /// Merge a normalized AI update into the current structured CV before persist.
    /// Matches FE <c>mergeAssistStructuredUpdate</c>: with focus, replace only those
    /// sections and ignore non-focused AI sections; without focus, AI wins by id while
    /// omitted current sections are preserved and AI-only sections are appended.
    /// </summary>
    public static SaveCvStructuredDocumentRequest MergeAssistUpdate(
        CvStructuredDocumentDto current,
        SaveCvStructuredDocumentRequest aiResult,
        IReadOnlyList<Guid>? focusSectionIds)
    {
        if (current.Sections.Count == 0)
        {
            return Reindex(aiResult.Sections);
        }

        var aiById = aiResult.Sections
            .Where((section) => section.Id is not null)
            .GroupBy((section) => section.Id!.Value)
            .ToDictionary((group) => group.Key, (group) => group.First());

        var focusSet = focusSectionIds is { Count: > 0 }
            ? focusSectionIds.ToHashSet()
            : null;

        List<CvStructuredSectionWriteDto> mergedSections;

        if (focusSet is not null)
        {
            mergedSections = current.Sections
                .Select((section) =>
                {
                    if (!focusSet.Contains(section.Id))
                    {
                        return ToWriteDto(section);
                    }

                    return aiById.TryGetValue(section.Id, out var fromAi)
                        ? fromAi
                        : ToWriteDto(section);
                })
                .ToList();
        }
        else
        {
            var previousIds = current.Sections.Select((section) => section.Id).ToHashSet();

            mergedSections = current.Sections
                .Select((section) =>
                    aiById.TryGetValue(section.Id, out var fromAi)
                        ? fromAi
                        : ToWriteDto(section))
                .ToList();

            foreach (var section in aiResult.Sections)
            {
                if (section.Id is null || !previousIds.Contains(section.Id.Value))
                {
                    mergedSections.Add(section);
                }
            }
        }

        return Reindex(mergedSections);
    }

    private static SaveCvStructuredDocumentRequest Reindex(
        IReadOnlyList<CvStructuredSectionWriteDto> sections) =>
        new(
            sections
                .Select((section, sectionIndex) => section with
                {
                    SortOrder = sectionIndex,
                    Entries = section.Entries
                        .Select((entry, entryIndex) => entry with { SortOrder = entryIndex })
                        .ToArray()
                })
                .ToArray());

    private static CvStructuredSectionWriteDto ToWriteDto(CvStructuredSectionDto section) =>
        new(
            section.Id,
            section.Heading,
            section.SectionType,
            section.SortOrder,
            section.Entries
                .OrderBy((entry) => entry.SortOrder)
                .Select((entry, entryIndex) => new CvStructuredEntryWriteDto(
                    entry.Id,
                    entry.Title,
                    entry.Subtitle,
                    entry.DateRange,
                    entry.Summary,
                    entry.Bullets,
                    entry.TechStack,
                    entry.Source,
                    entry.SourceSummaryId,
                    entryIndex))
                .ToArray());

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
