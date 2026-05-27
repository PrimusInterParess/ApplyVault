using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

internal static class CvStructuredImportHeuristic
{
    public static IReadOnlyList<CvStructuredSectionWriteDto> Parse(IReadOnlyList<CvPdfRawSection> rawSections)
    {
        return rawSections
            .Select((section, sectionIndex) => new CvStructuredSectionWriteDto(
                null,
                section.Heading,
                MapSectionType(section.NormalizedKey),
                sectionIndex,
                ParseEntries(section.Text, MapSectionType(section.NormalizedKey))))
            .Where((section) => section.Entries.Count > 0 || !string.IsNullOrWhiteSpace(section.Heading))
            .ToArray();
    }

    private static string MapSectionType(string normalizedKey) =>
        normalizedKey switch
        {
            "experience" or "employment" or "employment history" or "professional experience" or "work experience"
                => CvSectionTypes.Experience,
            "projects" or "personal projects" or "side projects" or "selected projects" => CvSectionTypes.Projects,
            "education" => CvSectionTypes.Education,
            "skills" or "technical skills" or "core competencies" => CvSectionTypes.Skills,
            "summary" or "profile" or "about me" => CvSectionTypes.Summary,
            _ => CvSectionTypes.Custom
        };

    private static IReadOnlyList<CvStructuredEntryWriteDto> ParseEntries(string text, string sectionType)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        if (sectionType == CvSectionTypes.Skills)
        {
            var skillLines = text
                .Split(['\n', ',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where((line) => !string.IsNullOrWhiteSpace(line))
                .ToArray();

            if (skillLines.Length == 0)
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
                    skillLines,
                    string.Empty,
                    CvEntrySources.Import,
                    null,
                    0)
            ];
        }

        var chunks = text.Split("\n\n", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        return chunks
            .Select((chunk, index) => ParseEntryChunk(chunk, index))
            .Where((entry) => !string.IsNullOrWhiteSpace(entry.Title) || !string.IsNullOrWhiteSpace(entry.Summary) || entry.Bullets.Count > 0)
            .ToArray();
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

    private static bool LooksLikeDateLine(string line) =>
        line.Contains("20", StringComparison.Ordinal)
        || line.Contains("Present", StringComparison.OrdinalIgnoreCase)
        || line.Contains("–")
        || line.Contains('-') && line.Any(char.IsDigit);

    private static bool IsBulletLine(string line) =>
        line.StartsWith("•", StringComparison.Ordinal)
        || line.StartsWith("-", StringComparison.Ordinal)
        || line.StartsWith("*", StringComparison.Ordinal)
        || line.StartsWith("·", StringComparison.Ordinal);

    private static string TrimBullet(string line) =>
        line.TrimStart('•', '-', '*', '·', ' ').Trim();
}
