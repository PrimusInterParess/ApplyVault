using System.Text.RegularExpressions;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services.CvSectionCatalog;

namespace ApplyVault.Api.Services;

/// <summary>
/// Fallback parser when Gemini is off or fails: Contact channels only; every other line is Unplaced.
/// </summary>
internal static class ContactOnlyCvParser
{
    private static readonly Regex EmailRegex = new(
        @"[A-Z0-9._%+\-]+@[A-Z0-9.\-]+\.[A-Z]{2,}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UrlRegex = new(
        @"https?://[^\s]+|www\.[^\s]+|linkedin\.com/[^\s]+|github\.com/[^\s]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PhoneRegex = new(
        @"(?:\+\d{1,3}[\s.\-]?)?(?:\(?\d{2,4}\)?[\s.\-]?){2,4}\d{2,4}",
        RegexOptions.Compiled);

    public static (IReadOnlyList<CvStructuredSectionWriteDto> Sections, IReadOnlyList<string> Unplaced) Parse(
        IReadOnlyList<string> lines,
        ICvSectionCatalog catalog)
    {
        string? name = null;
        string? email = null;
        string? phone = null;
        var links = new List<string>();
        var unplaced = new List<string>();

        foreach (var raw in lines)
        {
            var line = raw.Trim();

            if (line.Length == 0)
            {
                continue;
            }

            var emailMatch = EmailRegex.Match(line);
            if (emailMatch.Success && email is null)
            {
                email = emailMatch.Value.Trim();
                var remainder = EmailRegex.Replace(line, string.Empty).Trim(' ', '|', ',', ';', '·', '-');
                if (remainder.Length > 0)
                {
                    AbsorbRemainder(remainder, ref phone, links, unplaced);
                }

                continue;
            }

            var urlMatch = UrlRegex.Match(line);
            if (urlMatch.Success)
            {
                links.Add(urlMatch.Value.Trim().TrimEnd('.', ',', ';'));
                continue;
            }

            if (LooksLikePhone(line) && phone is null)
            {
                phone = line;
                continue;
            }

            if (name is null && LooksLikeName(line))
            {
                name = line;
                continue;
            }

            unplaced.Add(line);
        }

        var contactEntries = new List<CvStructuredEntryWriteDto>();
        var sort = 0;
        contactEntries.Add(Channel("Name", name, sort++));
        contactEntries.Add(Channel("Email", email, sort++));
        contactEntries.Add(Channel("Phone", phone, sort++));

        foreach (var link in links)
        {
            var title = link.Contains("linkedin", StringComparison.OrdinalIgnoreCase)
                ? "LinkedIn"
                : link.Contains("github", StringComparison.OrdinalIgnoreCase)
                    ? "GitHub"
                    : "Website";
            contactEntries.Add(Channel(title, link, sort++));
        }

        var sections = new[]
        {
            new CvStructuredSectionWriteDto(
                null,
                "Contact",
                CvSectionTypes.Contact,
                0,
                contactEntries)
        };

        return (FixedSectionLayout.Normalize(sections, catalog), unplaced);
    }

    private static void AbsorbRemainder(
        string remainder,
        ref string? phone,
        List<string> links,
        List<string> unplaced)
    {
        if (UrlRegex.IsMatch(remainder))
        {
            links.Add(UrlRegex.Match(remainder).Value.Trim().TrimEnd('.', ',', ';'));
            return;
        }

        if (LooksLikePhone(remainder) && phone is null)
        {
            phone = remainder;
            return;
        }

        unplaced.Add(remainder);
    }

    private static bool LooksLikePhone(string line)
    {
        var digits = line.Count(char.IsDigit);
        return digits >= 8 && PhoneRegex.IsMatch(line) && !line.Contains('@', StringComparison.Ordinal);
    }

    private static bool LooksLikeName(string line) =>
        line.Length is >= 2 and <= 60
        && line.Count(char.IsLetter) >= 2
        && !line.Contains('@', StringComparison.Ordinal)
        && !UrlRegex.IsMatch(line)
        && line.Count(char.IsDigit) < 3;

    private static CvStructuredEntryWriteDto Channel(string title, string? value, int sortOrder) =>
        new(
            null,
            title,
            title.Equals("Name", StringComparison.OrdinalIgnoreCase) ? value : null,
            null,
            string.Empty,
            title.Equals("Name", StringComparison.OrdinalIgnoreCase)
                ? []
                : [value ?? string.Empty],
            string.Empty,
            CvEntrySources.Import,
            null,
            sortOrder);
}
