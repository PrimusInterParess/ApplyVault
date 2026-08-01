using ApplyVault.Api.Services;
using ApplyVault.Api.Services.CvSectionCatalog;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using PdfPigDocument = UglyToad.PdfPig.PdfDocument;

namespace ApplyVault.Api.Tests;

public sealed class CvPdfFullTextExtractorTests
{
    public CvPdfFullTextExtractorTests()
    {
        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.FontResolver = ApplyVaultPdfFontResolver.Instance;
        }
    }

    [Fact]
    public void Extract_StructuredCv_ReturnsGoodQualityAndSections()
    {
        var pdfBytes = CreateStructuredCvPdf();
        using var stream = new MemoryStream(pdfBytes);
        var extractor = new CvPdfFullTextExtractor(CvSectionCatalogProvider.LoadFromDefaultPath());

        var result = extractor.Extract(stream);

        Assert.Equal(CvPdfExtractionQuality.Good, result.Quality);
        Assert.True(result.CharCount > 120);
        Assert.True(result.WordCount > 20);
        Assert.Contains(result.Sections, (section) =>
            section.NormalizedKey.Equals("experience", StringComparison.OrdinalIgnoreCase)
            || section.Heading.Equals("Experience", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            result.Lines.Select((line) => line.Text),
            (line) => line.Contains("Software Engineer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Extract_BlankPdf_ReturnsEmptyQuality()
    {
        var pdfBytes = CreateBlankPdf();
        using var stream = new MemoryStream(pdfBytes);
        var extractor = new CvPdfFullTextExtractor(CvSectionCatalogProvider.LoadFromDefaultPath());

        var result = extractor.Extract(stream);

        Assert.Equal(CvPdfExtractionQuality.Empty, result.Quality);
        Assert.Empty(result.Lines);
        Assert.Empty(result.Sections);
    }

    [Fact]
    public void Extract_TwoColumnLayout_ReadsLeftThenRight()
    {
        var pdfBytes = CreateTwoColumnPdf();
        using var stream = new MemoryStream(pdfBytes);
        var extractor = new CvPdfFullTextExtractor(CvSectionCatalogProvider.LoadFromDefaultPath());

        var result = extractor.Extract(stream);
        var joined = string.Join('\n', result.Lines.Select((line) => line.Text));

        Assert.NotEqual(CvPdfExtractionQuality.Empty, result.Quality);
        Assert.Contains("Left column headline", joined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Right column headline", joined, StringComparison.OrdinalIgnoreCase);

        var leftIndex = joined.IndexOf("Left column headline", StringComparison.OrdinalIgnoreCase);
        var rightIndex = joined.IndexOf("Right column headline", StringComparison.OrdinalIgnoreCase);

        Assert.True(leftIndex >= 0 && rightIndex >= 0);
        Assert.True(leftIndex < rightIndex, "Expected left band before right band in reading order.");
    }

    [Theory]
    [InlineData(1, 0, 0, CvPdfExtractionQuality.Empty)]
    [InlineData(1, 50, 10, CvPdfExtractionQuality.Sparse)]
    [InlineData(1, 500, 80, CvPdfExtractionQuality.Good)]
    public void ClassifyQuality_MapsCounts(
        int pageCount,
        int charCount,
        int wordCount,
        CvPdfExtractionQuality expected)
    {
        var quality = CvPdfFullTextExtractor.ClassifyQuality(pageCount, charCount, wordCount);

        Assert.Equal(expected, quality);
    }

    [Fact]
    public void Extract_DesktopSampleCv_IfPresent_CoversNearlyAllReadableText()
    {
        // Local feedback loop against a real multi-column CV. Not checked into the repo (PII).
        const string samplePath = @"C:\Users\yborisov\Desktop\Yordan-Borisov.pdf";
        if (!File.Exists(samplePath))
        {
            return;
        }

        using var stream = File.OpenRead(samplePath);
        var extractor = new CvPdfFullTextExtractor(CvSectionCatalogProvider.LoadFromDefaultPath());
        var result = extractor.Extract(stream);
        var joined = string.Join('\n', result.Lines.Select((line) => line.Text));

        using var document = PdfPigDocument.Open(samplePath);
        var contentOrder = string.Join(
            '\n',
            document.GetPages().Select((page) => ContentOrderTextExtractor.GetText(page) ?? string.Empty));
        var contentChars = contentOrder.Count(static (ch) => !char.IsWhiteSpace(ch));
        var extractedChars = joined.Count(static (ch) => !char.IsWhiteSpace(ch));

        Assert.Equal(CvPdfExtractionQuality.Good, result.Quality);
        Assert.True(contentChars > 0);
        Assert.True(
            extractedChars >= contentChars * 0.95,
            $"Expected >=95% content-order coverage, got {extractedChars}/{contentChars}.");
        Assert.Contains("Yordan Borisov", joined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Experience", joined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ApplyVault", joined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SoftUni", joined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("About Me", joined, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] CreateStructuredCvPdf()
    {
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;

        using (var graphics = XGraphics.FromPdfPage(page))
        {
            var headingFont = new XFont("Arial", 14, XFontStyleEx.Bold);
            var bodyFont = new XFont("Arial", 11);
            var y = 80d;

            graphics.DrawString("Jane Doe", bodyFont, XBrushes.Black, new XPoint(50, y));
            y += 18;
            graphics.DrawString("jane@example.com", bodyFont, XBrushes.Black, new XPoint(50, y));
            y += 24;
            graphics.DrawString("Summary", headingFont, XBrushes.Black, new XPoint(50, y));
            y += 22;
            graphics.DrawString(
                "Experienced software engineer focused on reliable backend systems and APIs.",
                bodyFont,
                XBrushes.Black,
                new XPoint(50, y));
            y += 28;
            graphics.DrawString("Experience", headingFont, XBrushes.Black, new XPoint(50, y));
            y += 22;
            graphics.DrawString("Software Engineer", bodyFont, XBrushes.Black, new XPoint(50, y));
            y += 18;
            graphics.DrawString("Acme Corp", bodyFont, XBrushes.Black, new XPoint(50, y));
            y += 18;
            graphics.DrawString("2020 – 2024", bodyFont, XBrushes.Black, new XPoint(50, y));
            y += 18;
            graphics.DrawString("Built reliable services across multiple teams.", bodyFont, XBrushes.Black, new XPoint(50, y));
            y += 28;
            graphics.DrawString("Education", headingFont, XBrushes.Black, new XPoint(50, y));
            y += 22;
            graphics.DrawString("BSc Computer Science", bodyFont, XBrushes.Black, new XPoint(50, y));
            y += 18;
            graphics.DrawString("Example University", bodyFont, XBrushes.Black, new XPoint(50, y));
            y += 28;
            graphics.DrawString("Skills", headingFont, XBrushes.Black, new XPoint(50, y));
            y += 22;
            graphics.DrawString("Languages: English, Danish", bodyFont, XBrushes.Black, new XPoint(50, y));
            y += 18;
            graphics.DrawString("Frameworks: .NET, Angular, PostgreSQL", bodyFont, XBrushes.Black, new XPoint(50, y));
        }

        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    private static byte[] CreateBlankPdf()
    {
        using var document = new PdfDocument();
        document.AddPage();
        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    private static byte[] CreateTwoColumnPdf()
    {
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;

        using (var graphics = XGraphics.FromPdfPage(page))
        {
            var font = new XFont("Arial", 12);

            graphics.DrawString("Left column headline", font, XBrushes.Black, new XPoint(40, 100));
            graphics.DrawString("Left body one about experience details", font, XBrushes.Black, new XPoint(40, 120));
            graphics.DrawString("Left body two with more content here", font, XBrushes.Black, new XPoint(40, 140));
            graphics.DrawString("Left body three continues the story", font, XBrushes.Black, new XPoint(40, 160));

            graphics.DrawString("Right column headline", font, XBrushes.Black, new XPoint(340, 100));
            graphics.DrawString("Right body one skills and tools listed", font, XBrushes.Black, new XPoint(340, 120));
            graphics.DrawString("Right body two more skill details here", font, XBrushes.Black, new XPoint(340, 140));
            graphics.DrawString("Right body three wraps the column text", font, XBrushes.Black, new XPoint(340, 160));
        }

        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }
}
