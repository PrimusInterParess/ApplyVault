using System.Net;
using System.Text;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services.Eures;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Tests;

public sealed class EuresJobClientTests
{
    [Fact]
    public async Task GetJobByIdAsync_EmptyId_ReturnsNullWithoutCallingApi()
    {
        var handler = new RecordingHttpMessageHandler();
        var client = CreateClient(handler);

        var result = await client.GetJobByIdAsync("  ", "en");

        Assert.Null(result);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetJobByIdAsync_FoundJob_ReturnsMappedDetail()
    {
        var detail = EuresTestData.CreateDetailJob("job-42", "Backend Developer", "Contoso", "Build APIs");
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(EuresTestData.SerializeDetailResponse(detail), Encoding.UTF8, "application/json")
        });
        var client = CreateClient(handler);

        var result = await client.GetJobByIdAsync("job-42", "en");

        Assert.NotNull(result);
        Assert.Equal("job-42", result!.Id);
        Assert.Equal("Backend Developer", result.Title);
        Assert.Equal("Contoso", result.Employer);
    }

    private static EuresJobClient CreateClient(HttpMessageHandler handler)
    {
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
        var apiClient = new EuresApiClient(httpClient, options);
        var searchService = new EuresJobSearchService(apiClient, options, new MemoryCache(new MemoryCacheOptions()));

        return new EuresJobClient(searchService, apiClient);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
