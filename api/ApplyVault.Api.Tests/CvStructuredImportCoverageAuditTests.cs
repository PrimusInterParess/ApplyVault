using ApplyVault.Api.Models;
using ApplyVault.Api.Services;

namespace ApplyVault.Api.Tests;

public sealed class CvStructuredImportCoverageAuditTests
{
    [Fact]
    public void CountMissingSourceLines_DetectsUnmappedLinesForDiagnostics()
    {
        var missing = CvStructuredImportCoverageAudit.CountMissingSourceLines(
        [
            new CvPdfRawSection(
                "Profile",
                "summary",
                0,
                """
                jane@example.com
                Experienced software engineer focused on backend systems.
                Unmapped certification line from source PDF
                """)
        ],
        [
            new CvStructuredSectionWriteDto(
                null,
                "Summary",
                CvSectionTypes.Summary,
                0,
                [
                    new CvStructuredEntryWriteDto(
                        null,
                        string.Empty,
                        null,
                        null,
                        "Experienced software engineer focused on backend systems.",
                        [],
                        string.Empty,
                        CvEntrySources.Import,
                        null,
                        0)
                ])
        ]);

        Assert.True(missing > 0);
    }

    [Fact]
    public void CountMissingSourceLines_ReturnsZeroWhenLinesAreRepresented()
    {
        var missing = CvStructuredImportCoverageAudit.CountMissingSourceLines(
        [
            new CvPdfRawSection("Summary", "summary", 0, "Experienced software engineer focused on backend.")
        ],
        [
            new CvStructuredSectionWriteDto(
                null,
                "Summary",
                CvSectionTypes.Summary,
                0,
                [
                    new CvStructuredEntryWriteDto(
                        null,
                        string.Empty,
                        null,
                        null,
                        "Experienced software engineer focused on backend.",
                        [],
                        string.Empty,
                        CvEntrySources.Import,
                        null,
                        0)
                ])
        ]);

        Assert.Equal(0, missing);
    }
}
