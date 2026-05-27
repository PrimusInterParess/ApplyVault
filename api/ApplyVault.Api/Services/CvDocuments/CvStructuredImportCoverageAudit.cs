using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

internal static class CvStructuredImportCoverageAudit
{
    private const int MinimumLineLength = 12;

    public static string? BuildNotice(
        IReadOnlyList<CvPdfRawSection> rawSections,
        IReadOnlyList<CvStructuredSectionWriteDto> sections,
        string? existingNotice)
    {
        var missingLineCount = CountMissingSourceLines(rawSections, sections);

        if (missingLineCount <= 0)
        {
            return existingNotice;
        }

        var coverageNotice =
            $"{missingLineCount} line{(missingLineCount == 1 ? string.Empty : "s")} from the PDF may not have been imported. Review Contact and Custom sections.";

        return string.IsNullOrWhiteSpace(existingNotice)
            ? coverageNotice
            : $"{existingNotice} {coverageNotice}";
    }

    private static int CountMissingSourceLines(
        IReadOnlyList<CvPdfRawSection> rawSections,
        IReadOnlyList<CvStructuredSectionWriteDto> sections)
    {
        var structuredText = CollectStructuredText(sections);
        var missingCount = 0;

        foreach (var rawSection in rawSections)
        {
            foreach (var line in rawSection.Text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (ShouldSkipSourceLine(line, rawSection.Heading))
                {
                    continue;
                }

                if (!IsLineRepresented(line, structuredText))
                {
                    missingCount++;
                }
            }
        }

        return missingCount;
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
