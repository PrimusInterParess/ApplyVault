using System.Net;
using System.Net.Http.Json;
using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services.JobRequirements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ApplyVault.Api.IntegrationTests;

public sealed class JobRequirementsPersistenceIntegrationTests(ApplyVaultWebApplicationFactory factory)
    : IClassFixture<ApplyVaultWebApplicationFactory>
{
    [Fact]
    public async Task Saving_and_editing_an_owned_job_records_internal_work_without_exposing_it_in_HTTP()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        var response = await client.PostAsJsonAsync("/api/scrape-results", IntegrationTestScrapeFactory.Create());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var saved = Assert.IsType<SaveScrapeResultResponse>(await response.Content.ReadFromJsonAsync<SaveScrapeResultResponse>());

        using var otherClient = factory.CreateAuthenticatedClient(TestUserTokens.UserB);
        var denied = await otherClient.PatchAsJsonAsync($"/api/scrape-results/{saved.Id}/description", new { description = "Other user's edit" });
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);

        var edited = await client.PatchAsJsonAsync($"/api/scrape-results/{saved.Id}/description", new { description = "Rust experience required." });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var body = await edited.Content.ReadAsStringAsync();
        Assert.DoesNotContain("requirementsStatus", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requirementsJson", body, StringComparison.OrdinalIgnoreCase);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplyVaultDbContext>();
        var userA = await db.Users.SingleAsync(user => user.SupabaseUserId == TestUserTokens.UserASub);
        var userB = await db.Users.SingleAsync(user => user.SupabaseUserId == TestUserTokens.UserBSub);
        var reader = scope.ServiceProvider.GetRequiredService<IJobRequirementsReader>();
        Assert.Equal(JobRequirementsStatus.Pending, (await reader.GetCurrentAsync(saved.Id, userA.Id))?.Status);
        Assert.Null(await reader.GetCurrentAsync(saved.Id, userB.Id));
    }

    [Fact]
    public async Task Concurrent_description_edit_returns_a_conflict_and_preserves_the_newer_description()
    {
        var gate = new DescriptionSaveGate();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddDbContext<ApplyVaultDbContext>(options => options.AddInterceptors(gate))));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", TestUserTokens.UserA);
        var response = await client.PostAsJsonAsync("/api/scrape-results", IntegrationTestScrapeFactory.Create());
        var saved = Assert.IsType<SaveScrapeResultResponse>(await response.Content.ReadFromJsonAsync<SaveScrapeResultResponse>());
        var route = $"/api/scrape-results/{saved.Id}/description";

        var older = client.PatchAsJsonAsync(route, new { description = DescriptionSaveGate.PausedDescription });
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            var newer = await client.PatchAsJsonAsync(route, new { description = "Newer posting description." });
            Assert.Equal(HttpStatusCode.OK, newer.StatusCode);
        }
        finally
        {
            gate.Release.TrySetResult();
        }

        Assert.Equal(HttpStatusCode.Conflict, (await older).StatusCode);
        var current = await client.GetFromJsonAsync<SavedScrapeResult>($"/api/scrape-results/{saved.Id}");
        Assert.Equal("Newer posting description.", current?.Payload.JobDetails.JobDescription);
    }

    private sealed class DescriptionSaveGate : SaveChangesInterceptor
    {
        public const string PausedDescription = "Older posting description.";
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.Entries<ScrapeResultEntity>().Any(entry =>
                entry.State == EntityState.Modified && entry.Entity.JobDescriptionOverride == PausedDescription) == true)
            {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }

            return result;
        }
    }
}
