using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace ApplyVault.Api.IntegrationTests;

public sealed class CvDocumentsUploadImportIntegrationTests(ApplyVaultWebApplicationFactory factory)
    : IClassFixture<ApplyVaultWebApplicationFactory>
{
    private static readonly byte[] TinyPngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    static CvDocumentsUploadImportIntegrationTests()
    {
        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.FontResolver = ApplyVaultPdfFontResolver.Instance;
        }
    }

    [Fact]
    public async Task Upload_cv_auto_imports_sections_and_keeps_original_download()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        var pdfBytes = CreateStructuredCvPdf(includeHeadshot: true);

        using var form = new MultipartFormDataContent();
        var pdf = new ByteArrayContent(pdfBytes);
        pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(pdf, "file", "cv.pdf");

        var uploadResponse = await client.PostAsync("/api/cv-documents/current", form);

        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<CvDocumentUploadResultDto>();

        Assert.NotNull(uploadResult);
        Assert.True(uploadResult!.Import.Succeeded, $"Import failed: {uploadResult.Import.Notice}");
        Assert.True(uploadResult.Import.SectionCount > 0);
        Assert.True(uploadResult.Document.HasStructuredContent);
        Assert.True(uploadResult.Import.ProfilePhotoExtracted);
        Assert.True(uploadResult.Document.HasProfilePhoto);

        var profilePhotoResponse = await client.GetAsync("/api/cv-documents/current/profile-photo");

        Assert.Equal(HttpStatusCode.OK, profilePhotoResponse.StatusCode);
        Assert.NotEqual("application/pdf", profilePhotoResponse.Content.Headers.ContentType?.MediaType);

        var downloadResponse = await client.GetAsync("/api/cv-documents/current/content/original/download");

        Assert.Equal(HttpStatusCode.OK, downloadResponse.StatusCode);
        Assert.Equal("application/pdf", downloadResponse.Content.Headers.ContentType?.MediaType);

        var downloadedBytes = await downloadResponse.Content.ReadAsByteArrayAsync();

        Assert.Equal(pdfBytes, downloadedBytes);
    }

    [Fact]
    public async Task ProfilePhoto_returns_404_when_no_cv_uploaded()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserB);
        var response = await client.GetAsync("/api/cv-documents/current/profile-photo");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static byte[] CreateStructuredCvPdf(bool includeHeadshot)
    {
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;

        using (var graphics = XGraphics.FromPdfPage(page))
        {
            if (includeHeadshot)
            {
                using var image = LoadTinyPngImage();
                graphics.DrawImage(image, 430, 40, 80, 80);
            }

            graphics.DrawString(
                "Experience",
                new XFont("Arial", 14, XFontStyleEx.Bold),
                XBrushes.Black,
                new XPoint(50, 180));
            graphics.DrawString(
                "Software Engineer\nAcme Corp\n2020 – 2024\nBuilt reliable services.",
                new XFont("Arial", 11),
                XBrushes.Black,
                new XPoint(50, 210));
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
