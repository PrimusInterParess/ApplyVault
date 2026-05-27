using ApplyVault.Api.Models;
using ApplyVault.Api.Services;

namespace ApplyVault.Api.Tests;

public sealed class CvStructuredImportCoverageAuditTests
{
    [Fact]
    public void BuildNotice_AppendsWarningWhenSourceLinesAreMissing()
    {
        var notice = CvStructuredImportCoverageAudit.BuildNotice(
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
        ],
        null);

        Assert.NotNull(notice);
        Assert.Contains("may not have been imported", notice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildNotice_PreservesExistingNotice()
    {
        var notice = CvStructuredImportCoverageAudit.BuildNotice(
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
        ],
        "Google AI is disabled; a basic structure was generated.");

        Assert.Contains("Google AI is disabled", notice);
    }
}
