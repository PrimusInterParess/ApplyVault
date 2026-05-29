using System.Net;
using System.Text;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services.Jobnet;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Tests;

public sealed class JobnetJobSearchServiceTests
{
    [Fact]
    public async Task SearchAsync_SingleKeyword_OrdersByRelevanceThenNewestDate()
    {
        var service = CreateService(
            workInDenmarkOnly: true,
            (request) =>
            {
                var payload = new JobnetSearchResponsePayload
                {
                    JobAds =
                    [
                        JobnetTestData.CreateSearchJob(
                            "E10000002",
                            "Marketing Specialist",
                            "Fabrikam",
                            "General marketing",
                            publicationDate: "2026-01-20T00:00:00+01:00"),
                        JobnetTestData.CreateSearchJob(
                            "E10000001",
                            "Backend Developer",
                            "Contoso",
                            "developer",
                            publicationDate: "2026-01-10T00:00:00+01:00")
                    ]
                };

                return JsonResponse(payload);
            });

        var result = await service.SearchAsync(new JobnetJobSearchRequest
        {
            Keyword = "developer",
            Page = 1,
            ResultsPerPage = 10
        });

        Assert.Equal(2, result.Jobs.Count);
        Assert.Equal("E10000001", result.Jobs[0].Id);
        Assert.Equal("E10000002", result.Jobs[1].Id);
    }

    [Fact]
    public async Task SearchAsync_SingleKeyword_UsesNewestDateAsTieBreaker()
    {
        var service = CreateService(
            workInDenmarkOnly: true,
            (request) =>
            {
                var payload = new JobnetSearchResponsePayload
                {
                    JobAds =
                    [
                        JobnetTestData.CreateSearchJob(
                            "E10000001",
                            "Backend Developer",
                            "Contoso",
                            "developer",
                            publicationDate: "2026-01-10T00:00:00+01:00"),
                        JobnetTestData.CreateSearchJob(
                            "E10000002",
                            "Platform Developer",
                            "Fabrikam",
                            "developer platform",
                            publicationDate: "2026-01-20T00:00:00+01:00")
                    ]
                };

                return JsonResponse(payload);
            });

        var result = await service.SearchAsync(new JobnetJobSearchRequest
        {
            Keyword = "developer",
            Page = 1,
            ResultsPerPage = 10
        });

        Assert.Equal("E10000002", result.Jobs[0].Id);
        Assert.Equal("E10000001", result.Jobs[1].Id);
    }

