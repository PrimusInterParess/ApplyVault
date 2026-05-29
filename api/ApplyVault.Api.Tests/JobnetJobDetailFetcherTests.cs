using System.Net;
using System.Text;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services.Jobnet;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace ApplyVault.Api.Tests;

public sealed class JobnetJobDetailFetcherTests
{
    [Fact]
    public async Task FetchAsync_UsesCachedSearchPayload_WithoutCallingFindById()
    {
        var findByIdCalled = false;
        var handler = new StubHttpMessageHandler((request) =>
        {
            findByIdCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var options = Microsoft.Extensions.Options.Options.Create(new JobnetIntegrationOptions());
        var memoryCache = new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));
        var payloadCache = new JobnetSearchPayloadCache(memoryCache, options);
        var searchJob = JobnetTestData.CreateSearchJob(
            "E11069412",
            "Senior Backend Developer",
            "Nordea Bank Danmark",
            "<p>Build APIs with Java and Spring Boot across distributed systems.</p>");

        await payloadCache.SetAsync(searchJob, CancellationToken.None);

        var fetcher = new JobnetJobDetailFetcher(
            new JobnetApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://jobnet.dk/bff") }, options),
            payloadCache);

        var result = await fetcher.FetchAsync("E11069412", CancellationToken.None);

        Assert.False(findByIdCalled);
        Assert.NotNull(result);
        Assert.Equal(JobnetDescriptionSource.SearchFallback, result!.Source);
        Assert.Equal("Senior Backend Developer", result.Mapped.Title);
    }

    [Fact]
    public async Task FetchAsync_FallsBackToFindById_WhenCacheMisses()
    {
        var findSearchCalled = false;
        var handler = new StubHttpMessageHandler((request) =>
        {
            findSearchCalled = request.RequestUri?.AbsolutePath.Contains("/FindJob/Search", StringComparison.Ordinal) == true;
            var payload = new JobnetSearchResponsePayload
            {
                JobAds = [JobnetTestData.CreateSearchJob("E99999999", "Cached Miss Job", "Acme")]
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JobnetTestData.SerializeSearchResponse(payload), Encoding.UTF8, "application/json")
            };
        });

        var options = Microsoft.Extensions.Options.Options.Create(new JobnetIntegrationOptions());
        var memoryCache = new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()));
        var fetcher = new JobnetJobDetailFetcher(
            new JobnetApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://jobnet.dk/bff") }, options),
            new JobnetSearchPayloadCache(memoryCache, options));

        var result = await fetcher.FetchAsync("E99999999", CancellationToken.None);

        Assert.True(findSearchCalled);
        Assert.NotNull(result);
        Assert.Equal("Cached Miss Job", result!.Mapped.Title);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
