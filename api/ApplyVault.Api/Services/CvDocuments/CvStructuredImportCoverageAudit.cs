using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

/// <summary>
/// Internal diagnostics for source-line coverage. Not used for user-facing import notices
/// (substring matching produced noisy false positives).
/// </summary>
internal static class CvStructuredImportCoverageAudit
{
    private const int MinimumLineLength = 12;

    public static int CountMissingSourceLines(
        IReadOnlyList<CvPdfRawSection> rawSections,
        IReadOnlyList<CvStructuredSectionWriteDto> sections) =>
        CollectMissingSourceLines(rawSections.SelectMany((section) =>
                section.Text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)),
            sections).Count;

    public static IReadOnlyList<string> CollectMissingSourceLines(
        IEnumerable<string> sourceLines,
        IReadOnlyList<CvStructuredSectionWriteDto> sections)
    {
        var structuredText = CollectStructuredText(sections);
        var missing = new List<string>();

        foreach (var line in sourceLines)
        {
            var trimmed = line.Trim();

            if (trimmed.Length == 0 || ShouldSkipSourceLine(trimmed, string.Empty))
            {
                continue;
            }

            if (!IsLineRepresented(trimmed, structuredText))
            {
                missing.Add(trimmed);
            }
        }

        return missing;
    }

    private static bool ShouldSkipSourceLine(string line, string heading)
    {
        var normalizedLine = CvExportTextNormalizer.Field(line);
        var normalizedHeading = CvPdfSectionDetector.NormalizeHeading(heading);

        if (normalizedLine.Length < MinimumLineLength)
        {
            return true;
        }

        if (normalizedLine.Equals(normalizedHeading, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return CvPdfSectionDetector.TryMatchSectionHeading(line, out _);
    }

    private static bool IsLineRepresented(string line, string structuredText)
    {
        var normalizedLine = CvExportTextNormalizer.Field(line);

        if (normalizedLine.Length == 0)
        {
            return true;
        }

        if (structuredText.Contains(normalizedLine, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalizedLine.Length >= 20)
        {
            var prefix = normalizedLine[..Math.Min(20, normalizedLine.Length)];

            return structuredText.Contains(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static string CollectStructuredText(IReadOnlyList<CvStructuredSectionWriteDto> sections)
    {
        var parts = new List<string>();

        foreach (var section in sections)
        {
            parts.Add(section.Heading);

            foreach (var entry in section.Entries)
            {
                parts.Add(entry.Title);

                if (!string.IsNullOrWhiteSpace(entry.Subtitle))
                {
                    parts.Add(entry.Subtitle);
                }

                if (!string.IsNullOrWhiteSpace(entry.DateRange))
                {
                    parts.Add(entry.DateRange);
                }

                parts.Add(entry.Summary);
                parts.Add(entry.TechStack);
                parts.AddRange(entry.Bullets);
            }
        }

        return CvExportTextNormalizer.Field(string.Join(' ', parts));
    }
}
