using ApplyVault.Api.Models;
using ApplyVault.Api.Services.Eures;

namespace ApplyVault.Api.Tests;

public sealed class EuresScrapeResultMapperTests
{
    [Fact]
    public void MapToScrapeResult_MapsStructuredFields()
    {
        var detail = CreateDetail(
            id: "job-1",
            title: "Backend Developer",
            employer: "Contoso",
            location: "Copenhagen, DK",
            description: "Build APIs",
            applicationUrl: "https://jobs.example.com/apply");

        var mapped = EuresScrapeResultMapper.MapToScrapeResult(detail);

        Assert.Equal("Backend Developer", mapped.Title);
        Assert.Equal("https://jobs.example.com/apply", mapped.Url);
        Assert.Equal("Build APIs", mapped.Text);
        Assert.Equal("Build APIs".Length, mapped.TextLength);
        Assert.False(string.IsNullOrWhiteSpace(mapped.ExtractedAt));
        Assert.Equal("europa.eu", mapped.JobDetails.SourceHostname);
        Assert.Equal("eures-job", mapped.JobDetails.DetectedPageType);
        Assert.Equal("Backend Developer", mapped.JobDetails.JobTitle);
        Assert.Equal("Contoso", mapped.JobDetails.CompanyName);
        Assert.Equal("Copenhagen, DK", mapped.JobDetails.Location);
        Assert.Equal("Build APIs", mapped.JobDetails.JobDescription);
        Assert.Empty(mapped.JobDetails.HiringManagerContacts);
    }

    [Fact]
    public void ResolveCanonicalUrl_PrefersApplicationUrl()
    {
        var detail = CreateDetail(
            id: "job-1",
            applicationUrl: "https://jobs.example.com/apply",
            sourceUrl: "https://europa.eu/eures/portal/jv/detail/jv?id=job-1");

        var url = EuresScrapeResultMapper.ResolveCanonicalUrl(detail);

        Assert.Equal("https://jobs.example.com/apply", url);
    }

    [Fact]
    public void ResolveCanonicalUrl_FallsBackToSourceUrl()
    {
        var detail = CreateDetail(
            id: "job-1",
            sourceUrl: "https://europa.eu/eures/portal/jv/detail/jv?id=job-1");

        var url = EuresScrapeResultMapper.ResolveCanonicalUrl(detail);

        Assert.Equal("https://europa.eu/eures/portal/jv/detail/jv?id=job-1", url);
    }

    [Fact]
    public void ResolveCanonicalUrl_FallsBackToEuresPortalUrl()
    {
        var detail = CreateDetail(id: "job-42");

        var url = EuresScrapeResultMapper.ResolveCanonicalUrl(detail);

        Assert.Equal("https://europa.eu/eures/portal/jv/detail/jv?id=job-42", url);
    }

    [Fact]
    public void MapToScrapeResult_StripsHtmlFromDescription()
    {
        var detail = CreateDetail(
            id: "job-1",
            description: "<p>Build <strong>APIs</strong> with&nbsp;C#</p>");

        var mapped = EuresScrapeResultMapper.MapToScrapeResult(detail);

        Assert.Equal("Build APIs with C#", mapped.Text);
        Assert.Equal("Build APIs with C#", mapped.JobDetails.JobDescription);
    }

    [Fact]
    public void MapToScrapeResult_UsesFallbackTextWhenDescriptionEmpty()
    {
        var detail = CreateDetail(
            id: "job-1",
            title: "Platform Engineer",
            employer: "Fabrikam",
            location: "Aarhus, DK",
            description: null);

        var mapped = EuresScrapeResultMapper.MapToScrapeResult(detail);

        Assert.Contains("Platform Engineer", mapped.Text, StringComparison.Ordinal);
        Assert.Contains("Employer: Fabrikam", mapped.Text, StringComparison.Ordinal);
        Assert.Contains("Location: Aarhus, DK", mapped.Text, StringComparison.Ordinal);
        Assert.True(mapped.TextLength > 0);
        Assert.Equal(mapped.Text, mapped.JobDetails.JobDescription);
    }

    private static EuresJobDetailResponse CreateDetail(
        string id,
        string? title = "Developer",
        string? employer = "Contoso",
        string? location = "Copenhagen, DK",
        string? description = "Build APIs",
        string? applicationUrl = null,
        string? sourceUrl = null)
    {
        return new EuresJobDetailResponse(
            Id: id,
            Title: title,
            Employer: employer,
            Location: location,
            PublicationDate: "2024-01-15",
            SourceUrl: sourceUrl ?? applicationUrl ?? $"https://europa.eu/eures/portal/jv/detail/jv?id={id}",
            Description: description,
            ApplicationUrl: applicationUrl,
            ContractType: "PERMANENT",
            WorkHours: "FULLTIME");
    }
}
