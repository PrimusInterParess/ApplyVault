using ApplyVault.Api.Services;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace ApplyVault.Api.Tests;

public sealed class CvPdfProjectsMergeBuilderTests
{
    [Fact]
    public void Merge_InsertsSectionPagesBeforeAppendix()
    {
        EnsureFontResolver();

        var basePdf = CreateTwoPagePdf(firstPageLines: ["Experience", "Existing role"], secondPageLines: ["Education"]);
        var summaries = new[]
        {
            new CvPdfMergePlacement(
                "Experience",
                0,
                new CvPdfProjectSummaryEntry(
                    "ApplyVault Platform",
                    "Built a job application tracker.",
                    ["Implemented REST APIs."],
                    "C#, Angular"))
        };

        using var input = new MemoryStream(basePdf);
        var merged = CvPdfProjectsMergeBuilder.Merge(
            input,
            summaries,
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Experience"] = 0
            });

        using var mergedDocument = PdfReader.Open(new MemoryStream(merged), PdfDocumentOpenMode.Import);
        using var baseDocument = PdfReader.Open(new MemoryStream(basePdf), PdfDocumentOpenMode.Import);

        Assert.True(mergedDocument.PageCount > baseDocument.PageCount);
    }

    [Fact]
    public void Merge_UnmatchedSectionHeading_FallsBackToAppendix()
    {
        EnsureFontResolver();

        var basePdf = CreateTwoPagePdf(firstPageLines: ["Profile"], secondPageLines: ["Education"]);
        var summaries = new[]
        {
            new CvPdfMergePlacement(
                "Experience",
                0,
                new CvPdfProjectSummaryEntry(
                    "Missing Section Project",
                    "Should still appear in appendix.",
                    ["Fallback bullet."],
                    "TypeScript"))
        };

        using var input = new MemoryStream(basePdf);
        var merged = CvPdfProjectsMergeBuilder.Merge(
            input,
            summaries,
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));

        using var mergedDocument = PdfReader.Open(new MemoryStream(merged), PdfDocumentOpenMode.Import);
        using var baseDocument = PdfReader.Open(new MemoryStream(basePdf), PdfDocumentOpenMode.Import);

        Assert.True(mergedDocument.PageCount > baseDocument.PageCount);
    }

    [Fact]
    public void Merge_FromSameBase_ProducesStablePageCount()
    {
        EnsureFontResolver();

        var basePdf = CreateTwoPagePdf(firstPageLines: ["Experience"], secondPageLines: ["Education"]);
        var summaries = new[]
        {
            new CvPdfMergePlacement(
                null,
                0,
                new CvPdfProjectSummaryEntry("First Project", "Summary one.", ["Bullet one."], "C#")),
            new CvPdfMergePlacement(
                null,
                1,
                new CvPdfProjectSummaryEntry("Second Project", "Summary two.", ["Bullet two."], "Python"))
        };

        using var firstInput = new MemoryStream(basePdf);
        var firstMerged = CvPdfProjectsMergeBuilder.Merge(
            firstInput,
            summaries,
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));

        using var secondInput = new MemoryStream(basePdf);
        var secondMerged = CvPdfProjectsMergeBuilder.Merge(
            secondInput,
            summaries,
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));

        using var firstDocument = PdfReader.Open(new MemoryStream(firstMerged), PdfDocumentOpenMode.Import);
        using var secondDocument = PdfReader.Open(new MemoryStream(secondMerged), PdfDocumentOpenMode.Import);

        Assert.Equal(firstDocument.PageCount, secondDocument.PageCount);
    }

    [Fact]
    public void Merge_WithoutSummaries_Throws()
    {
        EnsureFontResolver();

        var basePdf = CreateTwoPagePdf(firstPageLines: ["Experience"], secondPageLines: ["Education"]);

        using var input = new MemoryStream(basePdf);

        Assert.Throws<InvalidOperationException>(() =>
            CvPdfProjectsMergeBuilder.Merge(
                input,
                [],
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)));
    }

    private static byte[] CreateTwoPagePdf(
        IReadOnlyList<string> firstPageLines,
        IReadOnlyList<string> secondPageLines)
    {
        using var document = new PdfDocument();
        AddLines(document.AddPage(), firstPageLines);
        AddLines(document.AddPage(), secondPageLines);

        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    private static void AddLines(PdfPage page, IReadOnlyList<string> lines)
    {
        using var graphics = XGraphics.FromPdfPage(page);
        var font = new XFont("Arial", 14, XFontStyleEx.Bold);
        var y = 50d;

        foreach (var line in lines)
        {
            graphics.DrawString(line, font, XBrushes.Black, 50, y);
            y += 24;
        }
    }

    private static void EnsureFontResolver()
    {
        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.FontResolver = ApplyVaultPdfFontResolver.Instance;
        }
    }
}
