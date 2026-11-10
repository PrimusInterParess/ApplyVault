using ApplyVault.Api.Models;
using ApplyVault.Api.Services.HtmlExport;

namespace ApplyVault.Api.Tests;

public sealed class CvExportDownloadFileNameTests
{
    [Theory]
    [InlineData("Jane Doe", "Jane-Doe-CV.pdf")]
    [InlineData(null, "CV.pdf")]
    [InlineData("   ", "CV.pdf")]
    [InlineData("A/B <C>", "AB-C-CV.pdf")]
    public void Build_formats_person_name(string? personName, string expected) =>
        Assert.Equal(expected, CvExportDownloadFileName.Build(personName));

    [Fact]
    public void BuildForExport_uses_contact_name()
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

        Assert.Equal("Alex-Rivera-CV.pdf", CvExportDownloadFileName.BuildForExport(structured));
    }

    [Fact]
    public void BuildForExport_falls_back_when_contact_name_missing()
    {
        Assert.Equal("CV.pdf", CvExportDownloadFileName.BuildForExport(structured: null));
    }
}
