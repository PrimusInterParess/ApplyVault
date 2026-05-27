using System.Net;
using System.Text;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services.Eures;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Tests;

public sealed class EuresJobSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_SingleKeyword_ReturnsRankedPaginatedResults()
    {
        var responsePayload = new EuresSearchResponsePayload
        {
            NumberRecords = 2,
            Jvs =
            [
                EuresTestData.CreateSearchJob("job-low", "Marketing Specialist", "Contoso", "General marketing"),
                EuresTestData.CreateSearchJob("job-high", "Backend Developer", "Fabrikam", "API development", creationDate: EuresTestData.SampleCreationDate + 1_000)
            ]
        };

        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(EuresTestData.SerializeSearchResponse(responsePayload), Encoding.UTF8, "application/json")
        });

        var result = await service.SearchAsync(new EuresJobSearchRequest
        {
            Keyword = "developer",
            Page = 1,
            ResultsPerPage = 1,
            LocationCode = "dk",
            RequestLanguage = "en"
        });

        Assert.Equal(1, result.TotalResults);
        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.ResultsPerPage);
        Assert.Single(result.Jobs);
        Assert.Equal("job-high", result.Jobs[0].Id);
        Assert.Equal("Backend Developer", result.Jobs[0].Title);
    }

    [Fact]
    public async Task SearchAsync_MultipleKeywords_MergesResultsAndKeepsBestScore()
    {
        var service = CreateService(request =>
        {
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();

            var payload = body.Contains("\"keyword\":\"backend\"", StringComparison.OrdinalIgnoreCase)
                ? new EuresSearchResponsePayload
                {
                    Jvs = [EuresTestData.CreateSearchJob("shared-job", "Backend Engineer", "Contoso", "Backend APIs")]
                }
                : new EuresSearchResponsePayload
                {
                    Jvs = [EuresTestData.CreateSearchJob("shared-job", "Platform Engineer", "Contoso", "C# platform work")]
                };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(EuresTestData.SerializeSearchResponse(payload), Encoding.UTF8, "application/json")
            };
        });

        var result = await service.SearchAsync(new EuresJobSearchRequest
        {
            Keywords = ["backend", "c#"],
            Page = 1,
            ResultsPerPage = 10,
            LocationCode = "dk",
            RequestLanguage = "en"
        });

        Assert.Equal(1, result.TotalResults);
        Assert.Equal("shared-job", result.Jobs[0].Id);
        Assert.Equal("Backend Engineer", result.Jobs[0].Title);
    }

    [Fact]
    public async Task SearchAsync_FiltersOutZeroRelevanceJobs()
    {
        var responsePayload = new EuresSearchResponsePayload
        {
            Jvs = [EuresTestData.CreateSearchJob("job-irrelevant", "Chef", "Restaurant", "Cooking")]
        };

        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(EuresTestData.SerializeSearchResponse(responsePayload), Encoding.UTF8, "application/json")
        });

        var result = await service.SearchAsync(new EuresJobSearchRequest
        {
            Keyword = "developer",
            Page = 1,
            ResultsPerPage = 10
        });

        Assert.Empty(result.Jobs);
        Assert.Equal(0, result.TotalResults);
    }

    private static EuresJobSearchService CreateService(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHttpMessageHandler(responder);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://europa.eu/eures/api")
        };
        var options = Microsoft.Extensions.Options.Options.Create(new EuresIntegrationOptions
        {
            BaseUrl = "https://europa.eu/eures/api",
            DefaultLocationCode = "dk",
            MaxResultsPerPage = 50
        });

        return new EuresJobSearchService(
            new EuresApiClient(httpClient, options),
            options,
            new EuresRankedResultsCache(
                new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions()))));
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
