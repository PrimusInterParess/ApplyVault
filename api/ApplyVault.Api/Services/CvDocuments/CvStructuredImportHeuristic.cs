using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

internal static class CvStructuredImportHeuristic
{
    public static IReadOnlyList<CvStructuredSectionWriteDto> Parse(IReadOnlyList<CvPdfRawSection> rawSections)
    {
        var sections = new List<CvStructuredSectionWriteDto>();

        for (var sectionIndex = 0; sectionIndex < rawSections.Count; sectionIndex++)
        {
            var section = rawSections[sectionIndex];

            if (IsDedicatedContactSection(section))
            {
                sections.Add(ParseContactSection(section, sections.Count));
                continue;
            }

            var sectionType = CvStructuredImportSectionTypeMapping.MapSectionType(section.NormalizedKey);

            if (sectionType == CvSectionTypes.Summary || IsHeaderProfileSection(section, rawSections))
            {
                sections.AddRange(ParseSummaryOrHeaderSection(section, sections.Count));
                continue;
            }

            sections.Add(new CvStructuredSectionWriteDto(
                null,
                section.Heading,
                sectionType,
                sections.Count,
                ParseEntries(section.Text, sectionType)));
        }

        return sections
            .Where((section) => section.Entries.Count > 0 || !string.IsNullOrWhiteSpace(section.Heading))
            .Select((section, sectionIndex) => section with { SortOrder = sectionIndex })
            .ToArray();
    }

    private static bool IsDedicatedContactSection(CvPdfRawSection section) =>
        section.NormalizedKey.Equals("contact", StringComparison.OrdinalIgnoreCase)
        || section.NormalizedKey.Equals("contact information", StringComparison.OrdinalIgnoreCase)
        || CvPdfSectionDetector.NormalizeHeading(section.Heading)
            .Equals("contact", StringComparison.OrdinalIgnoreCase)
        || CvPdfSectionDetector.NormalizeHeading(section.Heading)
            .Equals("contact information", StringComparison.OrdinalIgnoreCase);

    private static bool IsHeaderProfileSection(CvPdfRawSection section, IReadOnlyList<CvPdfRawSection> rawSections) =>
        section.Heading.Equals("Profile", StringComparison.OrdinalIgnoreCase)
        || (sectionIndexIsFirst(section, rawSections)
            && CvStructuredImportSectionTypeMapping.MapSectionType(section.NormalizedKey) == CvSectionTypes.Summary);

    private static bool sectionIndexIsFirst(CvPdfRawSection section, IReadOnlyList<CvPdfRawSection> rawSections) =>
        rawSections.Count > 0 && ReferenceEquals(section, rawSections[0]);

    private static CvStructuredSectionWriteDto ParseContactSection(CvPdfRawSection section, int sortOrder)
    {
        var lines = section.Text
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(CvStructuredImportEntrySupport.SplitContactTokens)
            .ToArray();

        return CvStructuredImportEntrySupport.CreateContactSection(lines, sortOrder: sortOrder);
    }

    private static IReadOnlyList<CvStructuredSectionWriteDto> ParseSummaryOrHeaderSection(
        CvPdfRawSection section,
        int sortOrder)
    {
        var lines = section.Text
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToArray();
        var (nameLine, contactLines, remainingLines) = CvStructuredImportEntrySupport.SplitLeadingContactBlock(lines);
        var sections = new List<CvStructuredSectionWriteDto>();

        if (contactLines.Count > 0)
        {
            sections.Add(CvStructuredImportEntrySupport.CreateContactSection(contactLines, nameLine, sections.Count));
        }

        if (remainingLines.Count > 0)
        {
            sections.Add(new CvStructuredSectionWriteDto(
                null,
                section.Heading.Equals("Profile", StringComparison.OrdinalIgnoreCase) ? "Summary" : section.Heading,
                CvSectionTypes.Summary,
                sections.Count,
                [
                    new CvStructuredEntryWriteDto(
                        null,
                        string.Empty,
                        null,
                        null,
                        string.Join('\n', remainingLines),
                        [],
                        string.Empty,
                        CvEntrySources.Import,
                        null,
                        0)
                ]));
        }
        else if (contactLines.Count == 0)
        {
            sections.Add(new CvStructuredSectionWriteDto(
                null,
                section.Heading,
                CvSectionTypes.Summary,
                sortOrder,
                ParseSummaryEntries(section.Text)));
        }

        return sections;
    }

    private static IReadOnlyList<CvStructuredEntryWriteDto> ParseSummaryEntries(string text) =>
    [
        new CvStructuredEntryWriteDto(
            null,
            string.Empty,
            null,
            null,
            text.Trim(),
            [],
            string.Empty,
            CvEntrySources.Import,
            null,
            0)
    ];

    private static IReadOnlyList<CvStructuredEntryWriteDto> ParseEntries(string text, string sectionType)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        if (sectionType == CvSectionTypes.Summary)
        {
            return ParseSummaryEntries(text);
        }

        if (sectionType == CvSectionTypes.Skills)
        {
            return ParseSkillsEntries(text);
        }

        var chunks = text.Split("\n\n", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (chunks.Length <= 1)
        {
            var lines = text
                .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            chunks = SplitIntoEntryChunks(lines);
        }

        return chunks
            .Select((chunk, index) => ParseEntryChunk(chunk, index))
            .Where(CvStructuredImportEntrySupport.EntryHasContent)
            .ToArray();
    }

