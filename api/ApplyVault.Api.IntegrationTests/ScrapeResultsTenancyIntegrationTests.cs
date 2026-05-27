using System.Net;
using System.Net.Http.Json;
using ApplyVault.Api.Models;

namespace ApplyVault.Api.IntegrationTests;

public sealed class ScrapeResultsTenancyIntegrationTests(ApplyVaultWebApplicationFactory factory)
    : IClassFixture<ApplyVaultWebApplicationFactory>
{
    [Fact]
    public async Task Post_without_token_returns_401()
    {
        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/scrape-results",
            IntegrationTestScrapeFactory.Create());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_with_valid_token_returns_201()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        var response = await client.PostAsJsonAsync(
            "/api/scrape-results",
            IntegrationTestScrapeFactory.Create());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<SaveScrapeResultResponse>();

        Assert.NotNull(payload);
        Assert.NotEqual(Guid.Empty, payload.Id);
    }

    [Fact]
    public async Task GetAll_does_not_include_other_users_jobs()
    {
        var jobUrl = $"https://jobs.example.com/listings/{Guid.NewGuid():N}";
        Guid createdId;

        using (var clientA = factory.CreateAuthenticatedClient(TestUserTokens.UserA))
        {
            var createResponse = await clientA.PostAsJsonAsync(
                "/api/scrape-results",
                IntegrationTestScrapeFactory.Create(jobUrl));

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            var created = await createResponse.Content.ReadFromJsonAsync<SaveScrapeResultResponse>();

            Assert.NotNull(created);
            createdId = created.Id;
        }

        using var clientB = factory.CreateAuthenticatedClient(TestUserTokens.UserB);
        var listResponse = await clientB.GetAsync("/api/scrape-results");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var jobs = await listResponse.Content.ReadFromJsonAsync<List<SavedScrapeResult>>();

        Assert.NotNull(jobs);
        Assert.DoesNotContain(jobs, (job) => job.Id == createdId);
    }

    [Fact]
    public async Task GetById_for_other_users_job_returns_404()
    {
        Guid createdId;

        using (var clientA = factory.CreateAuthenticatedClient(TestUserTokens.UserA))
        {
            var createResponse = await clientA.PostAsJsonAsync(
                "/api/scrape-results",
                IntegrationTestScrapeFactory.Create());

            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            var created = await createResponse.Content.ReadFromJsonAsync<SaveScrapeResultResponse>();

            Assert.NotNull(created);
            createdId = created.Id;
        }

        using var clientB = factory.CreateAuthenticatedClient(TestUserTokens.UserB);
        var getResponse = await clientB.GetAsync($"/api/scrape-results/{createdId}");

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Post_then_GetAll_returns_job_for_same_user()
    {
        var jobUrl = $"https://jobs.example.com/listings/{Guid.NewGuid():N}";
        Guid createdId;

        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);

        var createResponse = await client.PostAsJsonAsync(
            "/api/scrape-results",
            IntegrationTestScrapeFactory.Create(jobUrl));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<SaveScrapeResultResponse>();

        Assert.NotNull(created);
        createdId = created.Id;

        var listResponse = await client.GetAsync("/api/scrape-results");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var jobs = await listResponse.Content.ReadFromJsonAsync<List<SavedScrapeResult>>();

        Assert.NotNull(jobs);
        Assert.Contains(jobs, (job) => job.Id == createdId);
    }
}
