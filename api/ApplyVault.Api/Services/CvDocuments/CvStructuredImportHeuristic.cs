using System.Text.RegularExpressions;
using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

internal static class CvStructuredImportHeuristic
{
    // Date lines are short calendar ranges — not prose that happens to contain hyphens/digits.
    private static readonly Regex DateLinePattern = new(
        @"^(?=.{1,48}$)(?=.*(?:\b(?:19|20)\d{2}\b|\bPresent\b)).+$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

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

        // Only treat a leading line as a person name when contact channels were also found.
        // Otherwise job titles like "Full-Stack Software Developer" get dropped from Summary.
        if (contactLines.Count == 0 && !string.IsNullOrWhiteSpace(nameLine))
        {
            remainingLines = new[] { nameLine }.Concat(remainingLines).ToArray();
            nameLine = null;
        }

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
        string? pendingGroupTitle = null;

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
                    groupedEntries.Add(CreateSkillsEntry(title, values, groupedEntries.Count));
                    pendingGroupTitle = null;
                    continue;
                }
            }

            // Two-line groups: "Backend" then "C#, ASP.NET Core, ..."
            if (!line.Contains(',') && !line.Contains(';') && line.Length <= 48)
            {
                pendingGroupTitle = line.Trim();
                continue;
            }

            var inlineValues = line
                .Split([',', ';', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where((value) => !string.IsNullOrWhiteSpace(value))
                .ToArray();

            if (inlineValues.Length >= 1)
            {
                groupedEntries.Add(CreateSkillsEntry(pendingGroupTitle ?? "Skills", inlineValues, groupedEntries.Count));
                pendingGroupTitle = null;
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

        return [CreateSkillsEntry("Skills", fallbackValues, 0)];
    }

    private static CvStructuredEntryWriteDto CreateSkillsEntry(
        string title,
        IReadOnlyList<string> values,
        int sortOrder) =>
        new(
            null,
            title,
            null,
            null,
            string.Empty,
            [],
            string.Join(", ", values),
            CvEntrySources.Import,
            null,
            sortOrder);

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

        // Prefer date-anchored splits (Experience/Education). Avoid the old "3 lines = done"
        // rule that shredded multi-line job descriptions into fake entries.
        var datedStarts = FindDatedEntryStarts(lines);
        if (datedStarts.Count > 0)
        {
            if (datedStarts[0] > 0)
            {
                datedStarts[0] = 0;
            }

            return ChunkByStarts(lines, datedStarts);
        }

        return SplitUndatedEntryChunks(lines);
    }

    private static List<int> FindDatedEntryStarts(string[] lines)
    {
        var starts = new List<int>();

        for (var dateIndex = 0; dateIndex < lines.Length; dateIndex++)
        {
            if (!LooksLikeDateLine(lines[dateIndex]))
            {
                continue;
            }

            var start = dateIndex;

            if (dateIndex >= 1 && !LooksLikeDateLine(lines[dateIndex - 1]))
            {
                start = dateIndex - 1;

                // Title + company/subtitle + date
                if (start >= 1
                    && !LooksLikeDateLine(lines[start - 1])
                    && LooksLikeRoleTitleLine(lines[start - 1])
                    && LooksLikeCompanyOrSubtitleLine(lines[start]))
                {
                    start--;
                }
            }

            if (starts.Count == 0 || starts[^1] != start)
            {
                starts.Add(start);
            }
        }

        return starts;
    }

    private static string[] SplitUndatedEntryChunks(string[] lines)
    {
        var starts = new List<int> { 0 };

        for (var index = 1; index < lines.Length; index++)
        {
            if (!LooksLikeStandaloneTitleLine(lines[index]))
            {
                continue;
            }

            if (!UndatedEntryLooksComplete(lines, starts[^1], index))
            {
                continue;
            }

            starts.Add(index);
        }

        return ChunkByStarts(lines, starts);
    }

    private static string[] ChunkByStarts(string[] lines, IReadOnlyList<int> starts)
    {
        var chunks = new List<string>(starts.Count);

        for (var i = 0; i < starts.Count; i++)
        {
            var start = starts[i];
            var end = i + 1 < starts.Count ? starts[i + 1] : lines.Length;
            chunks.Add(string.Join('\n', lines[start..end]));
        }

        return chunks.ToArray();
    }

    private static bool LooksLikeRoleTitleLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || LooksLikeDateLine(line) || IsBulletLine(line) || LooksLikeLinkLine(line))
        {
            return false;
        }

        if (LooksLikeTechStackLine(line) || line.Length > 100)
        {
            return false;
        }

        return line.Contains('|', StringComparison.Ordinal)
            || (!line.EndsWith('.') && line.Length <= 80);
    }

    private static bool LooksLikeCompanyOrSubtitleLine(string line) =>
        !string.IsNullOrWhiteSpace(line)
        && !LooksLikeDateLine(line)
        && !IsBulletLine(line)
        && !LooksLikeLinkLine(line)
        && !LooksLikeTechStackLine(line)
        && line.Length <= 80
        && !line.Contains('|', StringComparison.Ordinal);

    private static bool LooksLikeStandaloneTitleLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)
            || IsBulletLine(line)
            || LooksLikeDateLine(line)
            || LooksLikeLinkLine(line)
            || LooksLikeTechStackLine(line))
        {
            return false;
        }

        if (line.Length > 60)
        {
            return false;
        }

        if (line.EndsWith('.') || line.EndsWith('!') || line.EndsWith('?'))
        {
            return false;
        }

        var colonIndex = line.IndexOf(':');
        if (colonIndex > 0 && colonIndex < line.Length - 1)
        {
            var afterColon = line[(colonIndex + 1)..].Trim();
            if (afterColon.Length > 30)
            {
                return false;
            }
        }

        return true;
    }

    private static bool UndatedEntryLooksComplete(string[] lines, int start, int nextStart)
    {
        if (nextStart - start < 2)
        {
            return false;
        }

        for (var i = start; i < nextStart; i++)
        {
            if (LooksLikeTechStackLine(lines[i]) || LooksLikeLinkLine(lines[i]))
            {
                return true;
            }
        }

        // Title + description is enough when the next line looks like a new project title.
        return nextStart - start >= 2;
    }

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
        string techStack = string.Empty;

        if (title.Contains(" | ", StringComparison.Ordinal))
        {
            var parts = title.Split(" | ", 2, StringSplitOptions.TrimEntries);
            title = parts[0];
            subtitle = parts.Length > 1 ? parts[1] : null;
        }

        if (index < lines.Length && LooksLikeDateLine(lines[index]))
        {
            dateRange = lines[index];
            index++;
        }
        else if (index < lines.Length
            && lines[index].Length <= 80
            && !IsBulletLine(lines[index])
            && !LooksLikeTechStackLine(lines[index])
            && !LooksLikeLinkLine(lines[index]))
        {
            // Only use as subtitle when the following line is a date (Title / Company / Date),
            // otherwise project descriptions and tech lines stay in summary/techStack.
            if (index + 1 < lines.Length && LooksLikeDateLine(lines[index + 1]))
            {
                subtitle ??= lines[index];
                index++;
                dateRange = lines[index];
                index++;
            }
        }

        var bullets = new List<string>();
        var summaryLines = new List<string>();

        for (; index < lines.Length; index++)
        {
            var line = lines[index];

            if (IsBulletLine(line))
            {
                bullets.Add(TrimBullet(line));
            }
            else if (LooksLikeTechStackLine(line))
            {
                techStack = ExtractTechStackValue(line);
            }
            else
            {
                summaryLines.Add(line);
            }
        }

        return new CvStructuredEntryWriteDto(
            null,
            title,
            subtitle,
            dateRange,
            string.Join(' ', summaryLines),
            bullets,
            techStack,
            CvEntrySources.Import,
            null,
            sortOrder);
    }

    internal static bool LooksLikeDateLine(string line) =>
        !string.IsNullOrWhiteSpace(line) && DateLinePattern.IsMatch(line.Trim());

    internal static bool LooksLikeTechStackLine(string line)
    {
        var trimmed = line.Trim();
        return trimmed.StartsWith("Technologies:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Technology:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Tech stack:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Tech:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Stack:", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool LooksLikeLinkLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var trimmed = line.Trim();
        return trimmed.Contains("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("https://", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("www.", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("github.com/", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("linkedin.com/", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("github.com/", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("linkedin.com/", StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractTechStackValue(string line)
    {
        var colonIndex = line.IndexOf(':');
        return colonIndex >= 0 && colonIndex < line.Length - 1
            ? line[(colonIndex + 1)..].Trim()
            : line.Trim();
    }

    internal static bool IsBulletLine(string line) =>
        line.StartsWith("•", StringComparison.Ordinal)
        || line.StartsWith("-", StringComparison.Ordinal)
        || line.StartsWith("*", StringComparison.Ordinal)
        || line.StartsWith("·", StringComparison.Ordinal);

    internal static string TrimBullet(string line) =>
        line.TrimStart('•', '-', '*', '·', ' ').Trim();
}
