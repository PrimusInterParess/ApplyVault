using System.Net;
using System.Text;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services.Eures;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Tests;

public sealed class EuresApiClientTests
{
    [Fact]
    public void BuildSearchPayload_IncludesNormalizedKeywordAndLocation()
    {
        var payload = EuresApiClient.BuildSearchPayload(
            keyword: "  developer  ",
            resultsPerPage: 25,
            page: 2,
            sortSearch: "MOST_RECENT",
            locationCode: " DK ",
            requestLanguage: " da ");

        Assert.Equal(25, payload.ResultsPerPage);
        Assert.Equal(2, payload.Page);
        Assert.Equal("MOST_RECENT", payload.SortSearch);
        Assert.Equal(["dk"], payload.LocationCodes);
        Assert.Equal("da", payload.RequestLanguage);
        Assert.Single(payload.Keywords);
        Assert.Equal("developer", payload.Keywords[0].Keyword);
        Assert.Equal("EVERYWHERE", payload.Keywords[0].SpecificSearchCode);
        Assert.StartsWith("applyvault-", payload.SessionId);
    }

    [Fact]
    public async Task SearchAsync_Success_ReturnsDeserializedPayload()
    {
        var responsePayload = new EuresSearchResponsePayload
        {
            NumberRecords = 1,
            Jvs = [EuresTestData.CreateSearchJob("job-1", "Developer")]
        };
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(EuresTestData.SerializeSearchResponse(responsePayload), Encoding.UTF8, "application/json")
        });

        var result = await client.SearchAsync(
            EuresApiClient.BuildSearchPayload("developer", 10, 1, "MOST_RECENT", "dk", "en"));

        Assert.NotNull(result);
        Assert.Equal(1, result!.NumberRecords);
        Assert.Single(result.Jvs!);
        Assert.Equal("job-1", result.Jvs![0].Id);
    }

    [Fact]
    public async Task SearchAsync_Failure_ThrowsEuresJobClientException()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("upstream failure", Encoding.UTF8, "text/plain")
        });

        var exception = await Assert.ThrowsAsync<EuresJobClientException>(
            () => client.SearchAsync(
                EuresApiClient.BuildSearchPayload("developer", 10, 1, "MOST_RECENT", "dk", "en")));

        Assert.Contains("502", exception.Message);
        Assert.Contains("upstream failure", exception.Message);
    }

    [Fact]
    public async Task GetJobByIdAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await client.GetJobByIdAsync("missing-id", "en");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetJobByIdAsync_Success_ReturnsDeserializedPayload()
    {
        var detail = EuresTestData.CreateDetailJob("job-1", "Developer");
        var client = CreateClient(request =>
        {
            Assert.Contains("/jv-searchengine/public/jv/id/job-1", request.RequestUri!.AbsoluteUri);
            Assert.Contains("requestLang=en", request.RequestUri.Query);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(EuresTestData.SerializeDetailResponse(detail), Encoding.UTF8, "application/json")
            };
        });

        var result = await client.GetJobByIdAsync("job-1", "en");

        Assert.NotNull(result);
        Assert.Equal("job-1", result!.Id);
    }

    private static EuresApiClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHttpMessageHandler(responder);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://europa.eu/eures/api")
        };

        return new EuresApiClient(
            httpClient,
            Microsoft.Extensions.Options.Options.Create(new EuresIntegrationOptions
            {
                BaseUrl = "https://europa.eu/eures/api"
            }));
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
