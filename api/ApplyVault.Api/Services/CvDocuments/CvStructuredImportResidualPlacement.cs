using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

/// <summary>
/// P1 residual placement: park unplaced source lines into Custom sections (never silent drop).
/// </summary>
internal static class CvStructuredImportResidualPlacement
{
    public const string CatchAllHeading = "Additional information";
    public const int DefaultMinResidualLength = 10;

    public sealed record Result(
        IReadOnlyList<CvStructuredSectionWriteDto> Sections,
        int ResidualLineCountBeforeSpill,
        int ConsideredSourceLineCount,
        bool UsedCatchAll);

    public static Result Apply(
        IReadOnlyList<CvStructuredSectionWriteDto> sections,
        IReadOnlyList<CvPdfRawSection> rawSections,
        int minResidualLength = DefaultMinResidualLength)
    {
        if (rawSections.Count == 0)
        {
            return new Result(sections, 0, 0, UsedCatchAll: false);
        }

        var structuredText = CollectStructuredText(sections);
        var residualsByHeading = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var catchAllLines = new List<string>();
        var residualCount = 0;
        var consideredCount = 0;

        foreach (var rawSection in rawSections)
        {
            var heading = rawSection.Heading.Trim();
            var mappedType = CvStructuredImportSectionTypeMapping.MapSectionType(rawSection.NormalizedKey);
            var preferSectionHeading = mappedType.Equals(CvSectionTypes.Custom, StringComparison.OrdinalIgnoreCase)
                && !heading.Equals("Profile", StringComparison.OrdinalIgnoreCase)
                && !heading.Equals("Summary", StringComparison.OrdinalIgnoreCase);

            foreach (var line in rawSection.Text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (ShouldSkipSourceLine(line, heading, minResidualLength))
                {
                    continue;
                }

                consideredCount++;

                if (IsLineRepresented(line, structuredText))
                {
                    continue;
                }

                residualCount++;

                if (preferSectionHeading
                    && HasCustomSectionWithHeading(sections, heading))
                {
                    AddResidual(residualsByHeading, heading, line);
                }
                else if (preferSectionHeading
                         && !heading.Equals(CatchAllHeading, StringComparison.OrdinalIgnoreCase))
                {
                    // Soft-promoted / unmatched Custom heading without a structured section yet.
                    AddResidual(residualsByHeading, heading, line);
                }
                else
                {
                    catchAllLines.Add(CvExportTextNormalizer.Field(line));
                }
            }
        }

        if (residualCount == 0)
        {
            return new Result(sections, 0, consideredCount, UsedCatchAll: false);
        }

        var updated = sections.ToList();
        var usedCatchAll = false;

        foreach (var (heading, lines) in residualsByHeading)
        {
            var distinctLines = lines
                .Select(CvExportTextNormalizer.Field)
                .Where((line) => line.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (distinctLines.Length == 0)
            {
                continue;
            }

            AppendOrCreateCustomSection(updated, heading, distinctLines);
        }

        var catchAllDistinct = catchAllLines
            .Select(CvExportTextNormalizer.Field)
            .Where((line) => line.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (catchAllDistinct.Length > 0)
        {
            usedCatchAll = true;
            AppendOrCreateCustomSection(updated, CatchAllHeading, catchAllDistinct);
        }

        var finalSections = updated
            .Select((section, index) => section with
            {
                SortOrder = index,
                Entries = section.Entries
                    .Select((entry, entryIndex) => entry with { SortOrder = entryIndex })
                    .ToArray()
            })
            .ToArray();

        return new Result(finalSections, residualCount, consideredCount, usedCatchAll);
    }

    private static void AddResidual(Dictionary<string, List<string>> map, string heading, string line)
    {
        if (!map.TryGetValue(heading, out var list))
        {
            list = [];
            map[heading] = list;
        }

        list.Add(line);
    }

    private static bool HasCustomSectionWithHeading(
        IReadOnlyList<CvStructuredSectionWriteDto> sections,
        string heading) =>
        sections.Any((section) =>
            section.SectionType.Equals(CvSectionTypes.Custom, StringComparison.OrdinalIgnoreCase)
            && section.Heading.Equals(heading, StringComparison.OrdinalIgnoreCase));

    private static void AppendOrCreateCustomSection(
        List<CvStructuredSectionWriteDto> sections,
        string heading,
        IReadOnlyList<string> residualLines)
    {
        var index = sections.FindIndex((section) =>
            section.SectionType.Equals(CvSectionTypes.Custom, StringComparison.OrdinalIgnoreCase)
            && section.Heading.Equals(heading, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            var existing = sections[index];
            var first = existing.Entries.FirstOrDefault();
            var mergedBullets = (first?.Bullets ?? [])
                .Concat(residualLines)
                .Select(CvExportTextNormalizer.Field)
                .Where((line) => line.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var updatedEntry = (first ?? CreateResidualEntry(residualLines, 0)) with
            {
                Bullets = mergedBullets.Length > 0 ? mergedBullets : residualLines.ToArray(),
                Summary = first?.Summary ?? string.Empty
            };

            // If the entry had no bullets and residual is the only content, prefer bullets.
            if (string.IsNullOrWhiteSpace(updatedEntry.Summary)
                && updatedEntry.Bullets.Count == 0)
            {
                updatedEntry = updatedEntry with { Bullets = residualLines.ToArray() };
            }
            else if (updatedEntry.Bullets.Count == 0)
            {
                updatedEntry = updatedEntry with
                {
                    Bullets = residualLines.ToArray()
                };
            }

            var entries = existing.Entries.Count == 0
                ? new[] { updatedEntry }
                : existing.Entries
                    .Select((entry, entryIndex) => entryIndex == 0 ? updatedEntry : entry)
                    .ToArray();

            sections[index] = existing with { Entries = entries };
            return;
        }

        sections.Add(new CvStructuredSectionWriteDto(
            null,
            heading,
            CvSectionTypes.Custom,
            sections.Count,
            [CreateResidualEntry(residualLines, 0)]));
    }

    private static CvStructuredEntryWriteDto CreateResidualEntry(IReadOnlyList<string> lines, int sortOrder) =>
        new(
            null,
            string.Empty,
            null,
            null,
            string.Empty,
            lines.ToArray(),
            string.Empty,
            CvEntrySources.Import,
            null,
            sortOrder);

    private static bool ShouldSkipSourceLine(string line, string heading, int minResidualLength)
    {
        var normalizedLine = CvExportTextNormalizer.Field(line);
        var normalizedHeading = CvPdfSectionDetector.NormalizeHeading(heading);

        if (normalizedLine.Length < minResidualLength)
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
            if (structuredText.Contains(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Contact pipes: "a | b" is represented when each token is present.
        if (CvStructuredImportEntrySupport.LooksLikeContactLine(normalizedLine))
        {
            var tokens = CvStructuredImportEntrySupport.SplitContactTokens(normalizedLine);
            if (tokens.Count > 0
                && tokens.All((token) =>
                    structuredText.Contains(CvExportTextNormalizer.Field(token), StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        // Skills-style "Label: values" → title + techStack/summary often omit the colon form.
        var colonIndex = normalizedLine.IndexOf(':');
        if (colonIndex > 0 && colonIndex < normalizedLine.Length - 1)
        {
            var label = normalizedLine[..colonIndex].Trim();
            var values = normalizedLine[(colonIndex + 1)..].Trim();
            if (label.Length > 0
                && values.Length > 0
                && structuredText.Contains(label, StringComparison.OrdinalIgnoreCase)
                && values
                    .Split([',', ';', '|'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .All((value) => structuredText.Contains(value, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
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
