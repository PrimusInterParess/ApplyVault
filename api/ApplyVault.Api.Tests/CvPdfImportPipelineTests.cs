using ApplyVault.Api.Models;
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
    public async Task BuildPreviewAsync_CallsAiWhenGoogleAiEnabled()
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
        var catalog = CvSectionCatalogProvider.LoadFromDefaultPath();
        var extractor = new CvPdfFullTextExtractor(catalog);

        var preview = await CvPdfImportPipeline.BuildPreviewAsync(
            pdfBytes,
            extractor,
            aiClient,
            catalog,
            googleAiEnabled: true,
            CancellationToken.None);

        Assert.True(aiClient.WasCalled);
        Assert.False(string.IsNullOrWhiteSpace(aiClient.LastFullText));
        Assert.Contains("Experience", aiClient.LastFullText!, StringComparison.OrdinalIgnoreCase);
        Assert.True(preview.UsedAi);
        Assert.Equal(CvPdfImportPipeline.ImportedWithAiNotice, preview.Notice);
        Assert.Equal(7, preview.Sections.Count);
        Assert.Contains(
            preview.Sections,
            (section) => section.Entries.Any((entry) => entry.Title == "AI Title"));
        Assert.True(preview.UnplacedContent.Count > 0);
    }

    [Fact]
    public async Task BuildPreviewAsync_DoesNotCallAiWhenGoogleAiDisabled()
    {
        var pdfBytes = CreateStructuredCvPdf();
        var aiClient = new SpyImportAiClient();
        var catalog = CvSectionCatalogProvider.LoadFromDefaultPath();
        var extractor = new CvPdfFullTextExtractor(catalog);

        var preview = await CvPdfImportPipeline.BuildPreviewAsync(
            pdfBytes,
            extractor,
            aiClient,
            catalog,
            googleAiEnabled: false,
            CancellationToken.None);

        Assert.False(aiClient.WasCalled);
        Assert.False(preview.UsedAi);
        Assert.Equal(7, preview.Sections.Count);
        Assert.Equal(CvPdfImportPipeline.ContactOnlyNotice, preview.Notice);
        Assert.Contains(
            preview.Sections.First((section) => section.SectionType == CvSectionTypes.Contact).Entries,
            (entry) => entry.Bullets.Contains("jane@example.com"));
        Assert.True(preview.UnplacedContent.Count > 0);
    }

    [Fact]
    public async Task BuildPreviewAsync_FallsBackWhenAiReturnsEmpty()
    {
        var pdfBytes = CreateStructuredCvPdf();
        var aiClient = new SpyImportAiClient
        {
            ResultFactory = () => new CvStructuredImportResult([])
        };
        var catalog = CvSectionCatalogProvider.LoadFromDefaultPath();
        var extractor = new CvPdfFullTextExtractor(catalog);

        var preview = await CvPdfImportPipeline.BuildPreviewAsync(
            pdfBytes,
            extractor,
            aiClient,
            catalog,
            googleAiEnabled: true,
            CancellationToken.None);

        Assert.True(aiClient.WasCalled);
        Assert.False(preview.UsedAi);
        Assert.Equal(7, preview.Sections.Count);
        Assert.Equal(CvPdfImportPipeline.ContactOnlyNotice, preview.Notice);
    }

    [Fact]
    public async Task BuildPreviewAsync_FallsBackWhenAiThrows()
    {
        var pdfBytes = CreateStructuredCvPdf();
        var aiClient = new SpyImportAiClient
        {
            ResultFactory = () => throw new HttpRequestException("Gemini unavailable")
        };
        var catalog = CvSectionCatalogProvider.LoadFromDefaultPath();
        var extractor = new CvPdfFullTextExtractor(catalog);

        var preview = await CvPdfImportPipeline.BuildPreviewAsync(
            pdfBytes,
            extractor,
            aiClient,
            catalog,
            googleAiEnabled: true,
            CancellationToken.None);

        Assert.True(aiClient.WasCalled);
        Assert.False(preview.UsedAi);
        Assert.Equal(7, preview.Sections.Count);
        Assert.Equal(CvPdfImportPipeline.ContactOnlyNotice, preview.Notice);
    }

    [Fact]
    public async Task BuildPreviewAsync_EmptyPdf_WithoutAi_ThrowsClearNotice()
    {
        var pdfBytes = CreateBlankPdf();
        var aiClient = new SpyImportAiClient();
        var extractor = new CvPdfFullTextExtractor(CvSectionCatalogProvider.LoadFromDefaultPath());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CvPdfImportPipeline.BuildPreviewAsync(
                pdfBytes,
                extractor,
                aiClient,
                CvSectionCatalogProvider.LoadFromDefaultPath(),
                googleAiEnabled: false,
                CancellationToken.None));

        Assert.False(aiClient.WasCalled);
        Assert.False(aiClient.WasPdfCalled);
        Assert.Equal(CvStructuredImportNotices.EmptyExtraction, exception.Message);
        Assert.DoesNotContain("OCR", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildPreviewAsync_EmptyPdf_WithAi_SendsPdfAndFillsSections()
    {
        var pdfBytes = CreateBlankPdf();
        var aiClient = new SpyImportAiClient
        {
            PdfResultFactory = () => new CvStructuredImportResult(
            [
                new CvStructuredImportSectionResult(
                    "Experience",
                    CvSectionTypes.Experience,
                    [
                        new CvStructuredImportEntryResult(
                            "PDF Title",
                            "PDF Corp",
                            "2022 – 2024",
                            "From attached PDF",
                            [],
                            string.Empty)
                    ])
            ])
        };
        var catalog = CvSectionCatalogProvider.LoadFromDefaultPath();
        var extractor = new CvPdfFullTextExtractor(catalog);

        var preview = await CvPdfImportPipeline.BuildPreviewAsync(
            pdfBytes,
            extractor,
            aiClient,
            catalog,
            googleAiEnabled: true,
            CancellationToken.None);

        Assert.False(aiClient.WasCalled);
        Assert.True(aiClient.WasPdfCalled);
        Assert.Equal(pdfBytes, aiClient.LastPdfBytes);
        Assert.True(preview.UsedAi);
        Assert.Equal(CvPdfImportPipeline.ImportedWithAiNotice, preview.Notice);
        Assert.Contains(
            preview.Sections,
            (section) => section.Entries.Any((entry) => entry.Title == "PDF Title"));
    }

    [Fact]
    public async Task BuildPreviewAsync_EmptyPdf_WithAiEmptyResult_ThrowsClearNotice()
    {
        var pdfBytes = CreateBlankPdf();
        var aiClient = new SpyImportAiClient
        {
            PdfResultFactory = () => new CvStructuredImportResult([])
        };
        var extractor = new CvPdfFullTextExtractor(CvSectionCatalogProvider.LoadFromDefaultPath());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CvPdfImportPipeline.BuildPreviewAsync(
                pdfBytes,
                extractor,
                aiClient,
                CvSectionCatalogProvider.LoadFromDefaultPath(),
                googleAiEnabled: true,
                CancellationToken.None));

        Assert.True(aiClient.WasPdfCalled);
        Assert.Equal(CvStructuredImportNotices.EmptyExtraction, exception.Message);
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

        public bool WasPdfCalled { get; private set; }

        public string? LastFullText { get; private set; }

        public byte[]? LastPdfBytes { get; private set; }

        public Func<CvStructuredImportResult>? ResultFactory { get; init; }

        public Func<CvStructuredImportResult>? PdfResultFactory { get; init; }

        public Task<CvStructuredImportResult> ParseAsync(
            string extractedFullText,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            LastFullText = extractedFullText;
            return Task.FromResult(
                ResultFactory?.Invoke()
                ?? new CvStructuredImportResult([]));
        }

        public Task<CvStructuredImportResult> ParsePdfAsync(
            byte[] pdfBytes,
            CancellationToken cancellationToken = default)
        {
            WasPdfCalled = true;
            LastPdfBytes = pdfBytes;
            return Task.FromResult(
                PdfResultFactory?.Invoke()
                ?? new CvStructuredImportResult([]));
        }
    }
}
