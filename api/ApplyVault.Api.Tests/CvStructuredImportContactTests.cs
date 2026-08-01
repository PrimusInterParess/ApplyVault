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
        Assert.Equal("Name", contactSection.Entries[0].Title, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Jane Doe", contactSection.Entries[0].Subtitle);
        Assert.Contains("jane@example.com", contactSection.Entries[0].Bullets, StringComparer.OrdinalIgnoreCase);

        var summarySection = sections.Single((section) => section.SectionType == CvSectionTypes.Summary);

        Assert.Contains(
            "Experienced software engineer",
            summarySection.Entries[0].Summary,
            StringComparison.OrdinalIgnoreCase);
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
        Assert.Equal("Name", contactSection.Entries[0].Title, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Jane Doe", contactSection.Entries[0].Subtitle);
        Assert.DoesNotContain("https:", bullets, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("PrimusInterParess", bullets, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void HeuristicParse_KeepsStreetAddressAtomic()
    {
        var sections = CvStructuredImportHeuristic.Parse(
        [
            new CvPdfRawSection(
                "Profile",
                "summary",
                0,
                """
                Jane Doe
                jane@example.com
                Address: Fruenshave 24, 8541 Skødstrup, Denmark
                Experienced software engineer focused on backend systems.
                """)
        ]);

        var contactSection = sections.First((section) =>
            section.Heading.Equals("Contact", StringComparison.OrdinalIgnoreCase));
        var allBullets = contactSection.Entries.SelectMany((entry) => entry.Bullets).ToArray();

        Assert.Contains(
            "Address: Fruenshave 24, 8541 Skødstrup, Denmark",
            allBullets,
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Normalizer_ExtractsContactFromSummaryWhenMixedInSummaryProse()
    {
        // Light structural split only — not restore/evaluation against source.
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
                        """
                        Jane Doe
                        jane@example.com
                        Experienced software engineer.
                        """,
                        [],
                        string.Empty,
                        CvEntrySources.Import,
                        null,
                        0)
                ])
        ]);

        Assert.Contains(
            sections,
            (section) => section.SectionType.Equals(CvSectionTypes.Contact, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            sections,
            (section) => section.SectionType.Equals(CvSectionTypes.Summary, StringComparison.OrdinalIgnoreCase)
                && section.Entries[0].Summary.Contains("Experienced", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Normalizer_ReshapesAiContactSummaryIntoSubtitleAndBullets()
    {
        var sections = CvStructuredImportNormalizer.Normalize(
        [
            new CvStructuredSectionWriteDto(
                null,
                "Contact",
                CvSectionTypes.Contact,
                0,
                [
                    new CvStructuredEntryWriteDto(
                        null, "Name", null, null, "Yordan Borisov", [], string.Empty, CvEntrySources.Import, null, 0),
                    new CvStructuredEntryWriteDto(
                        null, "Email", null, null, "diesonnekind@gmail.com", [], string.Empty, CvEntrySources.Import, null, 1),
                    new CvStructuredEntryWriteDto(
                        null, "Phone", null, null, "+45 36 21 63 02", [], string.Empty, CvEntrySources.Import, null, 2),
                    new CvStructuredEntryWriteDto(
                        null, "Address", null, null, "Fruenshave 24, 8541 Skødstrup, Danmark", [], string.Empty, CvEntrySources.Import, null, 3),
                    new CvStructuredEntryWriteDto(
                        null, "LinkedIn", null, null, "www.linkedin.com/in/yordan-dani-borisov-3b38a2239", [], string.Empty, CvEntrySources.Import, null, 4),
                    new CvStructuredEntryWriteDto(
                        null, "GitHub", null, null, "github.com/PrimusInterParess", [], string.Empty, CvEntrySources.Import, null, 5)
                ])
        ]);

        var contact = Assert.Single(sections, (section) => section.SectionType == CvSectionTypes.Contact);
        var name = Assert.Single(contact.Entries, (entry) => entry.Title.Equals("Name", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Yordan Borisov", name.Subtitle);
        Assert.Equal(string.Empty, name.Summary);
        Assert.Empty(name.Bullets);

        var email = Assert.Single(contact.Entries, (entry) => entry.Title.Equals("Email", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(["diesonnekind@gmail.com"], email.Bullets);
        Assert.Equal(string.Empty, email.Summary);

        var address = Assert.Single(contact.Entries, (entry) => entry.Title.Equals("Address", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(["Fruenshave 24, 8541 Skødstrup, Danmark"], address.Bullets);
    }

    [Fact]
    public void Normalizer_DropsSectionHeadingMisfiledAsContactName()
    {
        var sections = CvStructuredImportNormalizer.Normalize(
        [
            new CvStructuredSectionWriteDto(
                null,
                "Contact",
                CvSectionTypes.Contact,
                0,
                [
                    new CvStructuredEntryWriteDto(
                        null,
                        "Name",
                        null,
                        null,
                        "SUMMARY",
                        [".NET Developer with over 2 years of experience."],
                        string.Empty,
                        CvEntrySources.Import,
                        null,
                        0),
                    new CvStructuredEntryWriteDto(
                        null, "Phone", null, null, string.Empty, ["+359 88 348 3311"], string.Empty, CvEntrySources.Import, null, 1)
                ])
        ]);

        var contact = Assert.Single(sections, (section) => section.SectionType == CvSectionTypes.Contact);
        var name = Assert.Single(contact.Entries, (entry) => entry.Title.Equals("Name", StringComparison.OrdinalIgnoreCase));
        Assert.Null(name.Subtitle);
        Assert.Equal(string.Empty, name.Summary);
        Assert.Empty(name.Bullets);

        var phone = Assert.Single(contact.Entries, (entry) => entry.Title.Equals("Phone", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(["+359 88 348 3311"], phone.Bullets);
    }

    [Fact]
    public void Normalizer_DropsInventedSentenceFragmentContactName()
    {
        var sections = CvStructuredImportNormalizer.Normalize(
        [
            new CvStructuredSectionWriteDto(
                null,
                "Contact",
                CvSectionTypes.Contact,
                0,
                [
                    new CvStructuredEntryWriteDto(
                        null, "Name", "A mother of three", null, string.Empty, [], string.Empty, CvEntrySources.Import, null, 0),
                    new CvStructuredEntryWriteDto(
                        null, "Phone", null, null, string.Empty, ["+359 88 348 3311"], string.Empty, CvEntrySources.Import, null, 1)
                ])
        ]);

        var contact = Assert.Single(sections, (section) => section.SectionType == CvSectionTypes.Contact);
        var name = Assert.Single(contact.Entries, (entry) => entry.Title.Equals("Name", StringComparison.OrdinalIgnoreCase));
        Assert.Null(name.Subtitle);
        Assert.Equal(string.Empty, name.Summary);
    }

    [Fact]
    public void ContactGrounding_RecoversPersonNameWhenAiUsedJobTitle()
    {
        var sections = CvStructuredImportNormalizer.Normalize(
        [
            new CvStructuredSectionWriteDto(
                null,
                "Contact",
                CvSectionTypes.Contact,
                0,
                [
                    new CvStructuredEntryWriteDto(
                        null, "Name", "Software Developer", null, string.Empty, [], string.Empty, CvEntrySources.Import, null, 0),
                    new CvStructuredEntryWriteDto(
                        null, "Email", null, null, string.Empty, ["diesonnekind@gmail.com"], string.Empty, CvEntrySources.Import, null, 1)
                ])
        ]);

        sections = CvStructuredImportContactGrounding.FilterToSource(
            sections,
            """
            CONTACT
            Yordan Borisov
            Software Developer
            Email: diesonnekind@gmail.com
            """);

        var contact = Assert.Single(sections, (section) => section.SectionType == CvSectionTypes.Contact);
        var name = Assert.Single(contact.Entries, (entry) => entry.Title.Equals("Name", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Yordan Borisov", name.Subtitle);
    }

    [Fact]
    public void ContactGrounding_DropsValuesAbsentFromSource()
    {
        var sections = CvStructuredImportContactGrounding.FilterToSource(
            [
                new CvStructuredSectionWriteDto(
                    null,
                    "Contact",
                    CvSectionTypes.Contact,
                    0,
                    [
                        new CvStructuredEntryWriteDto(
                            null, "Name", "Invented Person", null, string.Empty, [], string.Empty, CvEntrySources.Import, null, 0),
                        new CvStructuredEntryWriteDto(
                            null, "Phone", null, null, string.Empty, ["+359 88 348 3311"], string.Empty, CvEntrySources.Import, null, 1),
                        new CvStructuredEntryWriteDto(
                            null, "Email", null, null, string.Empty, ["missing@example.com"], string.Empty, CvEntrySources.Import, null, 2)
                    ])
            ],
            """
            +359 88 348 3311
            SUMMARY
            .NET Developer
            """);

        var contact = Assert.Single(sections);
        Assert.DoesNotContain(contact.Entries, (entry) => entry.Title.Equals("Name", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(entry.Subtitle));
        Assert.Contains(contact.Entries, (entry) =>
            entry.Title.Equals("Phone", StringComparison.OrdinalIgnoreCase)
            && entry.Bullets.Any((b) => b.Contains("359", StringComparison.Ordinal)));
        Assert.DoesNotContain(contact.Entries, (entry) =>
            entry.Title.Equals("Email", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NormalizeExtractedText_FixesLigaturesAndNuls()
    {
        var normalized = CvPdfFullTextExtractor.NormalizeExtractedText("So\uFB01a\0, Bulgaria");
        Assert.Equal("Sofia, Bulgaria", normalized);
    }
}
