using ApplyVault.Api.Models;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace ApplyVault.Api.Services;

internal static class CvPdfStructuredExportBuilder
{
    private const double Margin = 50;
    private const double LineHeightFactor = 1.35;

    static CvPdfStructuredExportBuilder()
    {
        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.FontResolver = ApplyVaultPdfFontResolver.Instance;
        }
    }

    public static byte[] Build(CvStructuredDocumentDto document)
    {
        using var pdfDocument = new PdfDocument();
        var titleFont = new XFont("Arial", 18, XFontStyleEx.Bold);
        var sectionFont = new XFont("Arial", 14, XFontStyleEx.Bold);
        var entryTitleFont = new XFont("Arial", 12, XFontStyleEx.Bold);
        var metaFont = new XFont("Arial", 10, XFontStyleEx.Italic);
        var bodyFont = new XFont("Arial", 11, XFontStyleEx.Regular);
        var techFont = new XFont("Arial", 10, XFontStyleEx.Italic);

        var context = new PageLayoutContext(pdfDocument);
        context.Y = Margin;

        foreach (var section in document.Sections.OrderBy((section) => section.SortOrder))
        {
            context.Y += 8;
            context.EnsureSpace(sectionFont.Height * 2);
            context.Y = context.DrawWrapped(section.Heading, sectionFont, Margin, context.ContentWidth);
            context.Y += 6;

            foreach (var entry in section.Entries.OrderBy((entry) => entry.SortOrder))
            {
                context.EnsureSpace(entryTitleFont.Height * 3);
                context.Y = context.DrawWrapped(entry.Title, entryTitleFont, Margin, context.ContentWidth);

                var metaLine = BuildMetaLine(entry.Subtitle, entry.DateRange);

                if (!string.IsNullOrWhiteSpace(metaLine))
                {
                    context.Y = context.DrawWrapped(metaLine, metaFont, Margin, context.ContentWidth);
                }

                context.Y += 2;

                if (!string.IsNullOrWhiteSpace(entry.Summary))
                {
                    context.Y = context.DrawWrapped(entry.Summary.Trim(), bodyFont, Margin, context.ContentWidth);
                    context.Y += 4;
                }

                foreach (var bullet in entry.Bullets)
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

                if (!string.IsNullOrWhiteSpace(entry.TechStack))
                {
                    context.Y = context.DrawWrapped(
                        $"Tech: {entry.TechStack.Trim()}",
                        techFont,
                        Margin,
                        context.ContentWidth);
                }

                context.Y += 10;
            }
        }

        if (pdfDocument.PageCount == 0)
        {
            context.EnsureSpace(titleFont.Height * 2);
            context.DrawWrapped("CV", titleFont, Margin, context.ContentWidth);
        }

        using var outputStream = new MemoryStream();
        pdfDocument.Save(outputStream);
        return outputStream.ToArray();
    }

    private static string BuildMetaLine(string? subtitle, string? dateRange)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            parts.Add(subtitle.Trim());
        }

        if (!string.IsNullOrWhiteSpace(dateRange))
        {
            parts.Add(dateRange.Trim());
        }

        return string.Join(" · ", parts);
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
