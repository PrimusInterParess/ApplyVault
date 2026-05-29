using System.Text.RegularExpressions;

namespace ApplyVault.Api.Services.Jobnet;

internal static class JobDescriptionHeuristicRules
{
    private const int ShortLineThreshold = 30;
    private const int ScrapedLayoutLineCountThreshold = 8;
    private const double ScrapedLayoutShortLineRatioThreshold = 0.5;
    private const int NavTokenHitThreshold = 3;
    private const int StructuredHtmlMinimumLength = 120;

    private static readonly string[] NavTokens =
    [
        "principles",
        "inspiration",
        "services",
        "about us",
        "references",
        "contact",
        "contact us",
        "cookie",
        "cookies",
        "privacy",
        "login",
        "log in",
        "menu",
        "home",
        "careers",
        "footer"
    ];

    private static readonly Regex BlockHtmlPattern = new(
        @"<(p|ul|ol|li)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool ShouldUsePreviewOnly(JobnetDescriptionAssessmentRequest request)
    {
        var score = 0;

        if (request.Source == JobnetDescriptionSource.SearchFallback
            && JobnetJobIdentifiers.IsEuresImported(request.Id))
        {
            score += 2;
        }

        if (HasScrapedLayout(request.Description))
        {
            score += 2;
        }

        if (CountNavTokenHits(request.Description) >= NavTokenHitThreshold)
        {
            score += 2;
        }

        if (HasRepeatedTitleOrEmployerLines(request.Description, request.Title, request.Employer))
        {
            score += 1;
        }

        if (request.Source == JobnetDescriptionSource.NativeDetail && HasStructuredHtml(request.Description))
        {
            score -= 2;
        }

        return score >= 2;
    }

    public static bool IsNavLikeLine(string line, string? title, string? employer)
    {
        if (line.Length == 0)
        {
            return true;
        }

        if (line.Length > ShortLineThreshold)
        {
            return false;
        }

        var normalized = line.Trim().ToLowerInvariant();
        if (NavTokens.Contains(normalized))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(title)
            && string.Equals(normalized, title.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(employer)
            && string.Equals(normalized, employer.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool HasScrapedLayout(string? description)
    {
        var lines = SplitLines(description);
        if (lines.Count <= ScrapedLayoutLineCountThreshold)
        {
            return false;
        }

        var shortLines = lines.Count((line) => line.Length < ShortLineThreshold);
        return shortLines / (double)lines.Count >= ScrapedLayoutShortLineRatioThreshold;
    }

    private static int CountNavTokenHits(string? description)
    {
        return SplitLines(description)
            .Count((line) => NavTokens.Contains(line.Trim().ToLowerInvariant()));
    }

    private static bool HasRepeatedTitleOrEmployerLines(string? description, string? title, string? employer)
    {
        var lines = SplitLines(description);
        var titleHits = 0;
        var employerHits = 0;

        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(title)
                && string.Equals(line, title.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                titleHits++;
            }

            if (!string.IsNullOrWhiteSpace(employer)
                && string.Equals(line, employer.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                employerHits++;
            }
        }

        return titleHits >= 2 || employerHits >= 2;
    }

    private static bool HasStructuredHtml(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        return description.Trim().Length >= StructuredHtmlMinimumLength
            && BlockHtmlPattern.IsMatch(description);
    }

    private static IReadOnlyList<string> SplitLines(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return [];
        }

        return description
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select((line) => line.Trim())
            .Where((line) => line.Length > 0)
            .ToArray();
    }
}
