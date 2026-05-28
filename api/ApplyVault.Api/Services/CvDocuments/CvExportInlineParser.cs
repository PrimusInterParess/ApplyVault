namespace ApplyVault.Api.Services;

internal sealed record CvExportInlineRun(string Text, bool Bold, bool Italic, string? LinkUrl);

internal static class CvExportInlineParser
{
    public static IReadOnlyList<CvExportInlineRun> ParseRuns(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var runs = new List<CvExportInlineRun>();
        var index = 0;

        while (index < text.Length)
        {
            if (TryParseLink(text, index, out var linkRun, out var linkLength))
            {
                runs.Add(linkRun);
                index += linkLength;
                continue;
            }

            if (TryParseWrapped(text, index, "**", out var boldText, out var boldLength))
            {
                runs.AddRange(ParseRuns(boldText).Select((run) => run with { Bold = true }));
                index += boldLength;
                continue;
            }

            if (TryParseWrapped(text, index, "*", out var italicText, out var italicLength))
            {
                runs.AddRange(ParseRuns(italicText).Select((run) => run with { Italic = true }));
                index += italicLength;
                continue;
            }

            var nextSpecial = FindNextSpecialIndex(text, index);

            if (nextSpecial > index)
            {
                AppendPlainRun(runs, text[index..nextSpecial]);
            }

            index = nextSpecial == index ? index + 1 : nextSpecial;
        }

        return runs.Count == 0 ? [new CvExportInlineRun(text, false, false, null)] : runs;
    }

    private static bool TryParseLink(
        string text,
        int index,
        out CvExportInlineRun run,
        out int length)
    {
        run = null!;
        length = 0;

        if (text[index] != '[')
        {
            return false;
        }

        var labelEnd = text.IndexOf(']', index + 1);

        if (labelEnd < 0 || labelEnd + 1 >= text.Length || text[labelEnd + 1] != '(')
        {
            return false;
        }

        var urlEnd = text.IndexOf(')', labelEnd + 2);

        if (urlEnd < 0)
        {
            return false;
        }

        var label = text[(index + 1)..labelEnd];
        var url = text[(labelEnd + 2)..urlEnd].Trim();
        var safeUrl = SanitizeLinkUrl(url);

        if (safeUrl is null)
        {
            return false;
        }

        run = new CvExportInlineRun(label, false, false, safeUrl);
        length = urlEnd - index + 1;
        return true;
    }

    private static bool TryParseWrapped(
        string text,
        int index,
        string marker,
        out string inner,
        out int length)
    {
        inner = string.Empty;
        length = 0;

        if (!text.AsSpan(index).StartsWith(marker, StringComparison.Ordinal))
        {
            return false;
        }

        var closeIndex = text.IndexOf(marker, index + marker.Length, StringComparison.Ordinal);

        if (closeIndex < 0)
        {
            return false;
        }

        inner = text[(index + marker.Length)..closeIndex];

        if (inner.Length == 0)
        {
            return false;
        }

        length = closeIndex + marker.Length - index;
        return true;
    }

    private static int FindNextSpecialIndex(string text, int start)
    {
        var index = start;

        while (index < text.Length)
        {
            if (text[index] == '[' || text[index] == '*')
            {
                return index;
            }

            index += 1;
        }

        return text.Length;
    }

    private static void AppendPlainRun(List<CvExportInlineRun> runs, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        if (runs.Count > 0
            && runs[^1].LinkUrl is null
            && !runs[^1].Bold
            && !runs[^1].Italic)
        {
            runs[^1] = runs[^1] with { Text = runs[^1].Text + value };
            return;
        }

        runs.Add(new CvExportInlineRun(value, false, false, null));
    }

    internal static string? SanitizeLinkUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.Scheme is "http" or "https" or "mailto" ? uri.ToString() : null;
    }
}
