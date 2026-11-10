using System.Net;
using System.Text;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services.Jobnet;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Tests;

public sealed class JobnetApiClientTests
{
    [Fact]
    public async Task SearchAsync_Transient500_RetriesAndReturnsPayload()
    {
        var attempts = 0;
        var handler = new StubHttpMessageHandler((request) =>
        {
            attempts++;

            if (attempts < 3)
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
                TotalJobAdCount = 1,
                JobAds = [JobnetTestData.CreateSearchJob("job-1", "Developer", "Contoso", "developer")]
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JobnetTestData.SerializeSearchResponse(payload), Encoding.UTF8, "application/json")
            };
        });

        var client = CreateClient(handler, new JobnetIntegrationOptions { SearchMaxRetryAttempts = 3 });
        var result = await client.SearchAsync("developer", pageNumber: 1, resultsPerPage: 10);

        Assert.Equal(3, attempts);
        Assert.NotNull(result);
        Assert.Single(result!.JobAds ?? []);
    }

    [Fact]
    public async Task SearchAsync_WithoutPlace_OmitsPostalCodeAndRadius()
    {
        Uri? requestUri = null;
        var handler = new StubHttpMessageHandler((request) =>
        {
            requestUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JobnetTestData.SerializeSearchResponse(new JobnetSearchResponsePayload()),
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var client = CreateClient(handler, new JobnetIntegrationOptions());
        await client.SearchAsync("developer", pageNumber: 1, resultsPerPage: 10);

        var query = requestUri?.Query ?? string.Empty;
        Assert.DoesNotContain("postalCode=", query, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("kmRadius=", query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchAsync_WithPostalCode_SendsCenterAndCommuteDistance()
    {
        Uri? requestUri = null;
        var handler = new StubHttpMessageHandler((request) =>
        {
            requestUri = request.RequestUri;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    JobnetTestData.SerializeSearchResponse(new JobnetSearchResponsePayload()),
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var client = CreateClient(handler, new JobnetIntegrationOptions());
        await client.SearchAsync("developer", pageNumber: 1, resultsPerPage: 10, postalCode: "8000");

        var query = requestUri?.Query ?? string.Empty;
        Assert.Contains("postalCode=8000", query, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("kmRadius=50", query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchAsync_Persistent500_ThrowsAfterConfiguredAttempts()
    {
        var attempts = 0;
        var handler = new StubHttpMessageHandler((_) =>
        {
            attempts++;
            return new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(
                    "{\"errorInformation\":null,\"correlationId\":null}",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var client = CreateClient(handler, new JobnetIntegrationOptions { SearchMaxRetryAttempts = 2 });

        var exception = await Assert.ThrowsAsync<JobnetJobClientException>(
            () => client.SearchAsync("developer", pageNumber: 1, resultsPerPage: 10));

        Assert.Equal(2, attempts);
        Assert.Contains("status 500", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/FindJob/Search", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static JobnetApiClient CreateClient(
        HttpMessageHandler handler,
        JobnetIntegrationOptions integrationOptions)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://jobnet.dk/bff")
        };

        JobnetHttpClientConfigurator.Configure(httpClient, integrationOptions);
        return new JobnetApiClient(httpClient, Microsoft.Extensions.Options.Options.Create(integrationOptions));
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
