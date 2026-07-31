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

        var response = await client.GetAsync("/api/cv-documents/current/export/preview?templateId=2");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Preview_returns_not_found_when_no_cv()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserB);
        await UploadCvAsync(client);
        var deleteResponse = await client.DeleteAsync("/api/cv-documents/current");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var response = await client.GetAsync("/api/cv-documents/current/export/preview?templateId=2");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Preview_returns_html_for_each_template_id(int templateId)
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        await UploadCvAsync(client);

        var response = await client.GetAsync($"/api/cv-documents/current/export/preview?templateId={templateId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.TryGetValues("X-Cv-Export-Template-Id", out var templateHeaders));
        Assert.Equal(templateId.ToString(), Assert.Single(templateHeaders));

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<!DOCTYPE html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("jane@example.com", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer ", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Preview_legacy_template_id_4_normalizes_to_Modern()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        await UploadCvAsync(client);

        var response = await client.GetAsync("/api/cv-documents/current/export/preview?templateId=4");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Cv-Export-Template-Id", out var templateHeaders));
        Assert.Equal("2", Assert.Single(templateHeaders));

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("cv-layout", html, StringComparison.Ordinal);
        Assert.Contains("cv-name", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Download_exports_pdf_for_template_ids_including_legacy(int templateId)
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        await UploadCvAsync(client);

        var response = await client.GetAsync($"/api/cv-documents/current/export/download?templateId={templateId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.TryGetValues("X-Cv-Export-Template-Id", out var templateHeaders));

        var expectedResolved = templateId is 2 or 3 ? templateId : 2;
        Assert.Equal(expectedResolved.ToString(), Assert.Single(templateHeaders));

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
