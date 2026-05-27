using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;

namespace ApplyVault.Api.IntegrationTests;

public sealed class CvDocumentsExportPreviewIntegrationTests(ApplyVaultWebApplicationFactory factory)
    : IClassFixture<ApplyVaultWebApplicationFactory>
{
    [Fact]
    public async Task PreviewExport_without_token_returns_401()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/cv-documents/current/export/preview",
            CreatePreviewRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PreviewExport_without_uploaded_cv_returns_400()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserB);
        var response = await client.PostAsJsonAsync(
            "/api/cv-documents/current/export/preview",
            CreatePreviewRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PreviewExport_with_empty_sections_returns_400()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        await UploadMinimalCvAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/cv-documents/current/export/preview",
            new SaveCvStructuredDocumentRequest([]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PreviewExport_with_cv_and_sections_returns_pdf()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        await UploadMinimalCvAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/cv-documents/current/export/preview",
            CreatePreviewRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.NotEmpty(bytes);
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
    }

    private static SaveCvStructuredDocumentRequest CreatePreviewRequest() =>
        new(
        [
            new CvStructuredSectionWriteDto(
                null,
                "Experience",
                CvSectionTypes.Experience,
                0,
                [
                    new CvStructuredEntryWriteDto(
                        null,
                        "Software Engineer",
                        "Acme Corp",
                        "2020 – 2024",
                        "Built reliable services.",
                        ["Delivered features on schedule"],
                        "C#, .NET",
                        CvEntrySources.Manual,
                        null,
                        0)
                ])
        ]);

    private static async Task UploadMinimalCvAsync(HttpClient client)
    {
        using var form = new MultipartFormDataContent();
        var pdf = new ByteArrayContent("%PDF-1.4\n%%EOF"u8.ToArray());
        pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(pdf, "file", "cv.pdf");

        var uploadResponse = await client.PostAsync("/api/cv-documents/current", form);

        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
    }
}
