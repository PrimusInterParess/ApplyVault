using ApplyVault.Api.Services;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace ApplyVault.Api.Tests;

public sealed class CvPdfProfilePhotoExtractorTests
{
    private static readonly byte[] TinyPngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    static CvPdfProfilePhotoExtractorTests()
    {
        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.FontResolver = ApplyVaultPdfFontResolver.Instance;
        }
    }

    [Fact]
    public void TryExtractProfilePhoto_text_only_pdf_returns_null()
    {
        var extractor = new CvPdfProfilePhotoExtractor();
        using var stream = new MemoryStream(CreateTextOnlyPdf());

        var result = extractor.TryExtractProfilePhoto(stream);

        Assert.Null(result);
    }

    [Fact]
    public void TryExtractProfilePhoto_square_headshot_on_page_one_returns_photo()
    {
        var extractor = new CvPdfProfilePhotoExtractor();
        using var stream = new MemoryStream(CreatePdfWithImage(x: 430, y: 40, width: 80, height: 80));

        var result = extractor.TryExtractProfilePhoto(stream);

        Assert.NotNull(result);
        Assert.NotEmpty(result!.ImageBytes);
        Assert.True(
            string.Equals(result.ContentType, "image/png", StringComparison.OrdinalIgnoreCase)
            || string.Equals(result.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryExtractProfilePhoto_wide_banner_image_returns_null()
    {
        var extractor = new CvPdfProfilePhotoExtractor();
        using var stream = new MemoryStream(CreatePdfWithImage(x: 50, y: 40, width: 220, height: 50));

        var result = extractor.TryExtractProfilePhoto(stream);

        Assert.Null(result);
    }

    private static byte[] CreateTextOnlyPdf()
    {
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;

        using (var graphics = XGraphics.FromPdfPage(page))
        {
            graphics.DrawString(
                "Experience\nSoftware Engineer at Acme",
                new XFont("Arial", 12),
                XBrushes.Black,
                new XPoint(50, 80));
        }

        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    private static byte[] CreatePdfWithImage(double x, double y, double width, double height)
    {
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;

        using (var graphics = XGraphics.FromPdfPage(page))
        {
            using var image = LoadTinyPngImage();
            graphics.DrawImage(image, x, y, width, height);
            graphics.DrawString(
                "Experience\nSoftware Engineer at Acme",
                new XFont("Arial", 12),
                XBrushes.Black,
                new XPoint(50, 180));
        }

        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    private static XImage LoadTinyPngImage()
    {
        var imageStream = new MemoryStream(TinyPngBytes);
        return XImage.FromStream(imageStream);
    }
}
