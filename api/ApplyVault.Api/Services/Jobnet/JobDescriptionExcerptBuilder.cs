using System.Net;
using System.Text.RegularExpressions;
using ApplyVault.Api.Services.Shared;

namespace ApplyVault.Api.Services.Jobnet;

internal static class JobDescriptionExcerptBuilder
{
    private const int MaxExcerptLength = 240;

    private static readonly Regex HtmlTagPattern = new(
        "<[^>]+>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string? Build(string? description, string? title, string? employer)
    {
        var plainText = ToPlainText(description);
        var lines = SplitLines(plainText);

        foreach (var line in lines)
        {
            if (JobDescriptionHeuristicRules.IsNavLikeLine(line, title, employer))
            {
                continue;
            }

            if (line.Length >= 20)
            {
                return TruncateAtWordBoundary(line);
            }
        }

        foreach (var line in lines)
        {
            if (!JobDescriptionHeuristicRules.IsNavLikeLine(line, title, employer) && line.Length > 0)
            {
                return TruncateAtWordBoundary(line);
            }
        }

        var fallback = string.Join(' ', lines.Where((line) => line.Length > 0));
        return string.IsNullOrWhiteSpace(fallback) ? null : TruncateAtWordBoundary(fallback);
    }

    private static string ToPlainText(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        var withoutTags = HtmlTagPattern.Replace(description, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return Regex.Replace(decoded, @"[ \t]+", " ").Trim();
    }

    private static IReadOnlyList<string> SplitLines(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return [];
        }

        return plainText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select((line) => line.Trim())
            .Where((line) => line.Length > 0)
            .ToArray();
    }

    private static string TruncateAtWordBoundary(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length <= MaxExcerptLength)
        {
            return trimmed;
        }

        var candidate = trimmed[..MaxExcerptLength];
        var lastSpace = candidate.LastIndexOf(' ');
        if (lastSpace > MaxExcerptLength / 2)
        {
            candidate = candidate[..lastSpace];
        }

        return $"{candidate.Trim()}…";
    }
}
