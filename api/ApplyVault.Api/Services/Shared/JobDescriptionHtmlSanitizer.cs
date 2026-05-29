using System.Net;
using System.Text.RegularExpressions;

namespace ApplyVault.Api.Services.Shared;

internal static class JobDescriptionHtmlSanitizer
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "br", "ul", "ol", "li", "strong", "em", "b", "i",
        "h1", "h2", "h3", "h4", "h5", "h6",
        "a", "div", "span", "blockquote",
        "table", "thead", "tbody", "tr", "th", "td"
    };

    private static readonly HashSet<string> BlockTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "div", "section", "article", "header", "footer", "ul", "ol", "li",
        "h1", "h2", "h3", "h4", "h5", "h6", "blockquote", "table", "tr"
    };

    private static readonly Regex TagPattern = new(
        @"</?(?<tag>[a-z0-9]+)(?<attrs>[^>]*)>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex OnEventHandlerPattern = new(
        @"\s(on\w+|style|class|id)\s*=",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex HrefPattern = new(
        @"\shref\s*=\s*(?<quote>['""])(?<url>.*?)\k<quote>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ExcessiveBlankLinesPattern = new(
        @"(\r?\n\s*){3,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ForbiddenBlockPattern = new(
        @"<(script|style|iframe)\b[^>]*>[\s\S]*?</\1>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var decoded = WebUtility.HtmlDecode(html);
        var withoutForbiddenBlocks = ForbiddenBlockPattern.Replace(decoded, string.Empty);
        var sanitized = TagPattern.Replace(withoutForbiddenBlocks, SanitizeTagMatch);
        sanitized = ExcessiveBlankLinesPattern.Replace(sanitized, "\n\n");
        return sanitized.Trim();
    }

    private static string SanitizeTagMatch(Match match)
    {
        var tag = match.Groups["tag"].Value;
        var isClosing = match.Value.StartsWith("</", StringComparison.Ordinal);

        if (!AllowedTags.Contains(tag))
        {
            if (BlockTags.Contains(tag))
            {
                return isClosing ? "\n\n" : string.Empty;
            }

            return string.Empty;
        }

        if (isClosing || !tag.Equals("a", StringComparison.OrdinalIgnoreCase))
        {
            return isClosing ? $"</{tag}>" : $"<{tag}>";
        }

        var attrs = match.Groups["attrs"].Value;
        if (OnEventHandlerPattern.IsMatch(attrs))
        {
            return string.Empty;
        }

        var hrefMatch = HrefPattern.Match(attrs);
        if (!hrefMatch.Success)
        {
            return "<a>";
        }

        var url = hrefMatch.Groups["url"].Value.Trim();
        if (!IsSafeHref(url))
        {
            return "<a>";
        }

        return $"""<a href="{WebUtility.HtmlEncode(url)}">""";
    }

    private static bool IsSafeHref(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (url.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            return absolute.Scheme is "http" or "https" or "mailto";
        }

        return url.StartsWith("/", StringComparison.Ordinal)
            || url.StartsWith("#", StringComparison.Ordinal)
            || !url.Contains(":", StringComparison.Ordinal);
    }
}
