using ApplyVault.Api.Options;
using ApplyVault.Api.Services.CvSectionCatalog;
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
    IOptions<CvImportAiOptions>? importAiOptions = null) : ICvPdfFullTextExtractor
{
    internal const double YTolerancePoints = 2.0;
    internal const double ColumnGapMinWidthPoints = 24.0;
    internal const int DefaultSparseCharsPerPageThreshold = 120;
    internal const int SparseWordsPerPageThreshold = 20;

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
                    orderedLines.Add(new CvPdfExtractedLine(pageIndex, line.YPoints, line.Text));
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
            return new CvPdfExtractionResult([], [], pageCount, 0, wordCount, CvPdfExtractionQuality.Empty);
        }

        var sections = Sectionize(orderedLines);
        return new CvPdfExtractionResult(orderedLines, sections, pageCount, charCount, wordCount, quality);
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
        var words = page.GetWords().ToArray();

        if (words.Length > 0)
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

        if (page.Letters.Count == 0)
        {
            return [];
        }

        try
        {
            var nearestWords = page.GetWords(NearestNeighbourWordExtractor.Instance).ToArray();

            if (nearestWords.Length > 0)
            {
                return nearestWords
                    .Select(static (word) => new TextToken(
                        word.Text,
                        word.BoundingBox.Left,
                        word.BoundingBox.Right,
                        word.BoundingBox.Bottom,
                        word.BoundingBox.Top))
                    .Where(static (token) => !string.IsNullOrWhiteSpace(token.Text))
                    .ToArray();
            }
        }
        catch
        {
            // Fall through to letter clustering.
        }

        return AssembleTokensFromLetters(page.Letters);
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

        var splitX = TryDetectColumnSplit(tokens, pageWidth);
        var bands = splitX is null
            ? new[] { tokens }
            : new[]
            {
                tokens.Where((token) => token.CenterX < splitX.Value).ToArray(),
                tokens.Where((token) => token.CenterX >= splitX.Value).ToArray()
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

    private static double? TryDetectColumnSplit(IReadOnlyList<TextToken> tokens, double pageWidth)
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
            if (Math.Abs(mid - pageMid) > pageWidth * 0.25)
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

        foreach (var cluster in clusters)
        {
            var y = cluster.Average(static (token) => token.Bottom);
            var text = string.Join(
                    " ",
                    cluster
                        .OrderBy(static (token) => token.Left)
                        .Select(static (token) => token.Text))
                .Trim();

            if (!string.IsNullOrWhiteSpace(text))
            {
                yield return (text, y);
            }
        }
    }

    private sealed record TextToken(string Text, double Left, double Right, double Bottom, double Top)
    {
        public double CenterX => (Left + Right) / 2.0;
    }
}
