using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using PdfSharp.Fonts;
using UglyToad.PdfPig;

namespace ApplyVault.Api.IntegrationTests;

public sealed class CvDocumentsExportIntegrationTests(ApplyVaultWebApplicationFactory factory)
    : IClassFixture<ApplyVaultWebApplicationFactory>
{
    static CvDocumentsExportIntegrationTests()
    {
        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.FontResolver = ApplyVaultPdfFontResolver.Instance;
        }
    }

    [Fact]
    public async Task Export_download_returns_pdf_with_saved_structured_content()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);

        await UploadCvAsync(client, includeHeadshot: false);

        var original = await GetStructuredAsync(client);

        Assert.NotNull(original);

        var experienceSection = original!.Sections.First((section) =>
            section.SectionType.Equals("Experience", StringComparison.OrdinalIgnoreCase));
        var originalEntryId = experienceSection.Entries[0].Id;

        var updatedSections = original.Sections
            .Select((section, sectionIndex) =>
            {
                var entries = section.Entries
                    .Select((entry, entryIndex) => new CvStructuredEntryWriteDto(
                        entry.Id,
                        section.Id == experienceSection.Id && entryIndex == 0
                            ? "Senior Software Engineer"
                            : entry.Title,
                        entry.Subtitle,
                        entry.DateRange,
                        entry.Summary,
                        entry.Bullets,
                        entry.TechStack,
                        entry.Source,
                        entry.SourceSummaryId,
                        entryIndex))
                    .ToList();

                return new CvStructuredSectionWriteDto(
                    section.Id,
                    section.Heading,
                    section.SectionType,
                    sectionIndex,
                    entries);
            })
            .ToArray();

        var saveResponse = await client.PutAsJsonAsync(
            "/api/cv-documents/current/structured",
            new SaveCvStructuredDocumentRequest(updatedSections));

        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);

        var exportResponse = await client.GetAsync("/api/cv-documents/current/export/download");

        Assert.Equal(HttpStatusCode.OK, exportResponse.StatusCode);
        Assert.Equal("application/pdf", exportResponse.Content.Headers.ContentType?.MediaType);

        var downloadedBytes = await exportResponse.Content.ReadAsByteArrayAsync();
        var extractedText = ExtractPdfText(downloadedBytes);

        Assert.Contains("Senior", extractedText, StringComparison.Ordinal);
        Assert.Contains("Engineer", extractedText, StringComparison.Ordinal);
        Assert.Contains("jane@example.com", extractedText, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(Guid.Empty, originalEntryId);
    }

    [Fact]
    public async Task Export_download_works_without_profile_photo()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserB);

        await UploadCvAsync(client, includeHeadshot: false);

        var exportResponse = await client.GetAsync("/api/cv-documents/current/export/download");

        Assert.Equal(HttpStatusCode.OK, exportResponse.StatusCode);
        Assert.Equal("application/pdf", exportResponse.Content.Headers.ContentType?.MediaType);

        var downloadedBytes = await exportResponse.Content.ReadAsByteArrayAsync();

        Assert.True(downloadedBytes.Length > 100);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(downloadedBytes[..4]));
    }

    [Fact]
    public async Task Export_download_returns_not_found_when_no_cv_uploaded()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserB);
        await UploadCvAsync(client, includeHeadshot: false);

        var deleteResponse = await client.DeleteAsync("/api/cv-documents/current");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var response = await client.GetAsync("/api/cv-documents/current/export/download");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task UploadCvAsync(HttpClient client, bool includeHeadshot)
    {
        var pdfBytes = CvDocumentsUploadImportIntegrationTests.CreateStructuredCvPdf(includeHeadshot);

        using var form = new MultipartFormDataContent();
        var pdf = new ByteArrayContent(pdfBytes);
        pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(pdf, "file", "cv.pdf");

        var uploadResponse = await client.PostAsync("/api/cv-documents/current", form);

        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);
    }

    private static async Task<CvStructuredDocumentDto?> GetStructuredAsync(HttpClient client)
    {
        var getResponse = await client.GetAsync("/api/cv-documents/current/structured");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        return await getResponse.Content.ReadFromJsonAsync<CvStructuredDocumentDto>();
    }

    private static string ExtractPdfText(byte[] pdfBytes)
    {
        using var document = PdfDocument.Open(pdfBytes);

        return string.Join(' ', document.GetPages().SelectMany((page) => page.GetWords()).Select((word) => word.Text));
    }
}
