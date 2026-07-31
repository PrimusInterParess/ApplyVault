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
        // English "developer" currently ranks EURES (E-prefixed) ads first; those ids do not
        // support the native detail endpoint. Prefer a Danish occupation term and a wider page.
        var search = await client.SearchAsync("softwareudvikler", pageNumber: 1, resultsPerPage: 20);

        Assert.NotNull(search);
        Assert.NotEmpty(search!.JobAds ?? []);
        Assert.True(search.TotalJobAdCount > 0);

        var guidJob = search.JobAds!
            .FirstOrDefault((job) => JobnetJobIdentifiers.SupportsNativeDetailEndpoint(job.JobAdId));
        Assert.NotNull(guidJob);

        var detail = await client.GetJobByIdAsync(guidJob!.JobAdId!);
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
