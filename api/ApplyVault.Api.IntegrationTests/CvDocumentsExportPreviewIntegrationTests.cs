using System.Net;
using System.Net.Http.Headers;
using ApplyVault.Api.Services;
using PdfSharp.Fonts;

namespace ApplyVault.Api.IntegrationTests;

public sealed class CvDocumentsExportPreviewIntegrationTests(ApplyVaultWebApplicationFactory factory)
    : IClassFixture<ApplyVaultWebApplicationFactory>
{
    static CvDocumentsExportPreviewIntegrationTests()
    {
        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.FontResolver = ApplyVaultPdfFontResolver.Instance;
        }
    }

    [Fact]
    public async Task Preview_without_token_returns_401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/cv-documents/current/export/preview");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Preview_returns_not_found_when_no_cv()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserB);
        await UploadCvAsync(client);
        var deleteResponse = await client.DeleteAsync("/api/cv-documents/current");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var response = await client.GetAsync("/api/cv-documents/current/export/preview");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Preview_returns_single_template_html()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        await UploadCvAsync(client);

        var response = await client.GetAsync("/api/cv-documents/current/export/preview");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.Contains("X-Cv-Export-Template-Id"));
        Assert.True(response.Headers.TryGetValues("X-Cv-Export-Compact-Level", out var compactHeaders));
        Assert.True(int.TryParse(Assert.Single(compactHeaders), out var compactLevel));
        Assert.InRange(compactLevel, 0, CvPdfRenderOptions.MaxCompactLevel);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<!DOCTYPE html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cv-body", html, StringComparison.Ordinal);
        Assert.Contains("cv-name", html, StringComparison.Ordinal);
        Assert.DoesNotContain("cv-layout", html, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", html, StringComparison.Ordinal);
        Assert.Contains("jane@example.com", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer ", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Preview_ignores_legacy_templateId_query_parameter()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        await UploadCvAsync(client);

        var response = await client.GetAsync("/api/cv-documents/current/export/preview?templateId=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("X-Cv-Export-Template-Id"));

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("cv-body", html, StringComparison.Ordinal);
        Assert.DoesNotContain("cv-layout", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Download_exports_pdf_with_person_name_file_name()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        await UploadCvAsync(client);

        var response = await client.GetAsync("/api/cv-documents/current/export/download");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.Contains("X-Cv-Export-Template-Id"));
        Assert.True(response.Headers.Contains("X-Cv-Export-Page-Count"));
        Assert.True(response.Headers.Contains("X-Cv-Export-Exceeds-Limit"));

        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ContentDisposition?.FileName;
        Assert.NotNull(fileName);
        Assert.EndsWith("-CV.pdf", fileName.Trim('"'), StringComparison.Ordinal);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 100);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(bytes[..4]));
    }

    private static async Task UploadCvAsync(HttpClient client)
    {
        var pdfBytes = CvDocumentsUploadImportIntegrationTests.CreateStructuredCvPdf(includeHeadshot: false);

        using var form = new MultipartFormDataContent();
        var pdf = new ByteArrayContent(pdfBytes);
        pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(pdf, "file", "cv.pdf");

        var uploadResponse = await client.PostAsync("/api/cv-documents/current", form);
        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
    }
}
