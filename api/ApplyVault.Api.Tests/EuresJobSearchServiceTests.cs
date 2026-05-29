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
    public async Task SearchAsync_SingleKeyword_OrdersByRelevanceThenNewestDate()
    {
        var responsePayload = new EuresSearchResponsePayload
        {
            NumberRecords = 2,
            Jvs =
            [
                EuresTestData.CreateSearchJob(
                    "job-newer-irrelevant",
                    "Marketing Specialist",
                    "Contoso",
                    "General marketing",
                    creationDate: EuresTestData.SampleCreationDate + 1_000),
                EuresTestData.CreateSearchJob(
                    "job-older-relevant",
                    "Backend Developer",
                    "Fabrikam",
                    "API development",
                    creationDate: EuresTestData.SampleCreationDate)
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
            ResultsPerPage = 2,
            LocationCode = "dk",
            RequestLanguage = "en"
        });

        Assert.Equal(2, result.TotalResults);
        Assert.Equal("job-older-relevant", result.Jobs[0].Id);
        Assert.Equal("job-newer-irrelevant", result.Jobs[1].Id);
    }

    [Fact]
    public async Task SearchAsync_SingleKeyword_UsesNewestDateAsTieBreaker()
    {
        var responsePayload = new EuresSearchResponsePayload
        {
            NumberRecords = 2,
            Jvs =
            [
                EuresTestData.CreateSearchJob(
                    "job-older-match",
                    "Backend Developer",
                    "Contoso",
                    "API development",
                    creationDate: EuresTestData.SampleCreationDate),
                EuresTestData.CreateSearchJob(
                    "job-newer-match",
                    "Platform Developer",
                    "Fabrikam",
                    "Platform development",
                    creationDate: EuresTestData.SampleCreationDate + 1_000)
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
            ResultsPerPage = 2,
            LocationCode = "dk",
            RequestLanguage = "en"
        });

        Assert.Equal("job-newer-match", result.Jobs[0].Id);
        Assert.Equal("job-older-match", result.Jobs[1].Id);
    }

    [Fact]
    public async Task SearchAsync_SingleKeyword_IncludesZeroRelevanceUpstreamMatches()
    {
        var responsePayload = new EuresSearchResponsePayload
        {
            NumberRecords = 1,
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

        Assert.Single(result.Jobs);
        Assert.Equal(1, result.TotalResults);
    }

    [Fact]
    public async Task SearchAsync_MultipleKeywords_StillFiltersZeroRelevanceJobs()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                EuresTestData.SerializeSearchResponse(new EuresSearchResponsePayload
                {
                    Jvs = [EuresTestData.CreateSearchJob("job-irrelevant", "Chef", "Restaurant", "Cooking")]
                }),
                Encoding.UTF8,
                "application/json")
        });

        var result = await service.SearchAsync(new EuresJobSearchRequest
        {
            Keywords = ["developer", "backend"],
            Page = 1,
            ResultsPerPage = 10
        });

        Assert.Empty(result.Jobs);
        Assert.Equal(0, result.TotalResults);
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
    public async Task SearchAsync_SingleKeyword_FetchesAllUpstreamPages()
    {
        var upstreamRequests = 0;

        var service = CreateService((request) =>
        {
            upstreamRequests++;
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            var page = body.Contains("\"page\":2", StringComparison.Ordinal) ? 2 : 1;

            var payload = page switch
            {
                1 => new EuresSearchResponsePayload
                {
                    NumberRecords = 55,
                    Jvs = Enumerable.Range(1, 50)
                        .Select((index) => EuresTestData.CreateSearchJob(
                            $"job-{index}",
                            $"Software role {index}",
                            "Contoso",
                            "General software work"))
                        .ToArray()
                },
                _ => new EuresSearchResponsePayload
                {
                    NumberRecords = 55,
                    Jvs = Enumerable.Range(51, 5)
                        .Select((index) => EuresTestData.CreateSearchJob(
                            $"job-{index}",
                            $"Software role {index}",
                            "Contoso",
                            "General software work"))
                        .ToArray()
                }
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(EuresTestData.SerializeSearchResponse(payload), Encoding.UTF8, "application/json")
            };
        });

        var result = await service.SearchAsync(new EuresJobSearchRequest
        {
            Keyword = "software",
            Page = 1,
            ResultsPerPage = 5,
            LocationCode = "dk",
            RequestLanguage = "en"
        });

        Assert.Equal(2, upstreamRequests);
        Assert.Equal(55, result.TotalResults);
        Assert.Equal(5, result.Jobs.Count);
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
