using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services;
using ApplyVault.Api.Services.CvSectionCatalog;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace ApplyVault.Api.Tests;

public sealed class CvPdfImportPipelineTests
{
    public CvPdfImportPipelineTests()
    {
        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.FontResolver = ApplyVaultPdfFontResolver.Instance;
        }
    }

    [Fact]
    public async Task BuildPreviewAsync_DoesNotCallAiWhenHeuristicConfidenceHigh()
    {
        var pdfBytes = CreateStructuredCvPdf();
        var aiClient = new SpyImportAiClient();
        var extractor = new CvPdfFullTextExtractor(CvSectionCatalogProvider.LoadFromDefaultPath());

        var preview = await CvPdfImportPipeline.BuildPreviewAsync(
            pdfBytes,
            extractor,
            aiClient,
            googleAiEnabled: true,
            new CvImportAiOptions(),
            CancellationToken.None);

        Assert.False(aiClient.WasCalled);
        Assert.False(preview.UsedAi);
        Assert.True(preview.Sections.Count > 0);
        Assert.Null(preview.Notice);
        Assert.DoesNotContain(
            "Google AI",
            preview.Notice ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildPreviewAsync_CallsAiWhenForceAi()
    {
        var pdfBytes = CreateStructuredCvPdf();
        var aiClient = new SpyImportAiClient
        {
            ResultFactory = () => new CvStructuredImportResult(
            [
                new CvStructuredImportSectionResult(
                    "Experience",
                    CvSectionTypes.Experience,
                    [
                        new CvStructuredImportEntryResult(
                            "AI Title",
                            "AI Corp",
                            "2021 – 2023",
                            "AI summary",
                            [],
                            string.Empty)
                    ])
            ])
        };
        var extractor = new CvPdfFullTextExtractor(CvSectionCatalogProvider.LoadFromDefaultPath());

        var preview = await CvPdfImportPipeline.BuildPreviewAsync(
            pdfBytes,
            extractor,
            aiClient,
            googleAiEnabled: true,
            new CvImportAiOptions { ForceAi = true },
            CancellationToken.None);

        Assert.True(aiClient.WasCalled);
        Assert.True(preview.UsedAi);
        Assert.Null(preview.Notice);
        Assert.Contains(
            preview.Sections,
            (section) => section.Entries.Any((entry) => entry.Title == "AI Title"));
    }

    [Fact]
    public async Task BuildPreviewAsync_EmptyPdf_ThrowsClearNotice()
    {
        var pdfBytes = CreateBlankPdf();
        var aiClient = new SpyImportAiClient();
        var extractor = new CvPdfFullTextExtractor(CvSectionCatalogProvider.LoadFromDefaultPath());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CvPdfImportPipeline.BuildPreviewAsync(
                pdfBytes,
                extractor,
                aiClient,
                googleAiEnabled: true,
                new CvImportAiOptions(),
                CancellationToken.None));

        Assert.False(aiClient.WasCalled);
        Assert.Equal(CvStructuredImportNotices.EmptyExtraction, exception.Message);
        Assert.DoesNotContain("OCR", exception.Message, StringComparison.OrdinalIgnoreCase);
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
            graphics.DrawString("jane@example.com | +45 12 34 56 78", bodyFont, XBrushes.Black, new XPoint(50, y));
            y += 24;
            graphics.DrawString("Summary", headingFont, XBrushes.Black, new XPoint(50, y));
            y += 22;
            graphics.DrawString(
                "Experienced software engineer focused on reliable backend systems.",
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
            graphics.DrawString("Built reliable services.", bodyFont, XBrushes.Black, new XPoint(50, y));
            y += 28;
            graphics.DrawString("Skills", headingFont, XBrushes.Black, new XPoint(50, y));
            y += 22;
            graphics.DrawString("Languages: English, Danish", bodyFont, XBrushes.Black, new XPoint(50, y));
            y += 18;
            graphics.DrawString("Frameworks: .NET, Angular", bodyFont, XBrushes.Black, new XPoint(50, y));
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

    private sealed class SpyImportAiClient : ICvStructuredImportAiClient
    {
        public bool WasCalled { get; private set; }

        public Func<CvStructuredImportResult>? ResultFactory { get; init; }

        public Task<CvStructuredImportResult> ParseAsync(
            IReadOnlyList<CvImportSectionInput> sections,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(
                ResultFactory?.Invoke()
                ?? new CvStructuredImportResult([]));
        }
    }
}
