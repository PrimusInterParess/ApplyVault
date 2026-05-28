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

    private static void AppendRun(StringBuilder builder, CvExportInlineRun run)
    {
        if (run.LinkUrl is not null)
        {
            builder.Append("<a href=\"");
            builder.Append(WebUtility.HtmlEncode(run.LinkUrl));
            builder.Append("\" rel=\"noopener noreferrer\">");
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
