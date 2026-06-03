using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ApplyVault.Api.Services;

internal static class CvExportInlinePdfRenderer
{
    public static void AppendRuns(
        TextDescriptor text,
        string? value,
        Action<TextSpanDescriptor> configureSpan)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (var run in CvExportInlineParser.ParseRuns(value))
        {
            var span = run.LinkUrl is null
                ? text.Span(run.Text)
                : text.Hyperlink(run.Text, run.LinkUrl);

            if (run.LinkUrl is not null)
            {
                span.FontColor(Colors.Black).Underline(false);
            }

            configureSpan(span);

            if (run.Bold)
            {
                span.Bold();
            }

            if (run.Italic)
            {
                span.Italic();
            }

        }
    }
}
