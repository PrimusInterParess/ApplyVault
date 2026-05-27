using System.Text;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;

namespace ApplyVault.Api.Tests;

public sealed class CvStructuredExportPreviewTests
{
    [Fact]
    public void MapPreviewRequest_Build_returns_pdf_header()
    {
        var request = new SaveCvStructuredDocumentRequest(
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

        var structured = CvStructuredDocumentService.MapPreviewRequest(Guid.NewGuid(), request);
        var pdfBytes = CvPdfStructuredExportBuilder.Build(structured);

        Assert.NotEmpty(pdfBytes);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdfBytes.AsSpan(0, 4)));
    }
}
