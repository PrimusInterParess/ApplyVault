using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services.Jobnet;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Tests;

public sealed class JobnetJobDetailComposerTests
{
    [Fact]
    public async Task ComposeAsync_ReturnsFullDescription_ForNativeGuidListing()
    {
        var detail = JobnetTestData.CreateDetailJob(
            "Backend Developer",
            "Contoso A/S",
            "<p>Build <strong>APIs</strong></p><p>Ship reliable services across teams and mentor junior engineers.</p>",
            workInDenmark: true);

        var composer = CreateComposer(
            new JobnetRawDetail(
                JobnetDescriptionSource.NativeDetail,
                JobnetJobMapper.MapDetail("b2b58b21-1353-47c7-afdb-5bb1ff15fd5a", detail)));

        var response = await composer.ComposeAsync("b2b58b21-1353-47c7-afdb-5bb1ff15fd5a", CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(JobnetDescriptionQualityValues.QualityFull, response.DescriptionQuality);
        Assert.Equal(JobnetDescriptionQualityValues.SourceNativeDetail, response.DescriptionSource);
        Assert.Contains("<p>", response.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ComposeAsync_ReturnsPreviewOnly_ForExternalListing()
    {
        var mapped = JobnetJobMapper.MapDetailFromSearch(
            "E10990623",
            JobnetTestData.CreateSearchJob(
                "E10990623",
                "Developer — Responsive",
                "RESPONSIVE A/S",
                JobnetJobDetailTestData.ScrapedExternalDescription,
                jobAdUrl: "https://responsive.dk/about-us/developer/"));

        var composer = CreateComposer(
            new JobnetRawDetail(JobnetDescriptionSource.SearchFallback, mapped));

        var response = await composer.ComposeAsync("E10990623", CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(JobnetDescriptionQualityValues.QualityPreviewOnly, response.DescriptionQuality);
        Assert.Equal(JobnetDescriptionQualityValues.SourceSearchFallback, response.DescriptionSource);
        Assert.Null(response.Description);
        Assert.False(string.IsNullOrWhiteSpace(response.DescriptionExcerpt));
    }

    private static JobnetJobDetailComposer CreateComposer(JobnetRawDetail rawDetail)
    {
        var strategy = new StubFetchStrategy(_ => true, (_, _) => Task.FromResult<JobnetRawDetail?>(rawDetail));
        var resolver = new JobnetJobDetailResolver([strategy]);
        var options = Microsoft.Extensions.Options.Options.Create(new JobnetIntegrationOptions { WorkInDenmarkOnly = true });

        return new JobnetJobDetailComposer(resolver, new JobnetDescriptionQualityAssessor(), options);
    }

    private sealed class StubFetchStrategy(
        Func<string, bool> canHandle,
        Func<string, CancellationToken, Task<JobnetRawDetail?>> fetch) : IJobnetJobDetailFetchStrategy
    {
        public bool CanHandle(string id) => canHandle(id);

        public Task<JobnetRawDetail?> FetchAsync(string id, CancellationToken cancellationToken) =>
            fetch(id, cancellationToken);
    }
}
