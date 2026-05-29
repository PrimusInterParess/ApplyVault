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
    public void MapDetail_PreservesSafeHtmlStructure()
    {
        var detail = JobnetTestData.CreateDetailJob(
            "Backend Developer",
            "Contoso A/S",
            "<p>Build <strong>APIs</strong></p><ul><li>C#</li></ul>");

        var mapped = JobnetJobMapper.MapDetail("job-3", detail);

        Assert.Contains("<p>", mapped.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<strong>", mapped.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<ul>", mapped.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<li>", mapped.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Build", mapped.Description);
        Assert.Contains("APIs", mapped.Description);
        Assert.Contains("C#", mapped.Description);
    }

    [Fact]
    public void MapDetail_RemovesUnsafeHtml()
    {
        var detail = JobnetTestData.CreateDetailJob(
            "Backend Developer",
            "Contoso A/S",
            "<p>Safe</p><script>alert('x')</script><iframe src=\"evil\"></iframe>");

        var mapped = JobnetJobMapper.MapDetail("job-4", detail);

        Assert.Contains("<p>", mapped.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Safe", mapped.Description);
        Assert.DoesNotContain("script", mapped.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("iframe", mapped.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert", mapped.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MapDetailFromSearch_PreservesSafeHtmlStructure()
    {
        var job = JobnetTestData.CreateSearchJob(
            "E10990623",
            "Developer",
            "Contoso",
            "<p>Build <strong>APIs</strong></p><ul><li>C#</li></ul>");

        var mapped = JobnetJobMapper.MapDetailFromSearch("E10990623", job);

        Assert.Contains("<p>", mapped.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<strong>", mapped.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<ul>", mapped.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<li>", mapped.Description, StringComparison.OrdinalIgnoreCase);
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
