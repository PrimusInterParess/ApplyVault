using ApplyVault.Api.Services;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace ApplyVault.Api.Tests;

public sealed class CvPdfProjectsAppendixBuilderTests
{
    [Fact]
    public void Merge_AppendsProjectsSection_AndIncreasesPageCount()
    {
        var basePdf = CreateSinglePagePdf();
        var summaries = new[]
        {
            new CvPdfProjectSummaryEntry(
                "ApplyVault Platform",
                "Built a job application tracker with scraping and calendar sync.",
                ["Implemented REST APIs in ASP.NET Core.", "Integrated GitHub project summaries."],
                "C#, Angular, SQL Server")
        };

        using var input = new MemoryStream(basePdf);
        var merged = CvPdfProjectsAppendixBuilder.Merge(input, summaries);

        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(merged.AsSpan(0, 4)));

        using var mergedDocument = PdfReader.Open(new MemoryStream(merged), PdfDocumentOpenMode.Import);
        using var baseDocument = PdfReader.Open(new MemoryStream(basePdf), PdfDocumentOpenMode.Import);

        Assert.True(mergedDocument.PageCount > baseDocument.PageCount);
    }

    [Fact]
    public void Merge_FromSameBase_ProducesStablePageCount()
    {
        var basePdf = CreateSinglePagePdf();
        var summaries = new[]
        {
            new CvPdfProjectSummaryEntry(
                "First Project",
                "Summary for the first project.",
                ["Bullet one.", "Bullet two."],
                "TypeScript, Node.js"),
            new CvPdfProjectSummaryEntry(
                "Second Project",
                "Summary for the second project.",
                ["Bullet alpha."],
                "Python")
        };

        using var firstInput = new MemoryStream(basePdf);
        var firstMerged = CvPdfProjectsAppendixBuilder.Merge(firstInput, summaries);

        using var secondInput = new MemoryStream(basePdf);
        var secondMerged = CvPdfProjectsAppendixBuilder.Merge(secondInput, summaries);

        using var firstDocument = PdfReader.Open(new MemoryStream(firstMerged), PdfDocumentOpenMode.Import);
        using var secondDocument = PdfReader.Open(new MemoryStream(secondMerged), PdfDocumentOpenMode.Import);

        Assert.Equal(firstDocument.PageCount, secondDocument.PageCount);
    }

    [Fact]
    public void Merge_WithoutSummaries_Throws()
    {
        var basePdf = CreateSinglePagePdf();

        using var input = new MemoryStream(basePdf);

        Assert.Throws<InvalidOperationException>(() => CvPdfProjectsAppendixBuilder.Merge(input, []));
    }

    private static byte[] CreateSinglePagePdf()
    {
        if (PdfSharp.Fonts.GlobalFontSettings.FontResolver is null)
        {
            PdfSharp.Fonts.GlobalFontSettings.FontResolver = ApplyVaultPdfFontResolver.Instance;
        }

        using var document = new PdfDocument();
        document.AddPage();

        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }
}
