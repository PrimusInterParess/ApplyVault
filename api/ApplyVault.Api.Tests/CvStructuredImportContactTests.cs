using ApplyVault.Api.Models;
using ApplyVault.Api.Services;

namespace ApplyVault.Api.Tests;

public sealed class CvStructuredImportContactTests
{
    [Fact]
    public void HeuristicParse_SplitsLeadingContactFromProfileSection()
    {
        var sections = CvStructuredImportHeuristic.Parse(
        [
            new CvPdfRawSection(
                "Profile",
                "summary",
                0,
                """
                Jane Doe
                jane@example.com | +45 12 34 56 78
                Experienced software engineer focused on backend systems.
                """)
        ]);

        var contactSection = sections.First((section) =>
            section.Heading.Equals("Contact", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(CvSectionTypes.Contact, contactSection.SectionType);
        Assert.Contains("jane@example.com", contactSection.Entries[0].Bullets, StringComparer.OrdinalIgnoreCase);

        var summarySection = sections.Single((section) => section.SectionType == CvSectionTypes.Summary);

        Assert.Contains(
            "Experienced software engineer",
            summarySection.Entries[0].Summary,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Normalizer_RestoresMissingContactFromSourceAfterAiStyleSummaryOnlyOutput()
    {
        var sections = CvStructuredImportNormalizer.Normalize(
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
                        "Experienced software engineer.",
                        [],
                        string.Empty,
                        CvEntrySources.Import,
                        null,
                        0)
                ])
        ],
        [
            new CvPdfRawSection(
                "Profile",
                "summary",
                0,
                """
                Jane Doe
                jane@example.com
                +45 12 34 56 78
                Experienced software engineer.
                """)
        ]);

        var contactSection = sections.First((section) =>
            section.Heading.Equals("Contact", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(CvSectionTypes.Contact, contactSection.SectionType);
        Assert.Contains("jane@example.com", contactSection.Entries[0].Bullets, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("+45 12 34 56 78", contactSection.Entries[0].Bullets, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Normalizer_RestoresIntoContactType_WhenLegacyCustomContactPresent()
    {
        var sections = CvStructuredImportNormalizer.Normalize(
        [
            new CvStructuredSectionWriteDto(
                null,
                "Contact",
                CvSectionTypes.Custom,
                0,
                [
                    new CvStructuredEntryWriteDto(
                        null,
                        "Jane Doe",
                        null,
                        null,
                        string.Empty,
                        ["jane@example.com"],
                        string.Empty,
                        CvEntrySources.Import,
                        null,
                        0)
                ]),
            new CvStructuredSectionWriteDto(
                null,
                "Summary",
                CvSectionTypes.Summary,
                1,
                [
                    new CvStructuredEntryWriteDto(
                        null,
                        string.Empty,
                        null,
                        null,
                        "Experienced software engineer.",
                        [],
                        string.Empty,
                        CvEntrySources.Import,
                        null,
                        0)
                ])
        ],
        [
            new CvPdfRawSection(
                "Profile",
                "summary",
                0,
                """
                Jane Doe
                jane@example.com
                https://github.com/org/repo
                Experienced software engineer.
                """)
        ]);

        var contactSection = Assert.Single(
            sections,
            (section) => section.Heading.Equals("Contact", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(CvSectionTypes.Contact, contactSection.SectionType);
        Assert.Contains("https://github.com/org/repo", contactSection.Entries[0].Bullets, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryMatchSectionHeading_MatchesContactHeading()
    {
        var matched = CvPdfSectionDetector.TryMatchSectionHeading("Contact", out var normalizedKey);

        Assert.True(matched);
        Assert.Equal("contact", normalizedKey);
    }

    [Fact]
    public void SplitContactTokens_PreservesUrlsWithSlashes()
    {
        var tokens = CvStructuredImportEntrySupport.SplitContactTokens(
            "jane@example.com | https://github.com/PrimusInterParess/ApplyVault | linkedin.com/in/jane-doe");

        Assert.Equal(
            [
                "jane@example.com",
                "https://github.com/PrimusInterParess/ApplyVault",
                "linkedin.com/in/jane-doe"
            ],
            tokens);
    }

    [Fact]
    public void HeuristicParse_PreservesContactUrlsWithSlashes()
    {
        var sections = CvStructuredImportHeuristic.Parse(
        [
            new CvPdfRawSection(
                "Profile",
                "summary",
                0,
                """
                Jane Doe
                jane@example.com | https://github.com/PrimusInterParess/ApplyVault | linkedin.com/in/jane-doe
                Experienced software engineer focused on backend systems.
                """)
        ]);

        var contactSection = sections.First((section) =>
            section.Heading.Equals("Contact", StringComparison.OrdinalIgnoreCase));
        var bullets = contactSection.Entries[0].Bullets;

        Assert.Contains("https://github.com/PrimusInterParess/ApplyVault", bullets, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("linkedin.com/in/jane-doe", bullets, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("https:", bullets, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("PrimusInterParess", bullets, StringComparer.OrdinalIgnoreCase);
    }
}
