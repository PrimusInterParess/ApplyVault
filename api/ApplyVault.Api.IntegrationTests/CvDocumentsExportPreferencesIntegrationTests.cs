using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using PdfSharp.Fonts;

namespace ApplyVault.Api.IntegrationTests;

public sealed class CvDocumentsExportPreferencesIntegrationTests(ApplyVaultWebApplicationFactory factory)
    : IClassFixture<ApplyVaultWebApplicationFactory>
{
    static CvDocumentsExportPreferencesIntegrationTests()
    {
        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.FontResolver = ApplyVaultPdfFontResolver.Instance;
        }
    }

    [Fact]
    public async Task Export_preferences_round_trip_on_current_document()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        await UploadCvAsync(client);

        var getBefore = await client.GetAsync("/api/cv-documents/current");
        Assert.Equal(HttpStatusCode.OK, getBefore.StatusCode);
        var before = await getBefore.Content.ReadFromJsonAsync<CvDocumentDto>();
        Assert.NotNull(before);
        Assert.Equal(2, before!.TemplateId);
        Assert.Null(before.MaxPages);

        var putResponse = await client.PutAsJsonAsync(
            "/api/cv-documents/current/export-preferences",
            new CvExportPreferencesDto(2, 3));
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var putBody = await putResponse.Content.ReadFromJsonAsync<CvDocumentDto>();
        Assert.NotNull(putBody);
        Assert.Equal(2, putBody!.TemplateId);
        Assert.Equal(3, putBody.MaxPages);

        var getAfter = await client.GetAsync("/api/cv-documents/current");
        Assert.Equal(HttpStatusCode.OK, getAfter.StatusCode);
        var after = await getAfter.Content.ReadFromJsonAsync<CvDocumentDto>();
        Assert.NotNull(after);
        Assert.Equal(2, after!.TemplateId);
        Assert.Equal(3, after.MaxPages);
    }

    [Fact]
    public async Task Export_preferences_normalize_legacy_template_id_on_write()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        await UploadCvAsync(client);

        var putResponse = await client.PutAsJsonAsync(
            "/api/cv-documents/current/export-preferences",
            new CvExportPreferencesDto(4, null));
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var putBody = await putResponse.Content.ReadFromJsonAsync<CvDocumentDto>();
        Assert.NotNull(putBody);
        Assert.Equal(2, putBody!.TemplateId);
        Assert.Null(putBody.MaxPages);
    }

    [Fact]
    public async Task Export_preferences_reject_invalid_max_pages()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        await UploadCvAsync(client);

        var putResponse = await client.PutAsJsonAsync(
            "/api/cv-documents/current/export-preferences",
            new CvExportPreferencesDto(2, 9));

        Assert.Equal(HttpStatusCode.BadRequest, putResponse.StatusCode);
    }

    [Fact]
    public async Task Export_preferences_are_isolated_per_user()
    {
        using var clientA = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        using var clientB = factory.CreateAuthenticatedClient(TestUserTokens.UserB);
        await UploadCvAsync(clientA);
        await UploadCvAsync(clientB);

        var putA = await clientA.PutAsJsonAsync(
            "/api/cv-documents/current/export-preferences",
            new CvExportPreferencesDto(3, 2));
        Assert.Equal(HttpStatusCode.OK, putA.StatusCode);

        var putB = await clientB.PutAsJsonAsync(
            "/api/cv-documents/current/export-preferences",
            new CvExportPreferencesDto(2, 4));
        Assert.Equal(HttpStatusCode.OK, putB.StatusCode);

        var getA = await clientA.GetFromJsonAsync<CvDocumentDto>("/api/cv-documents/current");
        var getB = await clientB.GetFromJsonAsync<CvDocumentDto>("/api/cv-documents/current");

        Assert.NotNull(getA);
        Assert.NotNull(getB);
        Assert.Equal(3, getA!.TemplateId);
        Assert.Equal(2, getA.MaxPages);
        Assert.Equal(2, getB!.TemplateId);
        Assert.Equal(4, getB.MaxPages);
    }

    [Fact]
    public async Task Preview_uses_stored_template_when_query_omitted()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        await UploadCvAsync(client);

        var putResponse = await client.PutAsJsonAsync(
            "/api/cv-documents/current/export-preferences",
            new CvExportPreferencesDto(3, null));
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var preview = await client.GetAsync("/api/cv-documents/current/export/preview");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.True(preview.Headers.TryGetValues("X-Cv-Export-Template-Id", out var templateHeaders));
        Assert.Equal("3", Assert.Single(templateHeaders));
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
