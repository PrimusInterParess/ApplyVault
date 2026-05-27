using ApplyVault.Api.Models;
using System.Text.RegularExpressions;

namespace ApplyVault.Api.Services;

internal static class CvStructuredImportEntrySupport
{
    private static readonly Regex PhonePattern = new(
        @"\+?\d[\d\s().\-]{6,}\d",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool EntryHasContent(CvStructuredEntryWriteDto entry) =>
        !string.IsNullOrWhiteSpace(entry.Title)
        || !string.IsNullOrWhiteSpace(entry.Subtitle)
        || !string.IsNullOrWhiteSpace(entry.DateRange)
        || !string.IsNullOrWhiteSpace(entry.Summary)
        || entry.Bullets.Count > 0
        || !string.IsNullOrWhiteSpace(entry.TechStack);

    public static bool EntryHasContent(CvStructuredImportEntryResult entry) =>
        !string.IsNullOrWhiteSpace(entry.Title)
        || !string.IsNullOrWhiteSpace(entry.Subtitle)
        || !string.IsNullOrWhiteSpace(entry.DateRange)
        || !string.IsNullOrWhiteSpace(entry.Summary)
        || entry.Bullets?.Count > 0
        || !string.IsNullOrWhiteSpace(entry.TechStack);

    public static bool LooksLikeContactLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var trimmed = line.Trim();

        return trimmed.Contains('@', StringComparison.Ordinal)
            || PhonePattern.IsMatch(trimmed)
            || trimmed.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("github.com", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("https://", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("www.", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("linkedin:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("github:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("email:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("phone:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)
            || IsContactLabelLine(trimmed);
    }

    public static IReadOnlyList<string> SplitContactTokens(string line) =>
        line.Split(['|', '·', '•', '/', '\\'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .SelectMany((token) =>
                token.Contains(',', StringComparison.Ordinal) && LooksLikeContactLine(token)
                    ? token.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    : [token])
            .Where((token) => !string.IsNullOrWhiteSpace(token))
            .ToArray();

    public static (string? NameLine, IReadOnlyList<string> ContactLines, IReadOnlyList<string> RemainingLines)
        SplitLeadingContactBlock(IReadOnlyList<string> lines)
    {
        var contactLines = new List<string>();
        var remaining = new List<string>();
        string? nameLine = null;
        var inContactBlock = true;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            if (inContactBlock)
            {
                if (LooksLikeContactLine(trimmed))
                {
                    contactLines.AddRange(SplitContactTokens(trimmed));
                    continue;
                }

                if (contactLines.Count == 0 && nameLine is null && CouldBeNameLine(trimmed))
                {
                    nameLine = trimmed;
                    continue;
                }

                if (contactLines.Count > 0 || nameLine is not null)
                {
                    inContactBlock = false;
                    remaining.Add(trimmed);
                    continue;
                }

                return (null, [], lines.Where((value) => !string.IsNullOrWhiteSpace(value)).ToArray());
            }

            remaining.Add(trimmed);
        }

        return (nameLine, contactLines, remaining);
    }

    public static CvStructuredSectionWriteDto CreateContactSection(
        IReadOnlyList<string> contactLines,
        string? nameLine = null,
        int sortOrder = 0)
    {
        var bullets = contactLines
            .Select(CvExportTextNormalizer.Field)
            .Where((line) => line.Length > 0)
            .ToArray();

        return new CvStructuredSectionWriteDto(
            null,
            "Contact",
            CvSectionTypes.Custom,
            sortOrder,
            [
                new CvStructuredEntryWriteDto(
                    null,
                    nameLine ?? string.Empty,
                    null,
                    null,
                    string.Empty,
                    bullets,
                    string.Empty,
                    CvEntrySources.Import,
                    null,
                    0)
            ]);
    }

    public static IReadOnlyList<string> ExtractContactLinesFromSource(IReadOnlyList<CvPdfRawSection> sourceSections)
    {
        var contactLines = new List<string>();

        foreach (var section in sourceSections)
        {
            if (IsContactSection(section))
            {
                contactLines.AddRange(
                    section.Text
                        .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                        .SelectMany(SplitContactTokens));

                continue;
            }

            if (!IsHeaderProfileSection(section, sourceSections))
            {
                continue;
            }

            var lines = section.Text
                .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToArray();
            var (_, extractedContactLines, _) = SplitLeadingContactBlock(lines);
            contactLines.AddRange(extractedContactLines);
        }

        return contactLines
            .Select(CvExportTextNormalizer.Field)
            .Where((line) => line.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsContactRepresented(
        IReadOnlyList<CvStructuredSectionWriteDto> sections,
        string contactLine)
    {
        var normalizedContact = CvExportTextNormalizer.Field(contactLine);

        if (normalizedContact.Length == 0)
        {
            return true;
        }

        foreach (var section in sections)
        {
            foreach (var entry in section.Entries)
            {
                if (FieldContains(normalizedContact, entry.Title)
                    || FieldContains(normalizedContact, entry.Subtitle)
                    || FieldContains(normalizedContact, entry.Summary)
                    || FieldContains(normalizedContact, entry.DateRange)
                    || FieldContains(normalizedContact, entry.TechStack)
                    || entry.Bullets.Any((bullet) => FieldContains(normalizedContact, bullet)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static IReadOnlyList<CvStructuredSectionWriteDto> RestoreMissingContactFromSource(
        IReadOnlyList<CvStructuredSectionWriteDto> sections,
        IReadOnlyList<CvPdfRawSection>? sourceSections)
    {
        if (sourceSections is null || sourceSections.Count == 0)
        {
            return sections;
        }

        var sourceContactLines = ExtractContactLinesFromSource(sourceSections);

        if (sourceContactLines.Count == 0)
        {
            return sections;
        }

        var missingContactLines = sourceContactLines
            .Where((line) => !IsContactRepresented(sections, line))
            .ToArray();

        if (missingContactLines.Length == 0)
        {
            return sections;
        }

        var existingContactSectionIndex = sections.ToList().FindIndex((section) =>
            section.SectionType == CvSectionTypes.Custom
            && section.Heading.Equals("Contact", StringComparison.OrdinalIgnoreCase));

        if (existingContactSectionIndex >= 0)
        {
            var existingContactSection = sections[existingContactSectionIndex];
            var existingEntry = existingContactSection.Entries.FirstOrDefault();
            var mergedBullets = (existingEntry?.Bullets ?? [])
                .Concat(missingContactLines)
                .Select(CvExportTextNormalizer.Field)
                .Where((line) => line.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var updatedEntry = (existingEntry ?? new CvStructuredEntryWriteDto(
                null,
                string.Empty,
                null,
                null,
                string.Empty,
                [],
                string.Empty,
                CvEntrySources.Import,
                null,
                0)) with
            {
                Bullets = mergedBullets
            };

            return sections
                .Select((section, index) =>
                    index == existingContactSectionIndex
                        ? section with { Entries = [updatedEntry] }
                        : section)
                .ToArray();
        }

        var contactSection = CreateContactSection(missingContactLines);

        return sections
            .Prepend(contactSection)
            .Select((section, index) => section with { SortOrder = index })
            .ToArray();
    }

    private static bool IsContactSection(CvPdfRawSection section) =>
        section.NormalizedKey.Equals("contact", StringComparison.OrdinalIgnoreCase)
        || section.NormalizedKey.Equals("contact information", StringComparison.OrdinalIgnoreCase)
        || CvPdfSectionDetector.NormalizeHeading(section.Heading)
            .Equals("contact", StringComparison.OrdinalIgnoreCase)
        || CvPdfSectionDetector.NormalizeHeading(section.Heading)
            .Equals("contact information", StringComparison.OrdinalIgnoreCase);

    private static bool IsHeaderProfileSection(CvPdfRawSection section, IReadOnlyList<CvPdfRawSection> sourceSections) =>
        section.Heading.Equals("Profile", StringComparison.OrdinalIgnoreCase)
        || (ReferenceEquals(section, sourceSections[0])
            && CvStructuredImportSectionTypeMapping.MapSectionType(section.NormalizedKey) == CvSectionTypes.Summary);

    private static bool CouldBeNameLine(string line)
    {
        if (LooksLikeContactLine(line) || line.Length > 64)
        {
            return false;
        }

        if (line.Any(char.IsDigit))
        {
            return false;
        }

        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return words.Length is >= 1 and <= 5;
    }

    private static bool IsContactLabelLine(string line) =>
        line.StartsWith("contact", StringComparison.OrdinalIgnoreCase)
        && line.Length <= 32;

    private static bool FieldContains(string needle, string? haystack)
    {
        if (string.IsNullOrWhiteSpace(haystack))
        {
            return false;
        }

        return haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }
}
