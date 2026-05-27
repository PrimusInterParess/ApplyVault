using System.Text.RegularExpressions;
using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

internal static class CvStructuredImportNormalizer
{
    private static readonly Regex DatePattern = new(
        @"\b(?:Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\.?\s+\d{4}\s*(?:[–\-—]|to)\s*(?:Present|\d{4}|(?:Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|Nov(?:ember)?|Dec(?:ember)?)\.?\s+\d{4})|\b\d{4}\s*(?:[–\-—]|to)\s*(?:Present|\d{4})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IReadOnlyList<CvStructuredSectionWriteDto> Normalize(
        IReadOnlyList<CvStructuredSectionWriteDto> sections,
        IReadOnlyList<CvPdfRawSection>? sourceSections = null)
    {
        var sourceHints = BuildSourceHints(sourceSections);

        var normalizedSections = sections
            .Select((section) => NormalizeSection(section, sourceHints))
            .SelectMany(ExtractContactFromSummarySection)
            .Where((section) =>
                !string.IsNullOrWhiteSpace(section.Heading)
                || section.Entries.Count > 0)
            .Select((section, sectionIndex) => section with
            {
                SortOrder = sectionIndex,
                Entries = section.Entries
                    .Select((entry, entryIndex) => entry with { SortOrder = entryIndex })
                    .ToArray()
            })
            .ToArray();

        return CvStructuredImportEntrySupport.RestoreMissingContactFromSource(normalizedSections, sourceSections);
    }

    private static Dictionary<string, string> BuildSourceHints(IReadOnlyList<CvPdfRawSection>? sourceSections)
    {
        var hints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (sourceSections is null)
        {
            return hints;
        }

        foreach (var section in sourceSections)
        {
            var mappedType = CvStructuredImportSectionTypeMapping.MapSectionType(section.NormalizedKey);

            if (mappedType == CvSectionTypes.Custom)
            {
                continue;
            }

            var headingKey = CvPdfSectionDetector.NormalizeHeading(section.Heading);

            if (!string.IsNullOrWhiteSpace(headingKey))
            {
                hints[headingKey] = mappedType;
            }

            hints[section.NormalizedKey] = mappedType;
        }

        return hints;
    }

    private static CvStructuredSectionWriteDto NormalizeSection(
        CvStructuredSectionWriteDto section,
        IReadOnlyDictionary<string, string> sourceHints)
    {
        var heading = NormalizeField(section.Heading);
        var sectionType = ResolveSectionType(section.SectionType, heading, sourceHints);

        var entries = section.Entries
            .Select((entry) => NormalizeEntry(entry, sectionType))
            .Where(CvStructuredImportEntrySupport.EntryHasContent)
            .ToArray();

        return section with
        {
            Heading = heading,
            SectionType = sectionType,
            Entries = entries
        };
    }

    private static string ResolveSectionType(
        string sectionType,
        string heading,
        IReadOnlyDictionary<string, string> sourceHints)
    {
        var normalizedType = CvSectionTypes.Normalize(sectionType);
        var normalizedHeading = CvPdfSectionDetector.NormalizeHeading(heading);

        if (sourceHints.TryGetValue(normalizedHeading, out var hintedType)
            && normalizedType == CvSectionTypes.Custom
            && hintedType != CvSectionTypes.Custom)
        {
            return hintedType;
        }

        return normalizedType;
    }

    private static CvStructuredEntryWriteDto NormalizeEntry(CvStructuredEntryWriteDto entry, string sectionType)
    {
        var title = StripBulletMarkers(NormalizeField(entry.Title));
        var subtitle = NullIfEmpty(StripBulletMarkers(NormalizeField(entry.Subtitle)));
        var dateRange = NullIfEmpty(NormalizeField(entry.DateRange));
        var summary = StripBulletMarkers(NormalizeField(entry.Summary));
        var techStack = NormalizeField(entry.TechStack);
        var bullets = CvExportTextNormalizer.Bullets(entry.Bullets.Select(StripBulletMarkers).ToArray());

        (title, subtitle, dateRange) = RelocateEmbeddedDates(title, subtitle, dateRange);

        if (sectionType == CvSectionTypes.Skills
            && bullets.Count == 0
            && !string.IsNullOrWhiteSpace(summary))
        {
            bullets = PromoteSummaryToSkillBullets(summary);
            summary = string.Empty;
        }

        return entry with
        {
            Title = title,
            Subtitle = subtitle,
            DateRange = dateRange,
            Summary = summary,
            Bullets = bullets,
            TechStack = techStack
        };
    }

    private static (string Title, string? Subtitle, string? DateRange) RelocateEmbeddedDates(
        string title,
        string? subtitle,
        string? dateRange)
    {
        if (!string.IsNullOrWhiteSpace(dateRange))
        {
            return (title, subtitle, dateRange);
        }

        if (TryExtractDateRange(title, out var titleDate, out var cleanedTitle))
        {
            return (cleanedTitle, subtitle, titleDate);
        }

        if (!string.IsNullOrWhiteSpace(subtitle)
            && TryExtractDateRange(subtitle, out var subtitleDate, out var cleanedSubtitle))
        {
            return (title, cleanedSubtitle, subtitleDate);
        }

        return (title, subtitle, dateRange);
    }

    private static bool TryExtractDateRange(string value, out string dateRange, out string remainder)
    {
        dateRange = string.Empty;
        remainder = value;

        var match = DatePattern.Match(value);

        if (!match.Success)
        {
            return false;
        }

        dateRange = NormalizeField(match.Value);
        remainder = NormalizeField(value.Remove(match.Index, match.Length));

        return true;
    }

    private static IReadOnlyList<string> PromoteSummaryToSkillBullets(string summary) =>
        summary
            .Split([',', ';', '|', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeField)
            .Where((item) => item.Length > 0)
            .ToArray();

    private static IEnumerable<CvStructuredSectionWriteDto> ExtractContactFromSummarySection(
        CvStructuredSectionWriteDto section)
    {
        if (section.SectionType != CvSectionTypes.Summary || section.Entries.Count == 0)
        {
            yield return section;
            yield break;
        }

        var firstEntry = section.Entries[0];

        if (section.Entries.Count > 1 || string.IsNullOrWhiteSpace(firstEntry.Summary))
        {
            yield return section;
            yield break;
        }

        var lines = firstEntry.Summary
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToArray();
        var (nameLine, contactLines, remainingLines) = CvStructuredImportEntrySupport.SplitLeadingContactBlock(lines);

        if (contactLines.Count == 0)
        {
            yield return section;
            yield break;
        }

        yield return CvStructuredImportEntrySupport.CreateContactSection(contactLines, nameLine, section.SortOrder);

        if (remainingLines.Count == 0)
        {
            yield break;
        }

        yield return section with
        {
            Entries =
            [
                firstEntry with
                {
                    Summary = string.Join('\n', remainingLines)
                }
            ]
        };
    }

    private static string NormalizeField(string? value) =>
        CvExportTextNormalizer.Field(value);

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string StripBulletMarkers(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return CvStructuredImportHeuristic.TrimBullet(value.Trim());
    }
}
