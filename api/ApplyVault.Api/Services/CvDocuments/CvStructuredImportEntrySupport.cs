using ApplyVault.Api.Models;
using System.Text.RegularExpressions;

namespace ApplyVault.Api.Services;

/// <summary>
/// Shared helpers for heuristic Contact parsing, entry content checks, and post-AI Contact reshape.
/// AI prompts request Contact shape; the normalizer still remaps misfiled summary → subtitle/bullets.
/// </summary>
internal static class CvStructuredImportEntrySupport
{
    private static readonly Regex PhonePattern = new(
        @"\+?\d[\d\s().\-]{6,}\d",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PostalCodePattern = new(
        @"\b\d{4}\s+[A-Za-zÀ-ÿ]",
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
            || trimmed.StartsWith("mob:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("mobile:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("tlf:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("tlf.", StringComparison.OrdinalIgnoreCase)
            || PostalCodePattern.IsMatch(trimmed)
            || LooksLikeLocationLine(trimmed)
            || IsContactLabelLine(trimmed)
            || HasAddressOrLocationLabel(trimmed);
    }

    public static IReadOnlyList<string> SplitContactTokens(string line) =>
        CvImportLinkIntegrity.SplitContactTokens(line);

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
                // Keep street/city addresses atomic — never comma-split them into contact tokens.
                if (LooksLikeLocationLine(trimmed)
                    && (contactLines.Count > 0
                        || nameLine is not null
                        || HasAddressOrLocationLabel(trimmed)))
                {
                    contactLines.Add(CvExportTextNormalizer.Field(trimmed));
                    continue;
                }

                if (LooksLikeContactLine(trimmed) && !LooksLikeLocationLine(trimmed))
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

        var hasName = !string.IsNullOrWhiteSpace(nameLine);

        return new CvStructuredSectionWriteDto(
            null,
            "Contact",
            CvSectionTypes.Contact,
            sortOrder,
            [
                new CvStructuredEntryWriteDto(
                    null,
                    hasName ? "Name" : string.Empty,
                    hasName ? nameLine!.Trim() : null,
                    null,
                    string.Empty,
                    bullets,
                    string.Empty,
                    CvEntrySources.Import,
                    null,
                    0)
            ]);
    }

    /// <summary>
    /// Street / city / postal address lines (kept atomic — never comma-split).
    /// </summary>
    public static bool LooksLikeLocationLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Length > 120)
        {
            return false;
        }

        var trimmed = line.Trim();

        if (CvStructuredImportHeuristic.LooksLikeDateLine(trimmed)
            || trimmed.Contains('@', StringComparison.Ordinal)
            || CvImportLinkIntegrity.LooksLikeUrlLine(trimmed)
            || PhonePattern.IsMatch(trimmed) && !HasAddressOrLocationLabel(trimmed) && !PostalCodePattern.IsMatch(trimmed))
        {
            return false;
        }

        if (HasAddressOrLocationLabel(trimmed))
        {
            return true;
        }

        if (PostalCodePattern.IsMatch(trimmed))
        {
            return true;
        }

        if (trimmed.Contains(',', StringComparison.Ordinal)
            && trimmed.Any(char.IsDigit)
            && trimmed.Count(char.IsLetter) >= 4)
        {
            return true;
        }

        var parts = trimmed.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2
            && parts.All(static (part) =>
                part.Length >= 2
                && part.Count(char.IsLetter) >= 2
                && part.All(static (ch) => char.IsLetter(ch) || ch is ' ' or '-' or '\'')))
        {
            return true;
        }

