using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

/// <summary>
/// Drop Contact values that are not present in the extracted source text (AI hallucination guard).
/// Shape remapping stays in <see cref="CvStructuredImportEntrySupport.ReshapeContactEntryFields"/>.
/// </summary>
internal static class CvStructuredImportContactGrounding
{
    public static IReadOnlyList<CvStructuredSectionWriteDto> FilterToSource(
        IReadOnlyList<CvStructuredSectionWriteDto> sections,
        string extractedFullText)
    {
        if (sections.Count == 0 || string.IsNullOrWhiteSpace(extractedFullText))
        {
            return sections;
        }

        var haystack = NormalizeForMatch(extractedFullText);

        return sections
            .Select((section) =>
            {
                if (!section.SectionType.Equals(CvSectionTypes.Contact, StringComparison.OrdinalIgnoreCase)
                    && !section.Heading.Equals("Contact", StringComparison.OrdinalIgnoreCase))
                {
                    return section;
                }

                var entries = section.Entries
                    .Select((entry) => GroundEntry(entry, haystack))
                    .Where(ContactEntryHasGroundedValue)
                    .Select((entry, index) => entry with { SortOrder = index })
                    .ToList();

                if (!entries.Any(static (entry) =>
                        CvStructuredImportEntrySupport.IsContactNameTitle(entry.Title)
                        && !string.IsNullOrWhiteSpace(entry.Subtitle)))
                {
                    var recovered = TryRecoverNameFromSource(extractedFullText);
                    if (!string.IsNullOrWhiteSpace(recovered))
                    {
                        entries.Insert(
                            0,
                            new CvStructuredEntryWriteDto(
                                null,
                                "Name",
                                recovered,
                                null,
                                string.Empty,
                                [],
                                string.Empty,
                                CvEntrySources.Import,
                                null,
                                0));

                        for (var i = 0; i < entries.Count; i++)
                        {
                            entries[i] = entries[i] with { SortOrder = i };
                        }
                    }
                }

                return section with { Entries = entries };
            })
            .Where((section) =>
                !string.IsNullOrWhiteSpace(section.Heading) || section.Entries.Count > 0)
            .ToArray();
    }

    private static string? TryRecoverNameFromSource(string extractedFullText)
    {
        var lines = extractedFullText.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines.Take(12))
        {
            if (line.Equals("Contact", StringComparison.OrdinalIgnoreCase)
                || line.Equals("CONTACT", StringComparison.OrdinalIgnoreCase)
                || CvStructuredImportEntrySupport.LooksLikeContactLine(line)
                || CvStructuredImportEntrySupport.LooksLikeSectionHeadingAsName(line))
            {
                continue;
            }

            if (CvStructuredImportEntrySupport.LooksLikePlausiblePersonName(line))
            {
                return line;
            }
        }

        return null;
    }

    private static CvStructuredEntryWriteDto GroundEntry(
        CvStructuredEntryWriteDto entry,
        string haystack)
    {
        if (CvStructuredImportEntrySupport.IsContactNameTitle(entry.Title))
        {
            var name = entry.Subtitle?.Trim();
            if (string.IsNullOrWhiteSpace(name) || !ContainsLoose(haystack, name))
            {
                name = null;
            }

            // Legacy import: Name entry may still carry channel bullets — keep grounded ones.
            var groundedBullets = entry.Bullets
                .Where((bullet) =>
                    !string.IsNullOrWhiteSpace(bullet)
                    && ContainsLoose(haystack, bullet)
                    && CvStructuredImportEntrySupport.LooksLikeContactLine(bullet))
                .Select(static (bullet) => bullet.Trim())
                .ToArray();

            return entry with
            {
                Subtitle = name,
                Summary = string.Empty,
                Bullets = groundedBullets
            };
        }

        if (!CvStructuredImportEntrySupport.IsKnownContactChannelLabel(entry.Title))
        {
            return entry;
        }

        var groundedChannelBullets = entry.Bullets
            .Where((bullet) => !string.IsNullOrWhiteSpace(bullet) && ContainsLoose(haystack, bullet))
            .Select(static (bullet) => bullet.Trim())
            .ToArray();

        var summary = entry.Summary;
        if (!string.IsNullOrWhiteSpace(summary) && !ContainsLoose(haystack, summary))
        {
            summary = string.Empty;
        }

        if (groundedChannelBullets.Length == 0
            && !string.IsNullOrWhiteSpace(summary)
            && ContainsLoose(haystack, summary))
        {
            groundedChannelBullets = [summary.Trim()];
            summary = string.Empty;
        }

        return entry with
        {
            Summary = summary ?? string.Empty,
            Bullets = groundedChannelBullets
        };
    }

    private static bool ContactEntryHasGroundedValue(CvStructuredEntryWriteDto entry)
    {
        if (CvStructuredImportEntrySupport.IsContactNameTitle(entry.Title))
        {
            return !string.IsNullOrWhiteSpace(entry.Subtitle) || entry.Bullets.Count > 0;
        }

        if (CvStructuredImportEntrySupport.IsKnownContactChannelLabel(entry.Title))
        {
            return entry.Bullets.Any(static (b) => !string.IsNullOrWhiteSpace(b))
                || !string.IsNullOrWhiteSpace(entry.Summary);
        }

        return CvStructuredImportEntrySupport.EntryHasContent(entry);
    }

    internal static bool ContainsLoose(string haystackNormalized, string needle)
    {
        var normalizedNeedle = NormalizeForMatch(needle);
        if (normalizedNeedle.Length < 3)
        {
            return haystackNormalized.Contains(normalizedNeedle, StringComparison.Ordinal);
        }

        return haystackNormalized.Contains(normalizedNeedle, StringComparison.Ordinal);
    }

    internal static string NormalizeForMatch(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var cleaned = CvPdfFullTextExtractor.NormalizeExtractedText(value).ToLowerInvariant();
        var buffer = new char[cleaned.Length];
        var written = 0;

        foreach (var ch in cleaned)
        {
            if (char.IsLetterOrDigit(ch) || ch is '@' or '+' or '.' or '/' or ':' or '-')
            {
                buffer[written++] = ch;
            }
        }

        return new string(buffer, 0, written);
    }
}
