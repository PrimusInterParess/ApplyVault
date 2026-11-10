using System.Net;
using System.Text;

namespace ApplyVault.Api.Services;

internal static class CvExportInlineHtmlRenderer
{
    public static string Render(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        foreach (var run in CvExportInlineParser.ParseRuns(value))
        {
            AppendRun(builder, run);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Contact lines that are only an http(s) URL, or a linkedin.com / github.com host,
    /// become a real link. Other copy stays on the markdown inline path.
    /// </summary>
    public static string RenderContactLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();

        if (!TryBareContactHref(trimmed, out var href))
        {
            return Render(trimmed);
        }

        return "<a href=\""
            + WebUtility.HtmlEncode(href)
            + "\" target=\"_blank\" rel=\"noopener noreferrer\">"
            + WebUtility.HtmlEncode(trimmed)
            + "</a>";
    }

    private static bool TryBareContactHref(string trimmed, out string href)
    {
        href = string.Empty;

        if (trimmed.Any(char.IsWhiteSpace) || trimmed.Contains('[') || trimmed.Contains('*'))
        {
            return false;
        }

        var isAbsoluteHttp = trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        if (!isAbsoluteHttp && !IsLinkedInOrGitHubHost(trimmed))
        {
            return false;
        }

        var safe = CvExportInlineParser.SanitizeLinkUrl(trimmed);

        if (safe is null
            || !(safe.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || safe.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        href = safe;
        return true;
    }

    private static bool IsLinkedInOrGitHubHost(string value)
    {
        var candidate = value.Contains("://", StringComparison.Ordinal) ? value : $"https://{value}";

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.IdnHost;

        return host.Equals("linkedin.com", StringComparison.OrdinalIgnoreCase)
            || host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".linkedin.com", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendRun(StringBuilder builder, CvExportInlineRun run)
    {
        if (run.LinkUrl is not null)
        {
            builder.Append("<a href=\"");
            builder.Append(WebUtility.HtmlEncode(run.LinkUrl));
            builder.Append("\" target=\"_blank\" rel=\"noopener noreferrer\">");
            AppendStyledText(builder, run.Text, run.Bold, run.Italic);
            builder.Append("</a>");
            return;
        }

        AppendStyledText(builder, run.Text, run.Bold, run.Italic);
    }

    private static void AppendStyledText(StringBuilder builder, string text, bool bold, bool italic)
    {
        var encoded = WebUtility.HtmlEncode(text);

        if (bold && italic)
        {
            builder.Append("<strong><em>").Append(encoded).Append("</em></strong>");
            return;
        }

        if (bold)
        {
            builder.Append("<strong>").Append(encoded).Append("</strong>");
            return;
        }

        if (italic)
        {
            builder.Append("<em>").Append(encoded).Append("</em>");
            return;
        }

        builder.Append(encoded);
    }
}
