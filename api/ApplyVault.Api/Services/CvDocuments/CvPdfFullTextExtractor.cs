using ApplyVault.Api.Options;
using ApplyVault.Api.Services.CvSectionCatalog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace ApplyVault.Api.Services;

public sealed record CvPdfRawSection(string Heading, string NormalizedKey, int PageIndex, string Text);

public interface ICvPdfFullTextExtractor
{
    CvPdfExtractionResult Extract(Stream pdfStream);
}

public sealed class CvPdfFullTextExtractor(
    ICvSectionCatalog sectionCatalog,
    ILogger<CvPdfFullTextExtractor>? logger = null,
    IOptions<CvImportAiOptions>? importAiOptions = null) : ICvPdfFullTextExtractor
{
    /// <summary>Console/log filter token — grep or search for this exact tag.</summary>
    public const string LogTag = "[CvPdfExtract]";

    internal const double YTolerancePoints = 2.0;
    internal const double ColumnGapMinWidthPoints = 24.0;
    /// <summary>Page-global column mid window (fraction of page width).</summary>
    internal const double DefaultColumnMidToleranceRatio = 0.25;
    /// <summary>Wider mid window for body-only split after header/span strip.</summary>
    internal const double BodyColumnMidToleranceRatio = 0.40;
    /// <summary>Token/line width ≥ this fraction of page width counts as spanning.</summary>
    internal const double HeaderSpanWidthRatio = 0.60;
    internal const int DefaultSparseCharsPerPageThreshold = 120;
    internal const int SparseWordsPerPageThreshold = 20;
    internal const double MinLetterCoverageRatio = 0.90;

    private readonly ILogger _logger = logger ?? NullLogger<CvPdfFullTextExtractor>.Instance;

    public CvPdfExtractionResult Extract(Stream pdfStream)
    {
        if (pdfStream.CanSeek)
        {
            pdfStream.Position = 0;
        }

        var orderedLines = new List<CvPdfExtractedLine>();
        var pageCount = 0;
        var wordCount = 0;

        using (var document = PdfDocument.Open(pdfStream))
        {
            pageCount = document.NumberOfPages;

            for (var pageIndex = 0; pageIndex < document.NumberOfPages; pageIndex++)
            {
                var page = document.GetPage(pageIndex + 1);
                var tokens = GetPageTokens(page);
                wordCount += tokens.Count;

                foreach (var line in BuildReadingOrderLines(tokens, page.Width))
                {
                    var text = NormalizeExtractedText(line.Text);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        continue;
                    }

                    orderedLines.Add(new CvPdfExtractedLine(pageIndex, line.YPoints, text));
                }
            }
        }

        var charCount = orderedLines.Sum(static (line) => line.Text.Length);
        var sparseCharsThreshold = importAiOptions?.Value.SparseMaxAverageCharsPerPage
            is > 0 and var configured
            ? configured
            : DefaultSparseCharsPerPageThreshold;
        var quality = ClassifyQuality(pageCount, charCount, wordCount, sparseCharsThreshold);

        if (orderedLines.Count == 0)
        {
            _logger.LogInformation(
                "{Tag} empty extraction pages={PageCount} words={WordCount}",
                LogTag,
                pageCount,
                wordCount);
            return new CvPdfExtractionResult([], [], pageCount, 0, wordCount, CvPdfExtractionQuality.Empty);
        }

        // AI-first: return ordered lines only. Sectionize is deferred to pipeline fallback/residual.
        _logger.LogInformation(
            "{Tag} quality={Quality} pages={PageCount} words={WordCount} chars={CharCount} lines={LineCount}",
            LogTag,
            quality,
            pageCount,
            wordCount,
            charCount,
            orderedLines.Count);

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            foreach (var line in orderedLines)
            {
                _logger.LogDebug("{Tag} P{Page} | {Text}", LogTag, line.PageIndex + 1, line.Text);
            }
        }

        return new CvPdfExtractionResult(orderedLines, [], pageCount, charCount, wordCount, quality);
    }

    /// <summary>
    /// Normalize PdfPig quirks so AI/heuristic see clean text: ligatures, soft hyphens, NULs.
    /// </summary>
    internal static string NormalizeExtractedText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return text
            .Replace("\0", string.Empty, StringComparison.Ordinal)
            .Replace("\u00AD", string.Empty, StringComparison.Ordinal) // soft hyphen
            .Replace("\uFB01", "fi", StringComparison.Ordinal) // ﬁ
            .Replace("\uFB02", "fl", StringComparison.Ordinal) // ﬂ
            .Replace("\u2013", "-", StringComparison.Ordinal) // en dash → ASCII (keep readable)
            .Replace("\u2014", "-", StringComparison.Ordinal);
    }

    internal static CvPdfExtractionQuality ClassifyQuality(
        int pageCount,
        int charCount,
        int wordCount,
        int sparseCharsPerPageThreshold = DefaultSparseCharsPerPageThreshold)
    {
        if (charCount <= 0 || wordCount <= 0)
        {
            return CvPdfExtractionQuality.Empty;
        }

        var pages = Math.Max(pageCount, 1);
        var charsPerPage = charCount / (double)pages;
        var wordsPerPage = wordCount / (double)pages;

        if (charsPerPage <= sparseCharsPerPageThreshold || wordsPerPage < SparseWordsPerPageThreshold)
        {
            return CvPdfExtractionQuality.Sparse;
        }

        return CvPdfExtractionQuality.Good;
    }

    /// <summary>
    /// Catalog-alias sectionize for heuristic fallback and P1 residual only (not on extract happy path).
    /// </summary>
    internal IReadOnlyList<CvPdfRawSection> SectionizeForFallback(IReadOnlyList<CvPdfExtractedLine> orderedLines) =>
        Sectionize(orderedLines);

    private IReadOnlyList<CvPdfRawSection> Sectionize(IReadOnlyList<CvPdfExtractedLine> orderedLines)
    {
        var sections = new List<(string Heading, string NormalizedKey, int PageIndex, List<string> BodyLines)>();
        List<string>? currentBody = null;
        string currentHeading = "Profile";
        string currentNormalizedKey = "summary";
        var currentPageIndex = 0;

        foreach (var line in orderedLines)
        {
            if (sectionCatalog.TryMatchSectionHeading(line.Text, out var normalizedKey))
            {
                FlushSection();

                currentHeading = line.Text.Trim();
                currentNormalizedKey = normalizedKey;
                currentPageIndex = line.PageIndex;
                currentBody = [];
                continue;
            }

            currentBody ??= [];
            currentBody.Add(line.Text);
        }

        FlushSection();

        return sections
            .Select((section) => new CvPdfRawSection(
                section.Heading,
                section.NormalizedKey,
                section.PageIndex,
                string.Join('\n', section.BodyLines)))
            .ToArray();

        void FlushSection()
        {
            if (currentBody is null)
            {
                return;
            }

            sections.Add((currentHeading, currentNormalizedKey, currentPageIndex, currentBody));
            currentBody = null;
        }
    }

    private static IReadOnlyList<TextToken> GetPageTokens(Page page)
    {
        var letterChars = CountNonWhitespaceChars(page.Letters);
        if (letterChars == 0)
        {
            return [];
        }

        // Default word extractor first — stable for typical digital CVs.
        var defaultWords = ToTokens(page.GetWords());
        if (HasGoodCoverage(defaultWords, letterChars))
        {
            return defaultWords;
        }

        try
        {
            var nearestWords = ToTokens(page.GetWords(NearestNeighbourWordExtractor.Instance));
            if (HasGoodCoverage(nearestWords, letterChars)
                && nearestWords.Count <= Math.Max(defaultWords.Count * 3, letterChars))
            {
                // Reject NN when it explodes into letter-sized tokens.
                var avgLen = nearestWords.Average(static (token) => token.Text.Length);
                if (avgLen >= 2.0)
                {
                    return nearestWords;
                }
            }
        }
        catch
        {
            // Fall through.
        }

        if (defaultWords.Count > 0)
        {
            return defaultWords;
        }

        return AssembleTokensFromLetters(page.Letters);
    }

    private static bool HasGoodCoverage(IReadOnlyList<TextToken> tokens, int letterChars)
    {
        if (tokens.Count == 0 || letterChars <= 0)
        {
            return false;
        }

        var chars = tokens.Sum(static (token) => CountNonWhitespaceChars(token.Text));
        return chars >= letterChars * MinLetterCoverageRatio;
    }

    private static IReadOnlyList<TextToken> ToTokens(IEnumerable<Word> words)
    {
        return words
            .Select(static (word) => new TextToken(
                word.Text,
                word.BoundingBox.Left,
                word.BoundingBox.Right,
                word.BoundingBox.Bottom,
                word.BoundingBox.Top))
            .Where(static (token) => !string.IsNullOrWhiteSpace(token.Text))
            .ToArray();
    }

    private static int CountNonWhitespaceChars(IEnumerable<Letter> letters)
    {
        return letters
            .Where(static (letter) => !string.IsNullOrWhiteSpace(letter.Value))
            .Sum(static (letter) => letter.Value.Count(static (ch) => !char.IsWhiteSpace(ch)));
    }

    private static int CountNonWhitespaceChars(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var count = 0;
        foreach (var ch in text)
        {
            if (!char.IsWhiteSpace(ch))
            {
                count++;
            }
        }

        return count;
    }

    private static IReadOnlyList<TextToken> AssembleTokensFromLetters(IReadOnlyList<Letter> letters)
    {
        var glyphs = letters
            .Where(static (letter) => !string.IsNullOrWhiteSpace(letter.Value))
            .Select(static (letter) =>
            {
                var box = letter.BoundingBox;
                return new TextToken(
                    letter.Value,
                    box.Left,
                    box.Right,
                    box.Bottom,
                    box.Top);
            })
            .OrderByDescending(static (token) => token.Bottom)
            .ThenBy(static (token) => token.Left)
            .ToArray();

        if (glyphs.Length == 0)
        {
            return [];
        }

        var words = new List<TextToken>();
        var current = new List<TextToken> { glyphs[0] };

        for (var index = 1; index < glyphs.Length; index++)
        {
            var glyph = glyphs[index];
            var previous = current[^1];
            var sameLine = Math.Abs(glyph.Bottom - previous.Bottom) <= YTolerancePoints;
            var gap = glyph.Left - previous.Right;
            var typicalWidth = Math.Max(previous.Right - previous.Left, 1.0);

            if (sameLine && gap <= typicalWidth * 0.6)
            {
                current.Add(glyph);
                continue;
            }

            words.Add(MergeTokens(current));
            current = [glyph];
        }

        words.Add(MergeTokens(current));
        return words;
    }

    private static TextToken MergeTokens(IReadOnlyList<TextToken> tokens)
    {
        var text = string.Concat(tokens.Select(static (token) => token.Text));
        return new TextToken(
            text,
            tokens.Min(static (token) => token.Left),
            tokens.Max(static (token) => token.Right),
            tokens.Average(static (token) => token.Bottom),
            tokens.Max(static (token) => token.Top));
    }

    private static IEnumerable<(string Text, double YPoints)> BuildReadingOrderLines(
        IReadOnlyList<TextToken> tokens,
        double pageWidth)
    {
        if (tokens.Count == 0)
        {
            yield break;
        }

        // Option 1: emit top full-width/span header as single-column, then split body only.
        var headerTokens = TakeTopHeaderSpanTokens(tokens, pageWidth);
        if (headerTokens.Count > 0)
        {
            foreach (var line in ClusterLinesByY(headerTokens))
            {
                yield return line;
            }
        }

        var bodyTokens = headerTokens.Count == 0
            ? tokens
            : tokens.Where((token) => !headerTokens.Contains(token)).ToArray();

        if (bodyTokens.Count == 0)
        {
            yield break;
        }

        var midTolerance = headerTokens.Count > 0
            ? BodyColumnMidToleranceRatio
            : DefaultColumnMidToleranceRatio;
        var splitX = TryDetectColumnSplit(bodyTokens, pageWidth, midTolerance);
        var bands = splitX is null
            ? new[] { bodyTokens }
            : new[]
            {
                bodyTokens.Where((token) => token.CenterX < splitX.Value).ToArray(),
                bodyTokens.Where((token) => token.CenterX >= splitX.Value).ToArray()
            };

        foreach (var band in bands)
        {
            if (band.Count == 0)
            {
                continue;
            }

            foreach (var line in ClusterLinesByY(band))
            {
                yield return line;
            }
        }
    }

    /// <summary>
    /// Contiguous top Y-lines that span the page (wide token / continuous mid coverage).
    /// Stops at the first non-span line so two-column body rows are not absorbed.
    /// </summary>
    private static IReadOnlyList<TextToken> TakeTopHeaderSpanTokens(
        IReadOnlyList<TextToken> tokens,
        double pageWidth)
    {
        if (pageWidth <= 0)
        {
            return [];
        }

        var pageMid = pageWidth / 2.0;
        var header = new List<TextToken>();

        foreach (var line in GroupTokensIntoYLines(tokens))
        {
            if (!IsSpanningHeaderLine(line, pageWidth, pageMid))
            {
                break;
            }

            header.AddRange(line);
        }

        return header;
    }

    private static bool IsSpanningHeaderLine(
        IReadOnlyList<TextToken> line,
        double pageWidth,
        double pageMid)
    {
        if (line.Count == 0)
        {
            return false;
        }

        var ordered = line.OrderBy(static (token) => token.Left).ToArray();

        // Reject two-column rows first: large gap near page mid is left|right, not a header span.
        for (var index = 1; index < ordered.Length; index++)
        {
            var gap = ordered[index].Left - ordered[index - 1].Right;
            if (gap < ColumnGapMinWidthPoints)
            {
                continue;
            }

            var gapMid = (ordered[index].Left + ordered[index - 1].Right) / 2.0;
            if (Math.Abs(gapMid - pageMid) <= pageWidth * DefaultColumnMidToleranceRatio)
            {
                return false;
            }
        }

        // Single wide token (name/title bar) or token that straddles the page mid.
        foreach (var token in ordered)
        {
            if (token.Width >= pageWidth * HeaderSpanWidthRatio)
            {
                return true;
            }

            if (token.Left < pageMid && token.Right > pageMid
                && token.Width >= pageWidth * 0.20)
            {
                return true;
            }
        }

        var minLeft = ordered[0].Left;
        var maxRight = ordered[^1].Right;
        if (maxRight - minLeft < pageWidth * HeaderSpanWidthRatio)
        {
            return false;
        }

        return minLeft < pageMid && maxRight > pageMid;
    }

    private static double? TryDetectColumnSplit(
        IReadOnlyList<TextToken> tokens,
        double pageWidth,
        double midToleranceRatio = DefaultColumnMidToleranceRatio)
    {
        if (tokens.Count < 8 || pageWidth <= 0)
        {
            return null;
        }

        var centers = tokens
            .Select(static (token) => token.CenterX)
            .OrderBy(static (value) => value)
            .ToArray();

        var pageMid = pageWidth / 2.0;
        var bestGap = 0.0;
        double? bestSplit = null;

        for (var index = 1; index < centers.Length; index++)
        {
            var gap = centers[index] - centers[index - 1];
            var mid = (centers[index] + centers[index - 1]) / 2.0;

            if (gap < ColumnGapMinWidthPoints)
            {
                continue;
            }

            // Prefer a gap near the horizontal middle (simple 2-column layouts).
            if (Math.Abs(mid - pageMid) > pageWidth * midToleranceRatio)
            {
                continue;
            }

            if (gap > bestGap)
            {
                bestGap = gap;
                bestSplit = mid;
            }
        }

        if (bestSplit is null)
        {
            return null;
        }

        var leftCount = tokens.Count(token => token.CenterX < bestSplit.Value);
        var rightCount = tokens.Count - leftCount;

        return leftCount >= 3 && rightCount >= 3 ? bestSplit : null;
    }

    private static IEnumerable<(string Text, double YPoints)> ClusterLinesByY(IReadOnlyList<TextToken> tokens)
    {
        foreach (var cluster in GroupTokensIntoYLines(tokens))
        {
            var y = cluster.Average(static (token) => token.Bottom);
            var orderedTexts = cluster
                .OrderBy(static (token) => token.Left)
                .Select(static (token) => token.Text)
                .ToArray();
            var text = CvImportLinkIntegrity.JoinAdjacentTokens(orderedTexts);

            if (!string.IsNullOrWhiteSpace(text))
            {
                yield return (text, y);
            }
        }
    }

    private static List<List<TextToken>> GroupTokensIntoYLines(IReadOnlyList<TextToken> tokens)
    {
        var ordered = tokens
            .OrderByDescending(static (token) => token.Bottom)
            .ThenBy(static (token) => token.Left)
            .ToArray();

        var clusters = new List<List<TextToken>>();
        List<TextToken>? current = null;
        var currentY = 0.0;

        foreach (var token in ordered)
        {
            if (current is null)
            {
                current = [token];
                currentY = token.Bottom;
                continue;
            }

            if (Math.Abs(token.Bottom - currentY) <= YTolerancePoints)
            {
                current.Add(token);
                currentY = current.Average(static (item) => item.Bottom);
                continue;
            }

            clusters.Add(current);
            current = [token];
            currentY = token.Bottom;
        }

        if (current is not null)
        {
            clusters.Add(current);
        }

        return clusters;
    }

    private sealed record TextToken(string Text, double Left, double Right, double Bottom, double Top)
    {
        public double CenterX => (Left + Right) / 2.0;

        public double Width => Right - Left;
    }
}
