using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace ApplyVault.Api.Services;

public sealed record CvPdfMergePlacement(
    string? SectionHeading,
    int SortOrder,
    CvPdfProjectSummaryEntry Entry);

internal static class CvPdfProjectsMergeBuilder
{
    private const string AppendixSectionHeading = "Personal Projects";
    private const double Margin = 50;
    private const double LineHeightFactor = 1.35;

    static CvPdfProjectsMergeBuilder()
    {
        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.FontResolver = ApplyVaultPdfFontResolver.Instance;
        }
    }

    public static byte[] Merge(
        Stream basePdf,
        IReadOnlyList<CvPdfMergePlacement> placements,
        IReadOnlyDictionary<string, int> sectionPageIndexes)
    {
        if (placements.Count == 0)
        {
            throw new InvalidOperationException("Add at least one saved project summary before merging.");
        }

        var appendixSummaries = new List<CvPdfProjectSummaryEntry>();
        var sectionInsertions = new Dictionary<int, List<(string SectionHeading, List<CvPdfProjectSummaryEntry> Summaries)>>();

        foreach (var group in placements
                     .GroupBy((placement) => placement.SectionHeading, StringComparer.OrdinalIgnoreCase))
        {
            var orderedEntries = group
                .OrderBy((placement) => placement.SortOrder)
                .Select((placement) => placement.Entry)
                .ToArray();

            if (string.IsNullOrWhiteSpace(group.Key))
            {
                appendixSummaries.AddRange(orderedEntries);
                continue;
            }

            if (!TryResolveSectionPageIndex(group.Key!, sectionPageIndexes, out var pageIndex))
            {
                appendixSummaries.AddRange(orderedEntries);
                continue;
            }

            if (!sectionInsertions.TryGetValue(pageIndex, out var pageInsertions))
            {
                pageInsertions = [];
                sectionInsertions[pageIndex] = pageInsertions;
            }

            pageInsertions.Add((group.Key!, orderedEntries.ToList()));
        }

        using var outputDocument = new PdfDocument();
        PdfDocument? inputDocument = null;

        try
        {
            inputDocument = PdfReader.Open(basePdf, PdfDocumentOpenMode.Import);

            for (var pageIndex = 0; pageIndex < inputDocument.PageCount; pageIndex++)
            {
                outputDocument.AddPage(inputDocument.Pages[pageIndex]);

                if (!sectionInsertions.TryGetValue(pageIndex, out var insertionsForPage))
                {
                    continue;
                }

                foreach (var insertion in insertionsForPage.OrderBy((insertion) => insertion.SectionHeading, StringComparer.OrdinalIgnoreCase))
                {
                    RenderSummaryPages(outputDocument, insertion.SectionHeading, insertion.Summaries);
                }
            }
        }
        finally
        {
            inputDocument?.Close();
        }

        if (appendixSummaries.Count > 0)
        {
            RenderSummaryPages(outputDocument, AppendixSectionHeading, appendixSummaries);
        }

        using var outputStream = new MemoryStream();
        outputDocument.Save(outputStream);
        return outputStream.ToArray();
    }

    public static byte[] MergeAppendixOnly(Stream basePdf, IReadOnlyList<CvPdfProjectSummaryEntry> summaries) =>
        Merge(
            basePdf,
            summaries.Select((entry, index) => new CvPdfMergePlacement(null, index, entry)).ToArray(),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));

    private static bool TryResolveSectionPageIndex(
        string sectionHeading,
        IReadOnlyDictionary<string, int> sectionPageIndexes,
        out int pageIndex)
    {
        if (sectionPageIndexes.TryGetValue(sectionHeading, out pageIndex))
        {
            return true;
        }

        var normalizedHeading = sectionHeading.Trim();

        foreach (var pair in sectionPageIndexes)
        {
            if (string.Equals(pair.Key, normalizedHeading, StringComparison.OrdinalIgnoreCase))
            {
                pageIndex = pair.Value;
                return true;
            }
        }

        pageIndex = -1;
        return false;
    }

    private static void RenderSummaryPages(
        PdfDocument document,
        string sectionHeading,
        IReadOnlyList<CvPdfProjectSummaryEntry> summaries)
    {
        var titleFont = new XFont("Arial", 18, XFontStyleEx.Bold);
        var projectTitleFont = new XFont("Arial", 14, XFontStyleEx.Bold);
        var bodyFont = new XFont("Arial", 11, XFontStyleEx.Regular);
        var techFont = new XFont("Arial", 10, XFontStyleEx.Italic);

        var context = new PageLayoutContext(document);
        context.Y = Margin;
        context.Y = context.DrawWrapped(sectionHeading, titleFont, Margin, context.ContentWidth);

        foreach (var summary in summaries)
        {
            context.Y += 10;
            context.EnsureSpace(projectTitleFont.Height * 2);

            context.Y = context.DrawWrapped(summary.CvTitle, projectTitleFont, Margin, context.ContentWidth);
            context.Y += 4;

            if (!string.IsNullOrWhiteSpace(summary.CvSummary))
            {
                context.Y = context.DrawWrapped(summary.CvSummary.Trim(), bodyFont, Margin, context.ContentWidth);
                context.Y += 4;
            }

            foreach (var bullet in summary.CvBullets)
            {
                if (string.IsNullOrWhiteSpace(bullet))
                {
                    continue;
                }

                context.Y = context.DrawWrapped(
                    $"• {bullet.Trim()}",
                    bodyFont,
                    Margin + 12,
                    context.ContentWidth - 12);
                context.Y += 2;
            }

            if (!string.IsNullOrWhiteSpace(summary.TechStack))
            {
                context.Y = context.DrawWrapped(
                    $"Tech: {summary.TechStack.Trim()}",
                    techFont,
                    Margin,
                    context.ContentWidth);
            }

            context.Y += 12;
        }
    }

    private sealed class PageLayoutContext
    {
        private readonly PdfDocument _document;
        private readonly double _pageWidth = XUnit.FromMillimeter(210).Point;
        private readonly double _pageHeight = XUnit.FromMillimeter(297).Point;

        public double ContentWidth => _pageWidth - (Margin * 2);
        public PdfPage Page { get; private set; }
        public XGraphics Graphics { get; private set; }
        public double Y { get; set; }

        public PageLayoutContext(PdfDocument document)
        {
            _document = document;
            Page = document.AddPage();
            Page.Size = PdfSharp.PageSize.A4;
            Graphics = XGraphics.FromPdfPage(Page);
        }

        public void EnsureSpace(double requiredHeight)
        {
            if (Y <= _pageHeight - Margin - requiredHeight)
            {
                return;
            }

            Page = _document.AddPage();
            Page.Size = PdfSharp.PageSize.A4;
            Graphics = XGraphics.FromPdfPage(Page);
            Y = Margin;
        }

        public double DrawWrapped(string text, XFont font, double x, double maxWidth)
        {
            foreach (var line in WrapText(text, font, maxWidth))
            {
                EnsureSpace(font.Height * LineHeightFactor);
                Graphics.DrawString(line, font, XBrushes.Black, new XPoint(x, Y));
                Y += font.Height * LineHeightFactor;
            }

            return Y;
        }

        private IEnumerable<string> WrapText(string text, XFont font, double maxWidth)
        {
            var paragraphs = text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (paragraphs.Length == 0)
            {
                yield break;
            }

            foreach (var paragraph in paragraphs)
            {
                var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var currentLine = string.Empty;

                foreach (var word in words)
                {
                    var candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
                    var size = Graphics.MeasureString(candidate, font);

                    if (size.Width > maxWidth && !string.IsNullOrEmpty(currentLine))
                    {
                        yield return currentLine;
                        currentLine = word;
                    }
                    else
                    {
                        currentLine = candidate;
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    yield return currentLine;
                }
            }
        }
    }
}
