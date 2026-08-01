using ApplyVault.Api.Services.CvSectionCatalog;

namespace ApplyVault.Api.Services;

/// <summary>
/// Conservative soft Custom heading promotion for Sectionize (unknown short headings).
/// </summary>
internal static class CvStructuredImportSoftHeading
{
    public const int MaxHeadingLength = 64;

    public static bool LooksLikePromotableHeading(
        string line,
        string currentNormalizedKey,
        ICvSectionCatalog sectionCatalog)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var trimmed = line.Trim();

        if (trimmed.Length > MaxHeadingLength)
        {
            return false;
        }

        if (sectionCatalog.TryMatchSectionHeading(trimmed, out _))
        {
            return false;
        }

        if (CvStructuredImportHeuristic.IsBulletLine(trimmed)
            || CvStructuredImportHeuristic.LooksLikeDateLine(trimmed)
            || CvStructuredImportHeuristic.LooksLikeLinkLine(trimmed)
            || CvStructuredImportEntrySupport.LooksLikeContactLine(trimmed)
            || LooksLikePersonName(trimmed))
        {
            return false;
        }

        // Prefer false-negative: do not promote inside dated career sections.
        var currentType = CvStructuredImportSectionTypeMapping.MapSectionType(currentNormalizedKey);
        if (currentType.Equals(CvSectionTypes.Experience, StringComparison.OrdinalIgnoreCase)
            || currentType.Equals(CvSectionTypes.Education, StringComparison.OrdinalIgnoreCase)
            || currentType.Equals(CvSectionTypes.Projects, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!LooksTitleCaseOrAllCaps(trimmed))
        {
            return false;
        }

        // Avoid promoting role-title-like lines that sit next to dates (caller may also check).
        if (LooksLikeRoleTitle(trimmed))
        {
            return false;
        }

        return true;
    }

    public static string ToNormalizedKey(string heading) =>
        CvPdfSectionDetector.NormalizeHeading(heading);

    private static bool LooksTitleCaseOrAllCaps(string line)
    {
        var letters = line.Where(char.IsLetter).ToArray();
        if (letters.Length < 2)
        {
            return false;
        }

        if (letters.All(char.IsUpper))
        {
            return true;
        }

        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length is 0 or > 8)
        {
            return false;
        }

        // Headings start with a capital; short 1–3 word labels like "Tech stack" count.
        if (!char.IsUpper(words[0][0]))
        {
            return false;
        }

        if (words.Length <= 3)
        {
            return true;
        }

        var titled = words.Count(static (word) => word.Length > 0 && char.IsUpper(word[0]));
        return titled >= (int)Math.Ceiling(words.Length * 0.6);
    }

    private static bool LooksLikePersonName(string line)
    {
        if (line.Any(char.IsDigit) || line.Contains('@', StringComparison.Ordinal))
        {
            return false;
        }

        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length is < 2 or > 4)
        {
            return false;
        }

        return words.All(static (word) =>
            word.Length > 0
            && char.IsUpper(word[0])
            && word.All(static (ch) => char.IsLetter(ch) || ch is '-' or '\''));
    }

    private static bool LooksLikeRoleTitle(string line)
    {
        // Common job-title fragments — soft promotion should not shred experience blocks
        // that leaked into Summary/Custom due to missing aliases.
        ReadOnlySpan<string> roleHints =
        [
            "engineer",
            "developer",
            "manager",
            "consultant",
            "architect",
            "analyst",
            "designer",
            "director",
            "lead",
            "intern",
            "specialist"
        ];

        foreach (var hint in roleHints)
        {
            if (line.Contains(hint, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
