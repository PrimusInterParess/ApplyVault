using System.Net;
using System.Text.Json;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services.Jobnet;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Tests;

public sealed class JobnetApiClientLiveTests
{
    [Fact]
    public async Task SearchAndDetail_AgainstLiveJobnetBff_ReturnsJobs()
    {
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://jobnet.dk/bff")
        };

        var options = Microsoft.Extensions.Options.Options.Create(new JobnetIntegrationOptions());
        JobnetHttpClientConfigurator.Configure(httpClient, options.Value);

        var client = new JobnetApiClient(httpClient, options);
        var search = await client.SearchAsync("developer", pageNumber: 1, resultsPerPage: 3);

        Assert.NotNull(search);
        Assert.NotEmpty(search!.JobAds ?? []);
        Assert.True(search.TotalJobAdCount > 0);

        var firstId = search.JobAds![0].JobAdId;
        Assert.False(string.IsNullOrWhiteSpace(firstId));

        var detail = await client.GetJobByIdAsync(firstId!);
        Assert.NotNull(detail);
        Assert.False(string.IsNullOrWhiteSpace(detail!.Title));
    }

    [Fact]
    public void FixtureSearchResponse_DeserializesRecordedPayload()
    {
        var json = File.ReadAllText(Path.Combine("Fixtures", "Jobnet", "search-response.json"));
        var payload = JsonSerializer.Deserialize<JobnetSearchResponsePayload>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(payload);
        Assert.NotEmpty(payload!.JobAds ?? []);
    }
}
