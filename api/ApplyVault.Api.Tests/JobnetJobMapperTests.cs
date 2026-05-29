using ApplyVault.Api.Services.Jobnet;

namespace ApplyVault.Api.Tests;

public sealed class JobnetJobMapperTests
{
    [Fact]
    public void MapListing_BuildsJobnetSourceUrlWhenMissingJobAdUrl()
    {
        var job = JobnetTestData.CreateSearchJob("job-1", "Backend Developer", "Contoso A/S");

        var listing = JobnetJobMapper.MapListing(job, workInDenmark: true);

        Assert.Equal("job-1", listing.Id);
        Assert.Equal("Backend Developer", listing.Title);
        Assert.Equal("Contoso A/S", listing.Employer);
        Assert.True(listing.WorkInDenmark);
        Assert.Contains("jobAdId=job-1", listing.SourceUrl);
    }

    [Fact]
    public void MapDetail_ExtractsApplicationUrlAndWorkInDenmarkFlag()
    {
        var detail = JobnetTestData.CreateDetailJob(
            "Backend Developer",
            "Contoso A/S",
            "<p>Build APIs</p>",
            workInDenmark: true,
            applicationUrl: "https://jobs.example.com/apply");

        var mapped = JobnetJobMapper.MapDetail("job-2", detail);

        Assert.Equal("job-2", mapped.Id);
        Assert.Equal("Backend Developer", mapped.Title);
        Assert.Equal("Contoso A/S", mapped.Employer);
        Assert.Equal("https://jobs.example.com/apply", mapped.ApplicationUrl);
        Assert.True(mapped.WorkInDenmark);
        Assert.Contains("Build APIs", mapped.Description);
    }

    [Fact]
    public void SupportsNativeDetailEndpoint_OnlyAcceptsGuidIds()
    {
        Assert.True(JobnetJobIdentifiers.SupportsNativeDetailEndpoint("b2b58b21-1353-47c7-afdb-5bb1ff15fd5a"));
        Assert.False(JobnetJobIdentifiers.SupportsNativeDetailEndpoint("E10990623"));
    }

    [Fact]
    public void MapDetailFromSearch_UsesSearchPayloadFields()
    {
        var job = JobnetTestData.CreateSearchJob(
            "E10990623",
            "Developer",
            "Contoso",
            "Build APIs",
            jobAdUrl: "https://jobs.example.com/apply");

        var mapped = JobnetJobMapper.MapDetailFromSearch("E10990623", job);

        Assert.Equal("E10990623", mapped.Id);
        Assert.Equal("Developer", mapped.Title);
        Assert.Equal("https://jobs.example.com/apply", mapped.ApplicationUrl);
        Assert.True(mapped.WorkInDenmark);
    }

    [Fact]
    public void HasWorkInDenmarkClassification_IsCaseInsensitive()
    {
        Assert.True(JobnetJobMapper.HasWorkInDenmarkClassification(["workindenmark"]));
        Assert.False(JobnetJobMapper.HasWorkInDenmarkClassification(["EURES"]));
    }
}
