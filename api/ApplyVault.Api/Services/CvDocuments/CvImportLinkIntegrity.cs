namespace ApplyVault.Api.Services;

/// <summary>
/// Shared URL-span helpers for PDF import link integrity (extract join, contact split, tests).
/// </summary>
internal static class CvImportLinkIntegrity
{
    /// <summary>
    /// True when <paramref name="ch"/> may appear inside a URL path/query/fragment.
    /// </summary>
    public static bool IsUrlContinuationChar(char ch)
    {
        if (char.IsLetterOrDigit(ch))
        {
            return true;
        }

        return "/?#&=%.-_~@:+,;()[]!".Contains(ch);
    }

    public static bool IsContactDelimiter(char ch) =>
        ch is '|' or '·' or '•';

    /// <summary>
    /// True when text at <paramref name="index"/> starts a URL-like span
    /// (<c>https?://</c>, <c>www.</c>, <c>mailto:</c>, <c>linkedin.com/</c>, <c>github.com/</c>).
    /// </summary>
    public static bool TryGetUrlSpanStart(string text, int index, out int spanStart)
    {
        spanStart = -1;

        if (string.IsNullOrEmpty(text) || index < 0 || index >= text.Length)
        {
            return false;
        }

        if (StartsAt(text, index, "https://", ignoreCase: true)
            || StartsAt(text, index, "http://", ignoreCase: true)
            || StartsAt(text, index, "mailto:", ignoreCase: true)
            || StartsAt(text, index, "www.", ignoreCase: true)
            || StartsAt(text, index, "linkedin.com/", ignoreCase: true)
            || StartsAt(text, index, "github.com/", ignoreCase: true))
        {
            spanStart = index;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Extends a URL span from <paramref name="spanStart"/> until whitespace or a contact delimiter.
    /// </summary>
    public static int GetUrlSpanEnd(string text, int spanStart)
    {
        if (string.IsNullOrEmpty(text) || spanStart < 0 || spanStart >= text.Length)
        {
            return spanStart;
        }

        var end = spanStart;

        while (end < text.Length)
        {
            var ch = text[end];

            if (char.IsWhiteSpace(ch) || IsContactDelimiter(ch))
            {
                break;
            }

            if (!IsUrlContinuationChar(ch))
            {
                break;
            }

            end++;
        }

        return end;
    }

    /// <summary>
    /// Returns true if index is inside any URL span in <paramref name="text"/>.
    /// </summary>
    public static bool IsInsideUrlSpan(string text, int index)
    {
        if (string.IsNullOrEmpty(text) || index < 0 || index >= text.Length)
        {
            return false;
        }

        for (var i = 0; i <= index; i++)
        {
            if (!TryGetUrlSpanStart(text, i, out var spanStart))
            {
                continue;
            }

            var spanEnd = GetUrlSpanEnd(text, spanStart);

            if (index >= spanStart && index < spanEnd)
            {
                return true;
            }

            if (spanEnd > i)
            {
                i = spanEnd - 1;
            }
        }

        return false;
    }

    /// <summary>
    /// Join Y-cluster tokens; omit the space when adjacent tokens continue a URL span.
    /// </summary>
    public static string JoinAdjacentTokens(IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
        builder.Append(tokens[0].Trim());

        for (var i = 1; i < tokens.Count; i++)
        {
            var next = tokens[i].Trim();

            if (next.Length == 0)
            {
                continue;
            }

            var left = builder.ToString();

            if (ShouldJoinWithoutSpace(left, next))
            {
                builder.Append(next);
            }
            else
            {
                builder.Append(' ');
                builder.Append(next);
            }
        }

        return builder.ToString().Trim();
    }

    public static bool ShouldJoinWithoutSpace(string left, string right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
        {
            return false;
        }

        // Previous token ends a scheme — next is host/path.
        if (left.EndsWith("://", StringComparison.OrdinalIgnoreCase)
            || left.EndsWith("mailto:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Left is already a URL prefix / open path; right continues with URL chars.
        if (LooksLikeUrlPrefix(left)
            && right.Length > 0
            && IsUrlContinuationChar(right[0])
            && !char.IsWhiteSpace(right[0]))
        {
            // Avoid gluing unrelated words: right should look like a path/host fragment.
            if (right.Contains('/') || right.Contains('.') || right.Contains('?') || right.Contains('#')
                || left.EndsWith('/') || left.EndsWith('.') || left.EndsWith(':'))
            {
                return true;
            }
        }

        return false;
    }

    public static bool LooksLikeUrlLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();

        return trimmed.Contains("https://", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("www.", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("mailto:", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("linkedin.com/", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("github.com/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeUrlPrefix(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        return text.Contains("https://", StringComparison.OrdinalIgnoreCase)
            || text.Contains("http://", StringComparison.OrdinalIgnoreCase)
            || text.Contains("www.", StringComparison.OrdinalIgnoreCase)
            || text.Contains("mailto:", StringComparison.OrdinalIgnoreCase)
            || text.Contains("linkedin.com", StringComparison.OrdinalIgnoreCase)
            || text.Contains("github.com", StringComparison.OrdinalIgnoreCase)
            || text.EndsWith("://", StringComparison.Ordinal);
    }

    /// <summary>
    /// Split contact multi-value lines on <c>|</c>, <c>·</c>, <c>•</c>, and commas outside URL spans.
    /// Never splits on <c>/</c> or <c>\</c>.
    /// </summary>
    public static IReadOnlyList<string> SplitContactTokens(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return [];
        }

        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (IsInsideUrlSpan(line, i))
            {
                current.Append(ch);
                continue;
            }

            if (IsContactDelimiter(ch))
            {
                FlushToken(current, tokens);
                continue;
            }

            if (ch == ',' && ShouldSplitOnCommaOutsideUrl(line, i, current))
            {
                FlushToken(current, tokens);
                continue;
            }

            current.Append(ch);
        }

        FlushToken(current, tokens);

        return tokens;
    }

    private static bool ShouldSplitOnCommaOutsideUrl(string line, int commaIndex, System.Text.StringBuilder current)
    {
        var soFar = current.ToString().Trim();
        var remainder = line[(commaIndex + 1)..].Trim();

        if (soFar.Length == 0 || remainder.Length == 0)
        {
            return false;
        }

        var whole = line.Trim();
        return CvStructuredImportEntrySupport.LooksLikeContactLine(whole)
            || CvStructuredImportEntrySupport.LooksLikeContactLine(soFar)
            || CvStructuredImportEntrySupport.LooksLikeContactLine(remainder);
    }

    private static void FlushToken(System.Text.StringBuilder current, List<string> tokens)
    {
        var value = current.ToString().Trim();
        current.Clear();

        if (value.Length > 0)
        {
            tokens.Add(value);
        }
    }

    private static bool StartsAt(string text, int index, string prefix, bool ignoreCase)
    {
        if (index + prefix.Length > text.Length)
        {
            return false;
        }

        return string.Compare(
            text,
            index,
            prefix,
            0,
            prefix.Length,
            ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) == 0;
    }
}
