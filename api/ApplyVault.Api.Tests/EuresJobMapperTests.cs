using ApplyVault.Api.Services.Eures;

namespace ApplyVault.Api.Tests;

public sealed class EuresJobMapperTests
{
    [Fact]
    public void MapListing_UsesRequestedLanguageProfile()
    {
        var job = EuresTestData.CreateSearchJob("job-1", "English Title", "English Corp", "English description", "en");
        job.JvProfiles!["da"] = new EuresJobProfilePayload
        {
            Title = "Danish Title",
            Description = "Danish description",
            Employer = new EuresEmployerPayload { Name = "Danish Corp" },
            Locations =
            [
                new EuresLocationPayload
                {
                    CityName = "Copenhagen",
                    CountryCode = "DK"
                }
            ]
        };

        var listing = EuresJobMapper.MapListing(job, "da");

        Assert.Equal("job-1", listing.Id);
        Assert.Equal("Danish Title", listing.Title);
        Assert.Equal("Danish Corp", listing.Employer);
        Assert.Equal("Copenhagen, DK", listing.Location);
        Assert.Equal("2024-01-15", listing.PublicationDate);
        Assert.Contains("europa.eu/eures/portal/jv/detail/jv?id=job-1", listing.SourceUrl);
    }

    [Fact]
    public void MapDetail_ExtractsApplicationUrlFromInstructions()
    {
        var detail = EuresTestData.CreateDetailJob("job-2", "Backend Developer", "Contoso", "Build APIs");

        var mapped = EuresJobMapper.MapDetail(detail, "en");

        Assert.Equal("job-2", mapped.Id);
        Assert.Equal("Backend Developer", mapped.Title);
        Assert.Equal("Contoso", mapped.Employer);
        Assert.Equal("https://jobs.example.com/apply", mapped.ApplicationUrl);
        Assert.Equal("https://jobs.example.com/apply", mapped.SourceUrl);
        Assert.Equal("Build APIs", mapped.Description);
        Assert.Equal("PERMANENT", mapped.ContractType);
        Assert.Equal("FULLTIME", mapped.WorkHours);
    }

    [Fact]
    public void MapListing_FallsBackToLocationMapWhenProfilesHaveNoLocations()
    {
        var job = new EuresSearchJobPayload
        {
            Id = "job-3",
            Title = "Engineer",
            CreationDate = EuresTestData.SampleCreationDate,
            LocationMap = new Dictionary<string, string[]>
            {
                ["dk"] = ["Capital Region"]
            }
        };

        var listing = EuresJobMapper.MapListing(job, "en");

        Assert.Equal("dk Capital Region", listing.Location);
    }
}