        return false;
    }

    public static bool HasAddressOrLocationLabel(string line)
    {
        ReadOnlySpan<string> labels =
        [
            "address",
            "addr",
            "location",
            "lokation",
            "bopæl",
            "bopael",
            "residence"
        ];

        var trimmed = line.Trim();

        foreach (var label in labels)
        {
            if (trimmed.Equals(label, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (trimmed.StartsWith(label + ":", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith(label + " ", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

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

    public static bool IsContactNameTitle(string? title) =>
        !string.IsNullOrWhiteSpace(title)
        && title.Trim().Equals("name", StringComparison.OrdinalIgnoreCase);

    public static bool IsKnownContactChannelLabel(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        return title.Trim().ToLowerInvariant() switch
        {
            "email" or "e-mail" or "phone" or "mobile" or "tel" or "telephone"
                or "linkedin" or "github" or "location" or "address"
                or "website" or "web" or "url" => true,
            _ => false
        };
    }

    /// <summary>
    /// Section headings the model sometimes misfiles as Contact Name (e.g. "SUMMARY").
    /// </summary>
    public static bool LooksLikeSectionHeadingAsName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > 48 || trimmed.Contains('@', StringComparison.Ordinal) || trimmed.Any(char.IsDigit))
        {
            return false;
        }

        var normalized = trimmed.TrimEnd(':').ToLowerInvariant();

        return normalized is "summary" or "profile" or "about" or "about me" or "objective"
            or "experience" or "education" or "skills" or "projects" or "contact"
            or "contact information" or "languages" or "interests" or "personal interests";
    }

    /// <summary>
    /// Reject sentence fragments the model invents when the PDF text has no person name
    /// (e.g. "A mother of three" from Personal Interests).
    /// </summary>
    public static bool LooksLikePlausiblePersonName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || LooksLikeSectionHeadingAsName(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        if (trimmed.Length > 64
            || trimmed.Contains('@', StringComparison.Ordinal)
            || trimmed.Any(char.IsDigit)
            || LooksLikeContactLine(trimmed))
        {
            return false;
        }

        var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (words.Length is < 1 or > 5)
        {
            return false;
        }

        // Require Title Case / all-caps tokens (allow particles like van/de/da/von).
        ReadOnlySpan<string> particles = ["van", "von", "de", "da", "del", "della", "di", "la", "le", "du"];
        ReadOnlySpan<string> jobTokens =
        [
            "developer", "engineer", "manager", "designer", "analyst", "consultant",
            "specialist", "architect", "intern", "student", "director", "officer",
            "lead", "senior", "junior", "full-stack", "fullstack", "software",
            "frontend", "backend", "devops", "founder", "ceo", "cto", "coo"
        ];

        for (var i = 0; i < words.Length; i++)
        {
            var word = words[i];
            var lower = word.ToLowerInvariant();

            foreach (var job in jobTokens)
            {
                if (lower.Equals(job, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            var isParticle = false;

            foreach (var particle in particles)
            {
                if (word.Equals(particle, StringComparison.OrdinalIgnoreCase))
                {
                    isParticle = true;
                    break;
                }
            }

            if (isParticle && i > 0)
            {
                continue;
            }

            if (word.Length == 0 || !char.IsLetter(word[0]))
            {
                return false;
            }

            // Reject lowercase-leading tokens ("mother", "of", "three").
            if (char.IsLower(word[0]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gemini often puts Name in summary and channel values in summary instead of
    /// subtitle / bullets. Reshape to the wire shape FE + export expect.
    /// </summary>
    public static (string? Subtitle, string Summary, IReadOnlyList<string> Bullets)
        ReshapeContactEntryFields(
            string title,
            string? subtitle,
            string summary,
            IReadOnlyList<string> bullets)
    {
        var valuedBullets = bullets
            .Where(static (line) => !string.IsNullOrWhiteSpace(line))
            .Select(static (line) => line.Trim())
            .ToArray();

        if (IsContactNameTitle(title))
        {
            var nameCandidate = !string.IsNullOrWhiteSpace(subtitle)
                ? subtitle.Trim()
                : !string.IsNullOrWhiteSpace(summary)
                    ? summary.Trim()
                    : valuedBullets.FirstOrDefault();

            if (LooksLikeSectionHeadingAsName(nameCandidate)
                || !LooksLikePlausiblePersonName(nameCandidate))
            {
                // Drop fake name; leave any non-prose channel bullets (phones/emails) if present.
                var channelLikeBullets = valuedBullets
                    .Where(static (line) =>
                        !LooksLikeSectionHeadingAsName(line)
                        && (line.Contains('@', StringComparison.Ordinal)
                            || LooksLikeContactLine(line)))
                    .ToArray();

                return (null, string.Empty, channelLikeBullets);
            }

            if (!string.IsNullOrWhiteSpace(nameCandidate))
            {
                // If the name was promoted from the first bullet, drop that bullet only.
                // Keep remaining channel bullets (heuristic legacy Name+bullets shape).
                IReadOnlyList<string> leftoverBullets;
                if (string.IsNullOrWhiteSpace(subtitle)
                    && string.IsNullOrWhiteSpace(summary)
                    && valuedBullets.Length > 0
                    && valuedBullets[0].Equals(nameCandidate, StringComparison.OrdinalIgnoreCase))
                {
                    leftoverBullets = valuedBullets.Skip(1).ToArray();
                }
                else if (!string.IsNullOrWhiteSpace(subtitle) || !string.IsNullOrWhiteSpace(summary))
                {
                    // Name came from subtitle/summary — keep all channel bullets.
                    leftoverBullets = valuedBullets;
                }
                else
                {
                    leftoverBullets = valuedBullets;
                }

                return (nameCandidate, string.Empty, leftoverBullets);
            }

            return (null, string.Empty, valuedBullets);
        }

        if (IsKnownContactChannelLabel(title))
        {
            if (valuedBullets.Length == 0 && !string.IsNullOrWhiteSpace(summary))
            {
                return (subtitle, string.Empty, [summary.Trim()]);
            }

            if (valuedBullets.Length > 0)
            {
                var deduped = valuedBullets
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                // Prefer bullets; drop summary duplicate / leftover from Gemini.
                return (subtitle, string.Empty, deduped);
            }
        }

        return (subtitle, summary, valuedBullets.Length > 0 ? valuedBullets : bullets);
    }
}
