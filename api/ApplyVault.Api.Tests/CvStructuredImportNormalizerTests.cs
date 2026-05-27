using ApplyVault.Api.Models;
using ApplyVault.Api.Services;

namespace ApplyVault.Api.Tests;

public sealed class CvStructuredImportNormalizerTests
{
    [Fact]
    public void Normalize_MovesEmbeddedDateFromTitleToDateRange()
    {
        var sections = CvStructuredImportNormalizer.Normalize(
        [
            new CvStructuredSectionWriteDto(
                null,
                "Experience",
                CvSectionTypes.Experience,
                0,
                [
                    new CvStructuredEntryWriteDto(
                        null,
                        "Software Engineer 2020 – 2024",
                        "Acme Corp",
                        null,
                        string.Empty,
                        [],
                        string.Empty,
                        CvEntrySources.Import,
                        null,
                        0)
                ])
        ]);

        var entry = Assert.Single(Assert.Single(sections).Entries);

        Assert.Equal("Software Engineer", entry.Title);
        Assert.Equal("2020 – 2024", entry.DateRange);
    }

    [Fact]
    public void Normalize_StripsBulletMarkersFromBullets()
    {
        var sections = CvStructuredImportNormalizer.Normalize(
        [
            new CvStructuredSectionWriteDto(
                null,
                "Experience",
                CvSectionTypes.Experience,
                0,
                [
                    new CvStructuredEntryWriteDto(
                        null,
                        "Engineer",
                        null,
                        null,
                        string.Empty,
                        ["- Built APIs", "• Improved tests"],
                        string.Empty,
                        CvEntrySources.Import,
                        null,
                        0)
                ])
        ]);

        var entry = Assert.Single(Assert.Single(sections).Entries);

        Assert.Equal(["Built APIs", "Improved tests"], entry.Bullets);
    }

    [Fact]
    public void Normalize_PromotesSkillsSummaryToBullets()
    {
        var sections = CvStructuredImportNormalizer.Normalize(
        [
            new CvStructuredSectionWriteDto(
                null,
                "Skills",
                CvSectionTypes.Skills,
                0,
                [
                    new CvStructuredEntryWriteDto(
                        null,
                        "Skills",
                        null,
                        null,
                        "C#, .NET, Angular",
                        [],
                        string.Empty,
                        CvEntrySources.Import,
                        null,
                        0)
                ])
        ]);

        var entry = Assert.Single(Assert.Single(sections).Entries);

        Assert.Equal(["C#", ".NET", "Angular"], entry.Bullets);
        Assert.Equal(string.Empty, entry.Summary);
    }

    [Fact]
    public void Normalize_UsesSourceHintWhenAiReturnsCustomSectionType()
    {
        var sections = CvStructuredImportNormalizer.Normalize(
        [
            new CvStructuredSectionWriteDto(
                null,
                "Work Experience",
                CvSectionTypes.Custom,
                0,
                [
                    new CvStructuredEntryWriteDto(
                        null,
                        "Engineer",
                        "Acme Corp",
                        "2020 – 2024",
                        string.Empty,
                        [],
                        string.Empty,
                        CvEntrySources.Import,
                        null,
                        0)
                ])
        ],
        [
            new CvPdfRawSection("Work Experience", "work experience", 0, "Engineer\nAcme Corp\n2020 – 2024")
        ]);

        var section = Assert.Single(sections);

        Assert.Equal(CvSectionTypes.Experience, section.SectionType);
    }

    [Fact]
    public void Normalize_DropsEmptyEntriesAndReindexesSortOrder()
    {
        var sections = CvStructuredImportNormalizer.Normalize(
        [
            new CvStructuredSectionWriteDto(
                null,
                "Experience",
                CvSectionTypes.Experience,
                5,
                [
                    new CvStructuredEntryWriteDto(
                        null,
                        string.Empty,
                        null,
                        null,
                        string.Empty,
                        [],
                        string.Empty,
                        CvEntrySources.Import,
                        null,
                        9),
                    new CvStructuredEntryWriteDto(
                        null,
                        "Engineer",
                        null,
                        null,
                        string.Empty,
                        [],
                        string.Empty,
                        CvEntrySources.Import,
                        null,
                        2)
                ])
        ]);

        var section = Assert.Single(sections);

        Assert.Equal(0, section.SortOrder);

        var entry = Assert.Single(section.Entries);

        Assert.Equal("Engineer", entry.Title);
        Assert.Equal(0, entry.SortOrder);
    }
}