    private static IReadOnlyList<CvStructuredEntryWriteDto> ParseSkillsEntries(string text)
    {
        var lines = text
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var groupedEntries = new List<CvStructuredEntryWriteDto>();

        foreach (var line in lines)
        {
            var colonIndex = line.IndexOf(':');

            if (colonIndex > 0 && colonIndex < line.Length - 1)
            {
                var title = line[..colonIndex].Trim();
                var values = line[(colonIndex + 1)..]
                    .Split([',', ';', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Where((value) => !string.IsNullOrWhiteSpace(value))
                    .ToArray();

                if (values.Length > 0)
                {
                    groupedEntries.Add(new CvStructuredEntryWriteDto(
                        null,
                        title,
                        null,
                        null,
                        string.Empty,
                        values,
                        string.Empty,
                        CvEntrySources.Import,
                        null,
                        groupedEntries.Count));

                    continue;
                }
            }

            var inlineValues = line
                .Split([',', ';', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where((value) => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            if (inlineValues.Length > 1)
            {
                groupedEntries.Add(new CvStructuredEntryWriteDto(
                    null,
                    "Skills",
                    null,
                    null,
                    string.Empty,
                    inlineValues,
                    string.Empty,
                    CvEntrySources.Import,
                    null,
                    groupedEntries.Count));
            }
            else if (inlineValues.Length == 1)
            {
                groupedEntries.Add(new CvStructuredEntryWriteDto(
                    null,
                    "Skills",
                    null,
                    null,
                    string.Empty,
                    inlineValues,
                    string.Empty,
                    CvEntrySources.Import,
                    null,
                    groupedEntries.Count));
            }
        }

        if (groupedEntries.Count > 0)
        {
            return groupedEntries;
        }

        var fallbackValues = text
            .Split(['\n', ',', ';', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where((value) => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (fallbackValues.Length == 0)
        {
            return [];
        }

        return
        [
            new CvStructuredEntryWriteDto(
                null,
                "Skills",
                null,
                null,
                string.Empty,
                fallbackValues,
                string.Empty,
                CvEntrySources.Import,
                null,
                0)
        ];
    }

    private static string[] SplitIntoEntryChunks(string[] lines)
    {
        if (lines.Length == 0)
        {
            return [];
        }

        if (lines.Length == 1)
        {
            return [lines[0]];
        }

        var chunks = new List<string>();
        var currentLines = new List<string>();

        for (var index = 0; index < lines.Length; index++)
        {
            if (currentLines.Count > 0
                && LooksLikeEntryStart(lines, index)
                && EntryLooksComplete(currentLines))
            {
                chunks.Add(string.Join('\n', currentLines));
                currentLines = [];
            }

            currentLines.Add(lines[index]);
        }

        if (currentLines.Count > 0)
        {
            chunks.Add(string.Join('\n', currentLines));
        }

        return chunks.ToArray();
    }

    private static bool LooksLikeEntryStart(string[] lines, int index)
    {
        var line = lines[index];

        if (IsBulletLine(line) || LooksLikeDateLine(line))
        {
            return false;
        }

        if (index + 1 >= lines.Length)
        {
            return false;
        }

        if (line.EndsWith('.') || line.EndsWith('!') || line.EndsWith('?'))
        {
            return false;
        }

        return true;
    }

    private static bool EntryLooksComplete(IReadOnlyList<string> lines) =>
        lines.Any(LooksLikeDateLine)
        || lines.Any(IsBulletLine)
        || lines.Count >= 3;

    private static CvStructuredEntryWriteDto ParseEntryChunk(string chunk, int sortOrder)
    {
        var lines = chunk
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToArray();

        if (lines.Length == 0)
        {
            return new CvStructuredEntryWriteDto(
                null,
                string.Empty,
                null,
                null,
                string.Empty,
                [],
                string.Empty,
                CvEntrySources.Import,
                null,
                sortOrder);
        }

        var title = lines[0];
        var index = 1;
        string? subtitle = null;
        string? dateRange = null;

        if (index < lines.Length && LooksLikeDateLine(lines[index]))
        {
            dateRange = lines[index];
            index++;
        }
        else if (index < lines.Length && lines[index].Length <= 80 && !IsBulletLine(lines[index]))
        {
            subtitle = lines[index];
            index++;

            if (index < lines.Length && LooksLikeDateLine(lines[index]))
            {
                dateRange = lines[index];
                index++;
            }
        }

        var bullets = new List<string>();
        var summaryLines = new List<string>();

        for (; index < lines.Length; index++)
        {
            if (IsBulletLine(lines[index]))
            {
                bullets.Add(TrimBullet(lines[index]));
            }
            else
            {
                summaryLines.Add(lines[index]);
            }
        }

        return new CvStructuredEntryWriteDto(
            null,
            title,
            subtitle,
            dateRange,
            string.Join(' ', summaryLines),
            bullets,
            string.Empty,
            CvEntrySources.Import,
            null,
            sortOrder);
    }

    internal static bool LooksLikeDateLine(string line) =>
        line.Contains("20", StringComparison.Ordinal)
        || line.Contains("Present", StringComparison.OrdinalIgnoreCase)
        || line.Contains('–')
        || line.Contains('-') && line.Any(char.IsDigit);

    internal static bool IsBulletLine(string line) =>
        line.StartsWith("•", StringComparison.Ordinal)
        || line.StartsWith("-", StringComparison.Ordinal)
        || line.StartsWith("*", StringComparison.Ordinal)
        || line.StartsWith("·", StringComparison.Ordinal);

    internal static string TrimBullet(string line) =>
        line.TrimStart('•', '-', '*', '·', ' ').Trim();
}
