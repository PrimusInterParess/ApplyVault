using ApplyVault.Api.Services;
using ApplyVault.Api.Services.CvSectionCatalog;

namespace ApplyVault.Api.Tests;

public sealed class ContactOnlyCvParserTests
{
    private static readonly ICvSectionCatalog Catalog = CvSectionCatalogProvider.LoadFromDefaultPath();

    [Fact]
    public void Parse_PlacesContactAndLeavesRestUnplaced()
    {
        var (sections, unplaced) = ContactOnlyCvParser.Parse(
            [
                "Jane Doe",
                "jane@example.com",
                "+45 12 34 56 78",
                "https://github.com/jane",
                "Software Engineer at Acme",
                "Built reliable services."
            ],
            Catalog);

        Assert.Equal(7, sections.Count);
        var contact = sections.Single((section) => section.SectionType == CvSectionTypes.Contact);
        Assert.Equal("Jane Doe", contact.Entries.Single((entry) => entry.Title == "Name").Subtitle);
        Assert.Contains("jane@example.com", contact.Entries.SelectMany((entry) => entry.Bullets));
        Assert.Contains(unplaced, (line) => line.Contains("Acme", StringComparison.Ordinal));
        Assert.Contains(unplaced, (line) => line.Contains("reliable", StringComparison.Ordinal));
    }
}
