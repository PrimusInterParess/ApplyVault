using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
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

        var structuredResponse = await client.GetAsync("/api/cv-documents/current/structured");

        Assert.Equal(HttpStatusCode.OK, structuredResponse.StatusCode);

        var structured = await structuredResponse.Content.ReadFromJsonAsync<CvStructuredDocumentDto>();

        Assert.NotNull(structured);

        var experienceSection = structured!.Sections.Single((section) =>
            section.SectionType.Equals("Experience", StringComparison.OrdinalIgnoreCase));
        var experienceEntry = Assert.Single(experienceSection.Entries);

        Assert.Equal("Software Engineer", experienceEntry.Title);
        Assert.Equal("Acme Corp", experienceEntry.Subtitle);
        Assert.Equal("2020 – 2024", experienceEntry.DateRange);

        var skillsSection = structured.Sections.Single((section) =>
            section.SectionType.Equals("Skills", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(skillsSection.Entries, (entry) => entry.Title == "Languages");
        Assert.Contains(
            skillsSection.Entries.SelectMany((entry) => entry.Bullets),
            (bullet) => bullet.Equals("English", StringComparison.OrdinalIgnoreCase));

        var summarySection = structured.Sections.Single((section) =>
            section.SectionType.Equals("Summary", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            "Experienced software engineer",
            Assert.Single(summarySection.Entries).Summary,
            StringComparison.OrdinalIgnoreCase);

        var contactSection = structured.Sections.Single((section) =>
            section.Heading.Equals("Contact", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("Custom", contactSection.SectionType, ignoreCase: true);
        Assert.Contains(
            contactSection.Entries.SelectMany((entry) => entry.Bullets),
            (bullet) => bullet.Contains("jane@example.com", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ProfilePhoto_returns_404_when_no_cv_uploaded()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserB);
        var response = await client.GetAsync("/api/cv-documents/current/profile-photo");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Save_structured_updates_sections_and_preserves_stable_ids()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        var pdfBytes = CreateStructuredCvPdf(includeHeadshot: false);

        using var form = new MultipartFormDataContent();
        var pdf = new ByteArrayContent(pdfBytes);
        pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(pdf, "file", "cv.pdf");

        var uploadResponse = await client.PostAsync("/api/cv-documents/current", form);

        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        var getResponse = await client.GetAsync("/api/cv-documents/current/structured");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var original = await getResponse.Content.ReadFromJsonAsync<CvStructuredDocumentDto>();

        Assert.NotNull(original);
        Assert.NotEmpty(original!.Sections);

        var experienceSection = original.Sections.First((section) =>
            section.SectionType.Equals("Experience", StringComparison.OrdinalIgnoreCase));
        var originalEntryId = experienceSection.Entries[0].Id;
        var originalImportedAt = original.StructuredImportedAt;

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

                if (section.Id == experienceSection.Id)
                {
                    entries.Add(new CvStructuredEntryWriteDto(
                        null,
                        "Side Project",
                        "Personal",
                        "2024 – Present",
                        "Built a portfolio tool.",
                        ["Shipped MVP", "Integrated GitHub"],
                        "TypeScript, Angular",
                        "Manual",
                        null,
                        entries.Count));
                }

                return new CvStructuredSectionWriteDto(
                    section.Id,
                    section.Id == experienceSection.Id ? "Work History" : section.Heading,
                    section.SectionType,
                    sectionIndex,
                    entries);
            })
            .ToArray();

        var saveResponse = await client.PutAsJsonAsync(
            "/api/cv-documents/current/structured",
            new SaveCvStructuredDocumentRequest(updatedSections));

        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);

        var saved = await saveResponse.Content.ReadFromJsonAsync<CvStructuredDocumentDto>();

        Assert.NotNull(saved);
        Assert.Equal(originalImportedAt, saved!.StructuredImportedAt);

        var updatedExperience = saved.Sections.Single((section) => section.Id == experienceSection.Id);

        Assert.Equal("Work History", updatedExperience.Heading);
        Assert.Equal(2, updatedExperience.Entries.Count);

        var updatedEntry = updatedExperience.Entries.Single((entry) => entry.Id == originalEntryId);

        Assert.Equal("Senior Software Engineer", updatedEntry.Title);

        var addedEntry = updatedExperience.Entries.Single((entry) => entry.Title == "Side Project");

        Assert.NotEqual(Guid.Empty, addedEntry.Id);
        Assert.Equal(["Shipped MVP", "Integrated GitHub"], addedEntry.Bullets);
    }

    [Fact]
    public async Task Ai_update_updates_structured_sections_with_fake_ai_client()
    {
        using var customFactory = factory.WithWebHostBuilder((builder) =>
        {
            builder.ConfigureTestServices((services) =>
            {
                var descriptors = services
                    .Where((descriptor) => descriptor.ServiceType == typeof(ICvStructuredUpdateAiClient))
                    .ToList();

                foreach (var descriptor in descriptors)
                {
                    services.Remove(descriptor);
                }

                services.AddSingleton<ICvStructuredUpdateAiClient, FakeCvStructuredUpdateAiClient>();
            });
        });
        using var client = customFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestUserTokens.UserA);

        var pdfBytes = CreateStructuredCvPdf(includeHeadshot: false);

        using var form = new MultipartFormDataContent();
        var pdf = new ByteArrayContent(pdfBytes);
        pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(pdf, "file", "cv.pdf");

        var uploadResponse = await client.PostAsync("/api/cv-documents/current", form);

        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        var originalResponse = await client.GetAsync("/api/cv-documents/current/structured");
        var original = await originalResponse.Content.ReadFromJsonAsync<CvStructuredDocumentDto>();

        Assert.NotNull(original);

        var experienceSection = original!.Sections.First((section) =>
            section.SectionType.Equals("Experience", StringComparison.OrdinalIgnoreCase));
        var originalEntryId = experienceSection.Entries[0].Id;

        var updateResponse = await client.PostAsJsonAsync(
            "/api/cv-documents/current/structured/ai-update",
            new UpdateCvStructuredWithAiRequest("Make the main role senior."));

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<CvStructuredDocumentDto>();

        Assert.NotNull(updated);

        var updatedExperience = updated!.Sections.Single((section) => section.Id == experienceSection.Id);
        var updatedEntry = updatedExperience.Entries.Single((entry) => entry.Id == originalEntryId);

        Assert.Equal("Senior Software Engineer", updatedEntry.Title);
        Assert.Equal(CvEntrySources.Manual, updatedEntry.Source);
    }

    [Fact]
    public async Task Reimport_replaces_edited_sections_and_preserves_contact()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        var pdfBytes = CreateStructuredCvPdf(includeHeadshot: false);

        using var form = new MultipartFormDataContent();
        var pdf = new ByteArrayContent(pdfBytes);
        pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(pdf, "file", "cv.pdf");

        var uploadResponse = await client.PostAsync("/api/cv-documents/current", form);

        Assert.Equal(HttpStatusCode.OK, uploadResponse.StatusCode);

        var original = await client.GetAsync("/api/cv-documents/current/structured");

        Assert.Equal(HttpStatusCode.OK, original.StatusCode);

        var structured = await original.Content.ReadFromJsonAsync<CvStructuredDocumentDto>();

        Assert.NotNull(structured);

        var experienceSection = structured!.Sections.First((section) =>
            section.SectionType.Equals("Experience", StringComparison.OrdinalIgnoreCase));

        var editedSections = structured.Sections
            .Select((section, sectionIndex) =>
            {
                var entries = section.Entries
                    .Select((entry, entryIndex) => new CvStructuredEntryWriteDto(
                        entry.Id,
                        section.Id == experienceSection.Id && entryIndex == 0
                            ? "Edited Title Should Be Replaced"
                            : entry.Title,
                        entry.Subtitle,
                        entry.DateRange,
                        entry.Summary,
                        entry.Bullets,
                        entry.TechStack,
                        entry.Source,
                        entry.SourceSummaryId,
                        entryIndex))
                    .ToArray();

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
            new SaveCvStructuredDocumentRequest(editedSections));

        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);

        var reimportResponse = await client.PostAsync(
            "/api/cv-documents/current/structured/reimport",
            null);

        Assert.Equal(HttpStatusCode.OK, reimportResponse.StatusCode);

        var reimportResult = await reimportResponse.Content.ReadFromJsonAsync<CvStructuredReimportResultDto>();

        Assert.NotNull(reimportResult);
        Assert.True(reimportResult!.Import.Succeeded, reimportResult.Import.Notice);

        var reimportedExperience = reimportResult.Structured!.Sections.Single((section) =>
            section.SectionType.Equals("Experience", StringComparison.OrdinalIgnoreCase));

        Assert.Equal("Software Engineer", reimportedExperience.Entries[0].Title);

        var contactSection = reimportResult.Structured.Sections.Single((section) =>
            section.Heading.Equals("Contact", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(
            contactSection.Entries.SelectMany((entry) => entry.Bullets),
            (bullet) => bullet.Contains("jane@example.com", StringComparison.OrdinalIgnoreCase));
    }

    internal static byte[] CreateStructuredCvPdf(bool includeHeadshot)
    {
        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;

        using (var graphics = XGraphics.FromPdfPage(page))
        {
            var headingFont = new XFont("Arial", 14, XFontStyleEx.Bold);
            var bodyFont = new XFont("Arial", 11);

            if (includeHeadshot)
            {
                using var image = LoadTinyPngImage();
                graphics.DrawImage(image, 430, 40, 80, 80);
            }

            var y = 120d;

            graphics.DrawString("Jane Doe", bodyFont, XBrushes.Black, new XPoint(50, y));
            y += 18;
            graphics.DrawString("jane@example.com | +45 12 34 56 78", bodyFont, XBrushes.Black, new XPoint(50, y));
            y += 24;
            graphics.DrawString("Summary", headingFont, XBrushes.Black, new XPoint(50, y));
            y += 24;
            graphics.DrawString(
                "Experienced software engineer focused on reliable backend systems.",
                bodyFont,
                XBrushes.Black,
                new XPoint(50, y));
            y += 36;

            graphics.DrawString("Experience", headingFont, XBrushes.Black, new XPoint(50, y));
            y += 24;
            graphics.DrawString("Software Engineer", bodyFont, XBrushes.Black, new XPoint(50, y));
            y += 18;
            graphics.DrawString("Acme Corp", bodyFont, XBrushes.Black, new XPoint(50, y));
            y += 18;
            graphics.DrawString("2020 – 2024", bodyFont, XBrushes.Black, new XPoint(50, y));
            y += 18;
            graphics.DrawString("Built reliable services.", bodyFont, XBrushes.Black, new XPoint(50, y));
            y += 36;

            graphics.DrawString("Skills", headingFont, XBrushes.Black, new XPoint(50, y));
            y += 24;
            graphics.DrawString("Languages: English, Danish", bodyFont, XBrushes.Black, new XPoint(50, y));
            y += 18;
            graphics.DrawString("Frameworks: .NET, Angular", bodyFont, XBrushes.Black, new XPoint(50, y));
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

    private sealed class FakeCvStructuredUpdateAiClient : ICvStructuredUpdateAiClient
    {
        public Task<SaveCvStructuredDocumentRequest> UpdateAsync(
            CvStructuredDocumentDto current,
            string instructions,
            CancellationToken cancellationToken = default)
        {
            var sections = current.Sections
                .Select((section, sectionIndex) => new CvStructuredSectionWriteDto(
                    section.Id,
                    section.Heading,
                    section.SectionType,
                    sectionIndex,
                    section.Entries
                        .Select((entry, entryIndex) => new CvStructuredEntryWriteDto(
                            entry.Id,
                            section.SectionType.Equals("Experience", StringComparison.OrdinalIgnoreCase) && entryIndex == 0
                                ? "Senior Software Engineer"
                                : entry.Title,
                            entry.Subtitle,
                            entry.DateRange,
                            entry.Summary,
                            entry.Bullets,
                            entry.TechStack,
                            section.SectionType.Equals("Experience", StringComparison.OrdinalIgnoreCase) && entryIndex == 0
                                ? CvEntrySources.Manual
                                : entry.Source,
                            entry.SourceSummaryId,
                            entryIndex))
                        .ToArray()))
                .ToArray();

            return Task.FromResult(new SaveCvStructuredDocumentRequest(sections));
        }
    }
}
