using QuestPDF.Fluent;
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
            var span = text.Span(run.Text);
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
