namespace ApplyVault.Api.Services;

internal sealed record CvMarkdownEntry(
    string Title,
    string? MetaLine,
    IReadOnlyList<string> Paragraphs,
    IReadOnlyList<string> Bullets,
    string? TechStack);

internal static class CvExportMarkdownGrouper
{
    public static IReadOnlyList<CvMarkdownEntry> GroupEntries(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return [];
        }

        var entries = new List<CvMarkdownEntry>();
        CvMarkdownEntry? current = null;
        var lines = markdown.Replace("\r\n", "\n").Split('\n');

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("### ", StringComparison.Ordinal)
                || line.StartsWith("## ", StringComparison.Ordinal)
                || line.StartsWith("# ", StringComparison.Ordinal))
            {
                if (current is not null)
                {
                    entries.Add(current);
                }

                var title = line.StartsWith("### ", StringComparison.Ordinal)
                    ? line[4..]
                    : line.StartsWith("## ", StringComparison.Ordinal)
                        ? line[3..]
                        : line[2..];

                current = new CvMarkdownEntry(title.Trim(), null, [], [], null);
                continue;
            }

            if (current is null)
            {
                current = new CvMarkdownEntry(string.Empty, null, [], [], null);
            }

            if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
            {
                var bullet = StripMarkup(line[2..].Trim());
                current = current with
                {
                    Bullets = current.Bullets.Append(bullet).ToArray()
                };
                continue;
            }

            if (line.StartsWith('*') && line.EndsWith('*') && line.Length > 2)
            {
                current = current with { TechStack = StripMarkup(line.Trim('*').Trim()) };
                continue;
            }

            if (current.MetaLine is null && line.Contains("**", StringComparison.Ordinal) && line.Contains('·'))
            {
                current = current with { MetaLine = StripMarkup(line) };
                continue;
            }

            if (current.MetaLine is null && line.Contains("**", StringComparison.Ordinal))
            {
                current = current with { MetaLine = StripMarkup(line) };
                continue;
            }

            current = current with
            {
                Paragraphs = current.Paragraphs.Append(StripMarkup(line)).ToArray()
            };
        }

        if (current is not null)
        {
            entries.Add(current);
        }

        return entries
            .Where((entry) => !string.IsNullOrWhiteSpace(entry.Title)
                || entry.Paragraphs.Count > 0
                || entry.Bullets.Count > 0
                || !string.IsNullOrWhiteSpace(entry.MetaLine)
                || !string.IsNullOrWhiteSpace(entry.TechStack))
            .ToArray();
    }

    private static string StripMarkup(string text) =>
        text.Replace("**", string.Empty, StringComparison.Ordinal).Trim();
}