    [Fact]
    public async Task SearchAsync_WorkInDenmarkOnly_FiltersNonWorkInDenmarkJobs()
    {
        var service = CreateService(
            workInDenmarkOnly: true,
            (request) =>
            {
                var path = request.RequestUri!.AbsolutePath;

                if (path.Contains("/FindJob/Search", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = new JobnetSearchResponsePayload
                    {
                        TotalJobAdCount = 2,
                        JobAds =
                        [
                            JobnetTestData.CreateSearchJob("b2b58b21-1353-47c7-afdb-5bb1ff15fd5a", "Backend Developer", "Contoso", "API developer"),
                            JobnetTestData.CreateSearchJob("c3c69c22-2464-58d8-bfec-6cc2ff26fe6b", "Chef", "Restaurant", "Cooking")
                        ]
                    };

                    return JsonResponse(payload);
                }

                if (path.Contains("b2b58b21-1353-47c7-afdb-5bb1ff15fd5a", StringComparison.OrdinalIgnoreCase))
                {
                    return JsonResponse(JobnetTestData.CreateDetailJob("Backend Developer", "Contoso", workInDenmark: true));
                }

                if (path.Contains("c3c69c22-2464-58d8-bfec-6cc2ff26fe6b", StringComparison.OrdinalIgnoreCase))
                {
                    return JsonResponse(JobnetTestData.CreateDetailJob("Chef", "Restaurant", workInDenmark: false));
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });

        var result = await service.SearchAsync(new JobnetJobSearchRequest
        {
            Keyword = "developer",
            Page = 1,
            ResultsPerPage = 10
        });

        Assert.Single(result.Jobs);
        Assert.Equal("b2b58b21-1353-47c7-afdb-5bb1ff15fd5a", result.Jobs[0].Id);
        Assert.True(result.Jobs[0].WorkInDenmark);
    }

    [Fact]
    public async Task SearchAsync_WorkInDenmarkOnly_IncludesEuresImportedJobsWithoutCallingDetail()
    {
        HttpRequestMessage? detailRequest = null;

        var service = CreateService(
            workInDenmarkOnly: true,
            (request) =>
            {
                if (request.RequestUri!.AbsolutePath.Contains("/FindJob/JobAdDetails", StringComparison.OrdinalIgnoreCase))
                {
                    detailRequest = request;
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);
                }

                var payload = new JobnetSearchResponsePayload
                {
                    JobAds =
                    [
                        JobnetTestData.CreateSearchJob("E10990623", "EURES Developer", "Contoso", "developer"),
                        JobnetTestData.CreateSearchJob("E11024431", "Another EURES Developer", "Fabrikam", "developer")
                    ]
                };

                return JsonResponse(payload);
            });

        var result = await service.SearchAsync(new JobnetJobSearchRequest
        {
            Keyword = "developer",
            Page = 1,
            ResultsPerPage = 10
        });

        Assert.Null(detailRequest);
        Assert.Equal(2, result.Jobs.Count);
        Assert.All(result.Jobs, (job) => Assert.True(job.WorkInDenmark));
    }

    [Fact]
    public async Task SearchAsync_WorkInDenmarkDisabled_SkipsDetailClassificationChecks()
    {
        var detailRequests = 0;

        var service = CreateService(
            workInDenmarkOnly: false,
            (request) =>
            {
                if (request.RequestUri!.AbsolutePath.Contains("/FindJob/JobAdDetails", StringComparison.OrdinalIgnoreCase))
                {
                    detailRequests++;
                }

                var payload = new JobnetSearchResponsePayload
                {
                    JobAds = [JobnetTestData.CreateSearchJob("job-1", "Backend Developer", "Contoso", "developer")]
                };

                return JsonResponse(payload);
            });

        var result = await service.SearchAsync(new JobnetJobSearchRequest
        {
            Keyword = "developer",
            Page = 1,
            ResultsPerPage = 10
        });

        Assert.Single(result.Jobs);
        Assert.Equal(0, detailRequests);
    }

    [Fact]
    public async Task SearchAsync_SingleKeyword_FetchesAllUpstreamPages()
    {
        var upstreamRequests = 0;

        var service = CreateService(
            workInDenmarkOnly: true,
            (request) =>
        {
            upstreamRequests++;
            var query = request.RequestUri!.Query;
            var page = query.Contains("pageNumber=2", StringComparison.Ordinal) ? 2 : 1;

            var payload = page switch
            {
                1 => new JobnetSearchResponsePayload
                {
                    TotalJobAdCount = 55,
                    JobAds = Enumerable.Range(1, 50)
                        .Select((index) => JobnetTestData.CreateSearchJob(
                            $"E{index:D8}",
                            $"Software role {index}",
                            "Contoso",
                            "General software work"))
                        .ToList()
                },
                _ => new JobnetSearchResponsePayload
                {
                    TotalJobAdCount = 55,
                    JobAds = Enumerable.Range(51, 5)
                        .Select((index) => JobnetTestData.CreateSearchJob(
                            $"E{index:D8}",
                            $"Software role {index}",
                            "Contoso",
                            "General software work"))
                        .ToList()
                }
            };

            return JsonResponse(payload);
        });

        var result = await service.SearchAsync(new JobnetJobSearchRequest
        {
            Keyword = "software",
            Page = 1,
            ResultsPerPage = 5
        });

        Assert.Equal(2, upstreamRequests);
        Assert.Equal(55, result.TotalResults);
        Assert.Equal(55, result.UpstreamTotalResults);
        Assert.False(result.ResultsTruncated);
        Assert.Equal(5, result.Jobs.Count);
    }

    [Fact]
    public async Task SearchAsync_UpstreamFailureAfterFirstPage_ReturnsPartialResults()
    {
        var upstreamRequests = 0;

        var service = CreateService(
            workInDenmarkOnly: false,
            (request) =>
            {
                upstreamRequests++;
                var query = request.RequestUri!.Query;

                if (query.Contains("pageNumber=2", StringComparison.Ordinal))
                {
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    {
                        Content = new StringContent(
                            "{\"errorInformation\":null,\"correlationId\":null}",
                            Encoding.UTF8,
                            "application/json")
                    };
                }

                var payload = new JobnetSearchResponsePayload
                {
                    TotalJobAdCount = 100,
                    JobAds = Enumerable.Range(1, 50)
                        .Select((index) => JobnetTestData.CreateSearchJob(
                            $"job-{index}",
                            $"Software role {index}",
                            "Contoso",
                            "software"))
                        .ToList()
                };

                return JsonResponse(payload);
            });

        var result = await service.SearchAsync(new JobnetJobSearchRequest
        {
            Keyword = "software",
            Page = 1,
            ResultsPerPage = 10
        });

        Assert.True(upstreamRequests >= 2);
        Assert.Equal(50, result.TotalResults);
        Assert.True(result.ResultsTruncated);
        Assert.Equal(10, result.Jobs.Count);
    }

    private static JobnetJobSearchService CreateService(
        bool workInDenmarkOnly,
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHttpMessageHandler(responder);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://jobnet.dk/bff")
        };

        var options = Microsoft.Extensions.Options.Options.Create(new JobnetIntegrationOptions
        {
            BaseUrl = "https://jobnet.dk/bff",
            WorkInDenmarkOnly = workInDenmarkOnly,
            MaxResultsPerPage = 50,
            MaxUpstreamScanPages = 20,
            ScanResultsPerPage = 50
        });

        return new JobnetJobSearchService(
            new JobnetApiClient(httpClient, options),
            options,
            new JobnetRankedResultsCache(
                new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())),
                options),
            new JobnetClassificationCache(
                new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())),
                options));
    }

    private static HttpResponseMessage JsonResponse(JobnetSearchResponsePayload payload) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JobnetTestData.SerializeSearchResponse(payload), Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage JsonResponse(JobnetDetailResponsePayload payload) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JobnetTestData.SerializeDetailResponse(payload), Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
