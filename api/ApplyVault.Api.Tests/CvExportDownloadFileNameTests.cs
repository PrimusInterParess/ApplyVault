using ApplyVault.Api.Models;
using ApplyVault.Api.Services.HtmlExport;

namespace ApplyVault.Api.Tests;

public sealed class CvExportDownloadFileNameTests
{
    [Theory]
    [InlineData("Jane Doe", "Modern", "Jane-Doe-Modern.pdf")]
    [InlineData("Jane Doe", "Minimal", "Jane-Doe-Minimal.pdf")]
    [InlineData(null, "Modern", "CV-Modern.pdf")]
    [InlineData("   ", "Minimal", "CV-Minimal.pdf")]
    [InlineData("A/B <C>", "Modern", "AB-C-Modern.pdf")]
    public void Build_formats_person_and_template(string? personName, string templateLabel, string expected) =>
        Assert.Equal(expected, CvExportDownloadFileName.Build(personName, templateLabel));

    [Fact]
    public void BuildForExport_uses_contact_name_and_template_display_name()
    {
        var structured = new CvStructuredDocumentDto(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            [
                new CvStructuredSectionDto(
                    Guid.NewGuid(),
                    "Contact",
                    "Contact",
                    0,
                    [
                        new CvStructuredEntryDto(
                            Guid.NewGuid(),
                            "Name",
                            "Alex Rivera",
                            null,
                            string.Empty,
                            [],
                            string.Empty,
                            new Dictionary<string, object?>(),
                            "manual",
                            null,
                            0)
                    ])
            ]);

        Assert.Equal(
            "Alex-Rivera-Modern.pdf",
            CvExportDownloadFileName.BuildForExport(structured, templateId: 2));
    }

    [Fact]
    public void BuildForExport_falls_back_when_contact_name_missing()
    {
        Assert.Equal(
            "CV-Modern.pdf",
            CvExportDownloadFileName.BuildForExport(structured: null, templateId: 1));
    }
}
