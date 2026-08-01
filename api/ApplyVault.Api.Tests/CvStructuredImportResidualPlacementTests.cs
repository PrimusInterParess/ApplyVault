using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using ApplyVault.Api.Services.CvSectionCatalog;

namespace ApplyVault.Api.Tests;

public sealed class CvStructuredImportResidualPlacementTests
{
    [Fact]
    public void Apply_ParksOrphanLinesInAdditionalInformationCatchAll()
    {
        var raw = new[]
        {
            new CvPdfRawSection(
                "Experience",
                "experience",
                0,
                """
                Software Engineer
                Acme Corp
                2020 – 2024
                Built reliable services.
                Orphan hobby line about volunteering abroad.
                """)
        };

        var placed = CvStructuredImportNormalizer.Normalize(
            CvStructuredImportHeuristic.Parse(raw),
            raw);

        // Simulate the orphan not being represented (strip it from structured output).
        var withoutOrphan = placed
            .Select((section) => section with
            {
                Entries = section.Entries
                    .Select((entry) => entry with
                    {
                        Summary = entry.Summary.Replace(
                            "Orphan hobby line about volunteering abroad.",
                            string.Empty,
                            StringComparison.OrdinalIgnoreCase).Trim(),
                        Bullets = entry.Bullets
                            .Where((bullet) =>
                                !bullet.Contains("Orphan hobby", StringComparison.OrdinalIgnoreCase))
                            .ToArray()
                    })
                    .ToArray()
            })
            .ToArray();

        var result = CvStructuredImportResidualPlacement.Apply(withoutOrphan, raw);

        Assert.True(result.ResidualLineCountBeforeSpill >= 1);
        Assert.True(result.UsedCatchAll);

        var catchAll = Assert.Single(
            result.Sections,
            (section) => section.Heading.Equals(
                CvStructuredImportResidualPlacement.CatchAllHeading,
                StringComparison.OrdinalIgnoreCase));

        Assert.Equal(CvSectionTypes.Custom, catchAll.SectionType);
        Assert.Contains(
            catchAll.Entries.SelectMany((entry) => entry.Bullets),
            (bullet) => bullet.Contains("Orphan hobby", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Apply_AppendsResidualToSoftPromotedCustomHeading()
    {
        var raw = new[]
        {
            new CvPdfRawSection(
                "Interests",
                "interests",
                0,
                "Hiking and open-source mentoring on weekends.")
        };

        // Empty Custom shell for the soft-promoted heading (body not yet placed).
        var sections = new[]
        {
            new CvStructuredSectionWriteDto(
                null,
                "Interests",
                CvSectionTypes.Custom,
                0,
                [])
        };

        var result = CvStructuredImportResidualPlacement.Apply(sections, raw);

        Assert.False(result.UsedCatchAll);
        Assert.True(result.ResidualLineCountBeforeSpill >= 1);

        var interests = Assert.Single(result.Sections);
        Assert.Equal("Interests", interests.Heading);
        Assert.Contains(
            interests.Entries.SelectMany((entry) => entry.Bullets),
            (bullet) => bullet.Contains("Hiking", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Apply_DoesNotResplitUrlsInResidualBullets()
    {
        var url = "https://github.com/PrimusInterParess/ApplyVault";
        var raw = new[]
        {
            new CvPdfRawSection("Profile", "summary", 0, $"See also {url} for samples.")
        };

        var result = CvStructuredImportResidualPlacement.Apply([], raw);

        Assert.True(result.UsedCatchAll);
        var catchAll = Assert.Single(result.Sections);
        var joined = string.Join(' ', catchAll.Entries.SelectMany((entry) => entry.Bullets));
        Assert.Contains(url, joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https:", catchAll.Entries.SelectMany((entry) => entry.Bullets));
    }

    [Fact]
    public void SoftHeading_PromotesUnknownTitleCaseOutsideExperience()
    {
        var catalog = CvSectionCatalogProvider.LoadFromDefaultPath();

        Assert.True(
            CvStructuredImportSoftHeading.LooksLikePromotableHeading(
                "Tech stack",
                currentNormalizedKey: "summary",
                catalog));

        Assert.False(
            CvStructuredImportSoftHeading.LooksLikePromotableHeading(
                "Tech stack",
                currentNormalizedKey: "experience",
                catalog));

        Assert.False(
            CvStructuredImportSoftHeading.LooksLikePromotableHeading(
                "https://example.com/path",
                currentNormalizedKey: "summary",
                catalog));
    }
}
