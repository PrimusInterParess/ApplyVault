namespace ApplyVault.Api.Services;

internal enum CvExportMarkdownBlockKind
{
    Paragraph,
    Heading,
    Bullet
}

internal sealed record CvExportMarkdownBlock(
    CvExportMarkdownBlockKind Kind,
    int HeadingLevel,
    string Text,
    bool Bold);

internal static class CvExportMarkdownParser
{
    public static IReadOnlyList<CvExportMarkdownBlock> Parse(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return [];
        }

        var blocks = new List<CvExportMarkdownBlock>();
        var lines = markdown.Replace("\r\n", "\n").Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();

            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                blocks.Add(new CvExportMarkdownBlock(CvExportMarkdownBlockKind.Heading, 3, line[4..].Trim(), false));
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                blocks.Add(new CvExportMarkdownBlock(CvExportMarkdownBlockKind.Heading, 2, line[3..].Trim(), false));
                continue;
            }

            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                blocks.Add(new CvExportMarkdownBlock(CvExportMarkdownBlockKind.Heading, 1, line[2..].Trim(), false));
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                blocks.Add(new CvExportMarkdownBlock(
                    CvExportMarkdownBlockKind.Bullet,
                    0,
                    line[2..].Trim(),
                    false));
                continue;
            }

            blocks.Add(new CvExportMarkdownBlock(CvExportMarkdownBlockKind.Paragraph, 0, line, ContainsBoldMarkup(line)));
        }

        return blocks;
    }

    public static IReadOnlyList<CvExportMarkdownRun> ParseRuns(string text, bool defaultBold = false)
    {
        if (!text.Contains("**", StringComparison.Ordinal))
        {
            return [new CvExportMarkdownRun(text, defaultBold)];
        }

        var runs = new List<CvExportMarkdownRun>();
        var index = 0;
        var bold = defaultBold;

        while (index < text.Length)
        {
            var marker = text.IndexOf("**", index, StringComparison.Ordinal);

            if (marker < 0)
            {
                AppendRun(runs, text[index..], bold);
                break;
            }

            if (marker > index)
            {
                AppendRun(runs, text[index..marker], bold);
            }

            index = marker + 2;
            bold = !bold;
        }

        return runs.Count == 0 ? [new CvExportMarkdownRun(text, defaultBold)] : runs;
    }

    private static void AppendRun(List<CvExportMarkdownRun> runs, string value, bool bold)
    {
        var trimmed = value.Trim();

        if (trimmed.Length == 0)
        {
            return;
        }

        if (runs.Count > 0 && runs[^1].Bold == bold)
        {
            runs[^1] = runs[^1] with { Text = $"{runs[^1].Text} {trimmed}" };
            return;
        }

        runs.Add(new CvExportMarkdownRun(trimmed, bold));
    }

    private static bool ContainsBoldMarkup(string text) => text.Contains("**", StringComparison.Ordinal);
}

internal sealed record CvExportMarkdownRun(string Text, bool Bold);
