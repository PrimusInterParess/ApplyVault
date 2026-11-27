using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using ApplyVault.Api.Services.JobRequirements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace ApplyVault.Api.Tests;

public sealed class JobRequirementsTests
{
    [Fact]
    public async Task Saved_job_is_processed_in_background_and_exposes_grounded_requirements()
    {
        await using var fixture = await Fixture.CreateAsync();
        var job = await fixture.SaveAsync("PostgreSQL experience is a plus.");

        Assert.Equal(JobRequirementsStatus.Pending, (await fixture.ReadAsync(job.Id))?.Status);
        await fixture.RunAsync();

        var result = await fixture.ReadAsync(job.Id);
        Assert.Equal(JobRequirementsStatus.Ready, result?.Status);
        var fact = Assert.Single(Assert.IsType<JobRequirementsDocument>(result?.Document).Facts);
        Assert.Equal("PostgreSQL", fact.Value);
        Assert.Equal(RequirementImportance.Preferred, fact.Importance);
        Assert.Equal("PostgreSQL experience is a plus.", fact.Evidence);
        Assert.Null(await fixture.ReadAsync(job.Id, Guid.NewGuid()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("<p>&nbsp;</p>")]
    public async Task Missing_or_unusable_description_waits_for_a_description(string? description)
    {
        await using var fixture = await Fixture.CreateAsync();
        var job = await fixture.SaveAsync(description);

        Assert.Equal(JobRequirementsStatus.DescriptionNeeded, (await fixture.ReadAsync(job.Id))?.Status);
        Assert.Equal(0, await fixture.RunAsync());

        await using var db = fixture.CreateDb();
        await new EfCoreScrapeResultStore(db).UpdateDescriptionAsync(job.Id, fixture.UserId, "PostgreSQL experience is a plus.");
        await fixture.RunAsync();
        Assert.Equal(JobRequirementsStatus.Ready, (await fixture.ReadAsync(job.Id))?.Status);
    }

    [Fact]
    public async Task Background_extraction_can_consume_the_Gemini_structured_response()
    {
        await using var fixture = await Fixture.CreateAsync();
        var job = await fixture.SaveAsync("PostgreSQL experience is a plus.");
        using var http = new HttpClient(new GeminiHandler());
        fixture.ProviderOverride = new GoogleAiJobRequirementsClient(http,
            Microsoft.Extensions.Options.Options.Create(new ApplyVault.Api.Options.GoogleAiOptions
            {
                Enabled = true, ApiKey = "test-key", Model = "test-model"
            }));

        await fixture.RunAsync();

        Assert.Equal(JobRequirementsStatus.Ready, (await fixture.ReadAsync(job.Id))?.Status);
    }

    private sealed class GeminiHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("test-key", Assert.Single(request.Headers.GetValues("x-goog-api-key")));
            Assert.DoesNotContain("test-key", request.RequestUri?.ToString() ?? "");
            var body = await (request.Content ?? throw new InvalidOperationException()).ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(body);
            Assert.Equal("application/json", document.RootElement.GetProperty("generationConfig").GetProperty("responseMimeType").GetString());
            // Live Gemini rejects this fact schema with a 120-item grammar bound.
            var factsSchema = document.RootElement.GetProperty("generationConfig")
                .GetProperty("responseSchema").GetProperty("properties").GetProperty("facts");
            if (factsSchema.TryGetProperty("maxItems", out var maxItems) && maxItems.GetInt32() == 120)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("""{"error":{"status":"INVALID_ARGUMENT","message":"Schema constraint has too many states for serving."}}""")
                };
            }

            var json = await new FakeProvider().ExtractAsync("", cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { new { text = json } } } } }
                }))
            };
        }
    }

    [Fact]
    public async Task Editing_the_description_immediately_hides_previous_requirements_even_if_replacement_fails()
    {
        await using var fixture = await Fixture.CreateAsync();
        var job = await fixture.SaveAsync("PostgreSQL experience is a plus.");
        await fixture.RunAsync();
        await fixture.EditAsync(job.Id, "Rust experience required.");

        var pending = await fixture.ReadAsync(job.Id);
        Assert.Equal(JobRequirementsStatus.Pending, pending?.Status);
        Assert.Null(pending?.Document);
        fixture.Settings.MaxAttempts = 1;
        fixture.Provider.Extract = (_, _) => throw new HttpRequestException("Provider unavailable");
        await fixture.RunAsync();

        var failed = await fixture.ReadAsync(job.Id);
        Assert.Equal(JobRequirementsStatus.Failed, failed?.Status);
        Assert.Null(failed?.Document);
    }

    [Fact]
    public async Task Description_edit_during_extraction_discards_the_old_result()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Settings.BatchSize = 1;
        var job = await fixture.SaveAsync("PostgreSQL experience is a plus.");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Provider.Extract = async (_, token) =>
        {
            entered.SetResult();
            return await release.Task.WaitAsync(token);
        };
        var run = fixture.RunAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            await fixture.EditAsync(job.Id, "Rust experience required.");
        }
        finally
        {
            release.SetResult(await new FakeProvider().ExtractAsync("", CancellationToken.None));
            await run;
        }

        var current = await fixture.ReadAsync(job.Id);
        Assert.Equal(JobRequirementsStatus.Pending, current?.Status);
        Assert.Null(current?.Document);
    }

    [Fact]
    public async Task Unchanged_description_does_not_requeue_successful_requirements()
    {
        await using var fixture = await Fixture.CreateAsync();
        var job = await fixture.SaveAsync("PostgreSQL experience is a plus.");
        await fixture.RunAsync();
        await fixture.EditAsync(job.Id, "PostgreSQL experience is a plus.");

        Assert.Equal(0, await fixture.RunAsync());
        Assert.Equal(JobRequirementsStatus.Ready, (await fixture.ReadAsync(job.Id))?.Status);
    }

    [Fact]
    public async Task Failed_attempts_are_delayed_and_stop_after_the_configured_limit()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Provider.Extract = (_, _) => throw new HttpRequestException("Provider unavailable");
        var job = await fixture.SaveAsync("PostgreSQL experience is a plus.");

        for (var attempt = 1; attempt <= fixture.Settings.MaxAttempts; attempt++)
        {
            Assert.Equal(1, await fixture.RunAsync());
            var expected = attempt == fixture.Settings.MaxAttempts ? JobRequirementsStatus.Failed : JobRequirementsStatus.Pending;
            Assert.Equal(expected, (await fixture.ReadAsync(job.Id))?.Status);
            Assert.Equal(0, await fixture.RunAsync());
            fixture.Clock.Now = fixture.Clock.Now.AddSeconds(fixture.Settings.RetryDelaySeconds);
        }

        Assert.Equal(0, await fixture.RunAsync());
        await fixture.EditAsync(job.Id, "A new posting description.");
        Assert.Equal(JobRequirementsStatus.Pending, (await fixture.ReadAsync(job.Id))?.Status);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"schemaVersion\":1,\"facts\":null}")]
    [InlineData("{\"schemaVersion\":1,\"facts\":[null]}")]
    [InlineData("{\"schemaVersion\":2,\"facts\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"facts\":[],\"extra\":true}")]
    [InlineData("{\"schemaVersion\":1}")]
    [InlineData("{\"schemaVersion\":1,\"facts\":[{\"category\":0,\"value\":\"SQL\",\"importance\":\"required\",\"evidence\":\"SQL\"}]}")]
    [InlineData("{\"schemaVersion\":1,\"facts\":[{\"category\":\"unknown\",\"value\":\"PostgreSQL\",\"importance\":\"preferred\",\"evidence\":\"PostgreSQL experience is a plus.\"}]}")]
    [InlineData("{\"schemaVersion\":1,\"facts\":[{\"category\":\"skillsToolsKnowledge\",\"value\":\"Rust\",\"importance\":\"required\",\"evidence\":\"Rust required.\"}]}")]
    [InlineData("{\"schemaVersion\":1,\"facts\":[{\"category\":\"skillsToolsKnowledge\",\"value\":\"Rust\",\"importance\":\"required\",\"evidence\":\"PostgreSQL experience is a plus.\"}]}")]
    public async Task Invalid_or_unsupported_output_is_never_available_to_preparation(string json)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Settings.MaxAttempts = 1;
        fixture.Provider.Extract = (_, _) => Task.FromResult(json);
        var job = await fixture.SaveAsync("PostgreSQL experience is a plus.");

        await fixture.RunAsync();

        var result = await fixture.ReadAsync(job.Id);
        Assert.Equal(JobRequirementsStatus.Failed, result?.Status);
        Assert.Null(result?.Document);
    }

    [Fact]
    public async Task Empty_categories_are_a_valid_success()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Provider.Extract = (_, _) => Task.FromResult("""{"schemaVersion":1,"facts":[]}""");
        var job = await fixture.SaveAsync("Welcome to our careers page.");
        await fixture.RunAsync();

        var result = await fixture.ReadAsync(job.Id);
        Assert.Equal(JobRequirementsStatus.Ready, result?.Status);
        Assert.Empty(Assert.IsType<JobRequirementsDocument>(result?.Document).Facts);
    }

    [Fact]
    public async Task Oversized_fact_collections_are_rejected_by_application_validation()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Settings.MaxAttempts = 1;
        var facts = Enumerable.Range(0, 121).Select(_ => new
        {
            category = "skillsToolsKnowledge", value = "PostgreSQL", importance = "preferred",
            evidence = "PostgreSQL experience is a plus."
        });
        fixture.Provider.Extract = (_, _) => Task.FromResult(JsonSerializer.Serialize(new { schemaVersion = 1, facts }));
        var job = await fixture.SaveAsync("PostgreSQL experience is a plus.");

        await fixture.RunAsync();

        var result = await fixture.ReadAsync(job.Id);
        Assert.Equal(JobRequirementsStatus.Failed, result?.Status);
        Assert.Null(result?.Document);
    }

    [Fact]
    public async Task Two_workers_cannot_process_the_same_live_claim()
    {
        await using var fixture = await Fixture.CreateAsync();
        var job = await fixture.SaveAsync("PostgreSQL experience is a plus.");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Provider.Extract = async (_, token) =>
        {
            entered.SetResult();
            return await release.Task.WaitAsync(token);
        };
        var first = fixture.RunAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            Assert.Equal(0, await fixture.RunAsync());
        }
        finally
        {
            release.SetResult(await new FakeProvider().ExtractAsync("", CancellationToken.None));
            await first;
        }

        Assert.Equal(JobRequirementsStatus.Ready, (await fixture.ReadAsync(job.Id))?.Status);
    }

    [Fact]
    public async Task Shutdown_cancellation_recovers_after_the_lease_without_using_an_attempt()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Settings.MaxAttempts = 1;
        var job = await fixture.SaveAsync("PostgreSQL experience is a plus.");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Provider.Extract = async (_, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("Unreachable");
        };
        using var stopping = new CancellationTokenSource();
        var first = fixture.RunAsync(stopping.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        stopping.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.Equal(0, await fixture.RunAsync());

        fixture.Clock.Now += fixture.Settings.LeaseDuration + TimeSpan.FromSeconds(1);
        fixture.Provider.Extract = new FakeProvider().Extract;
        Assert.Equal(1, await fixture.RunAsync());
        Assert.Equal(JobRequirementsStatus.Ready, (await fixture.ReadAsync(job.Id))?.Status);
    }

    [Fact]
    public async Task An_expired_worker_cannot_replace_the_recovered_workers_result()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Settings.BatchSize = 1;
        var job = await fixture.SaveAsync("PostgreSQL experience is a plus. Rust experience is a plus.");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Provider.Extract = async (_, token) =>
        {
            entered.SetResult();
            return await release.Task.WaitAsync(token);
        };
        var first = fixture.RunAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            fixture.Clock.Now += fixture.Settings.LeaseDuration + TimeSpan.FromSeconds(1);
            fixture.Provider.Extract = new FakeProvider().Extract;
            Assert.Equal(1, await fixture.RunAsync());
        }
        finally
        {
            release.SetResult("""{"schemaVersion":1,"facts":[{"category":"skillsToolsKnowledge","value":"Rust","importance":"preferred","evidence":"Rust experience is a plus."}]}""");
            await first;
        }

        var document = Assert.IsType<JobRequirementsDocument>((await fixture.ReadAsync(job.Id))?.Document);
        Assert.Equal("PostgreSQL", Assert.Single(document.Facts).Value);
    }

    [Fact]
    public async Task Deletion_during_extraction_prevents_the_result_becoming_available()
    {
        await using var fixture = await Fixture.CreateAsync();
        var job = await fixture.SaveAsync("PostgreSQL experience is a plus.");
        fixture.Provider.Extract = async (_, token) =>
        {
            await using var db = fixture.CreateDb();
            await new EfCoreScrapeResultStore(db).DeleteAsync(job.Id, fixture.UserId, token);
            return await new FakeProvider().ExtractAsync("", token);
        };

        await fixture.RunAsync();

        Assert.Null(await fixture.ReadAsync(job.Id));
        Assert.Equal(0, await fixture.RunAsync());
    }

    [Fact]
    public async Task Legacy_jobs_are_backfilled_in_bounded_batches_without_requeueing_completed_work()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Settings.BatchSize = 1;
        var first = await fixture.AddLegacyAsync("PostgreSQL experience is a plus.");
        var second = await fixture.AddLegacyAsync("PostgreSQL experience is a plus.");
        var deleted = await fixture.AddLegacyAsync("This deleted job must never be processed.", isDeleted: true);

        Assert.Equal(1, await fixture.RunAsync());
        var states = new[] { (await fixture.ReadAsync(first))?.Status, (await fixture.ReadAsync(second))?.Status };
        Assert.Single(states, status => status == JobRequirementsStatus.Ready);
        Assert.Single(states, status => status == JobRequirementsStatus.Pending);
        Assert.Equal(1, await fixture.RunAsync());
        Assert.Equal(0, await fixture.RunAsync());
        Assert.Null(await fixture.ReadAsync(deleted));
    }

    [Fact]
    public async Task All_six_categories_and_explicit_importance_levels_are_preserved()
    {
        await using var fixture = await Fixture.CreateAsync();
        const string description = "Build APIs. SQL is required. Senior role. Danish preferred. Banking team. Panel interview.";
        fixture.Provider.Extract = (_, _) => Task.FromResult("""
            {"schemaVersion":1,"facts":[
              {"category":"responsibilitiesOutcomes","value":"Build APIs","importance":"unspecified","evidence":"Build APIs."},
              {"category":"skillsToolsKnowledge","value":"SQL","importance":"required","evidence":"SQL is required."},
              {"category":"experienceSeniority","value":"Senior role","importance":"unspecified","evidence":"Senior role."},
              {"category":"educationCertificationsLanguages","value":"Danish","importance":"preferred","evidence":"Danish preferred."},
              {"category":"teamProductIndustry","value":"Banking team","importance":"unspecified","evidence":"Banking team."},
              {"category":"interviewProcess","value":"Panel interview","importance":"unspecified","evidence":"Panel interview."}
            ]}
            """);
        var job = await fixture.SaveAsync(description);
        await fixture.RunAsync();

        var document = Assert.IsType<JobRequirementsDocument>((await fixture.ReadAsync(job.Id))?.Document);
        Assert.Equal(6, document.Facts.Count);
        Assert.Equal(6, document.Facts.Select(fact => fact.Category).Distinct().Count());
        Assert.Equal(RequirementImportance.Required, document.Facts[1].Importance);
        Assert.Equal(RequirementImportance.Preferred, document.Facts[3].Importance);
        Assert.Equal(RequirementImportance.Unspecified, document.Facts[5].Importance);
    }

    [Fact]
    public async Task Provider_timeout_is_a_retryable_failure_rather_than_shutdown_cancellation()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Settings.TimeoutSeconds = 1;
        fixture.Provider.Extract = async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            throw new InvalidOperationException("Unreachable");
        };
        var job = await fixture.SaveAsync("PostgreSQL experience is a plus.");

        await fixture.RunAsync();

        Assert.Equal(JobRequirementsStatus.Pending, (await fixture.ReadAsync(job.Id))?.Status);
        Assert.Equal(0, await fixture.RunAsync());
    }

    [Fact]
    public async Task Capture_review_description_override_also_invalidates_current_requirements()
    {
        await using var fixture = await Fixture.CreateAsync();
        var job = await fixture.SaveAsync("PostgreSQL experience is a plus.");
        await fixture.RunAsync();
        await using var db = fixture.CreateDb();
        await new EfCoreScrapeResultStore(db).UpdateCaptureReviewAsync(job.Id, fixture.UserId,
            new UpdateScrapeResultCaptureReviewRequest(null, null, null, "Rust required."));

        var result = await fixture.ReadAsync(job.Id);
        Assert.Equal(JobRequirementsStatus.Pending, result?.Status);
        Assert.Null(result?.Document);
    }

    [Fact]
    public async Task Pending_work_with_a_missing_fingerprint_is_repaired_before_extraction()
    {
        await using var fixture = await Fixture.CreateAsync();
        var jobId = await fixture.AddLegacyAsync("PostgreSQL experience is a plus.");
        await using (var db = fixture.CreateDb())
        {
            await db.ScrapeResults.Where(job => job.Id == jobId).ExecuteUpdateAsync(update => update
                .SetProperty(job => job.RequirementsStatus, JobRequirementsStatus.Pending));
        }

        await fixture.RunAsync();
        await fixture.RunAsync();

        Assert.Equal(JobRequirementsStatus.Ready, (await fixture.ReadAsync(jobId))?.Status);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"requirements-{Guid.NewGuid():N}.db");
        public Guid UserId { get; } = Guid.NewGuid();
        public FakeProvider Provider { get; } = new();
        public IJobRequirementsAiClient? ProviderOverride { get; set; }
        public TestClock Clock { get; } = new();
        public JobRequirementsOptions Settings { get; } = new();

        public static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
            await using var db = fixture.CreateDb();
            await db.Database.EnsureCreatedAsync();
            db.Users.Add(new AppUserEntity { Id = fixture.UserId, SupabaseUserId = fixture.UserId.ToString() });
            await db.SaveChangesAsync();
            return fixture;
        }

        public ApplyVaultDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplyVaultDbContext>()
            .UseSqlite($"Data Source={_databasePath};Pooling=False")
            .ReplaceService<IModelCustomizer, SqliteTestModelCustomizer>().Options);

        public async Task<SavedScrapeResult> SaveAsync(string? description)
        {
            await using var db = CreateDb();
            var payload = new ScrapeResultDto("Engineer", "https://example.com/job", description ?? "", 0,
                "2026-11-27", new JobDetailsDto("example.com", "job", "Engineer", "Example", null,
                    description, null, null, []));
            return await new EfCoreScrapeResultStore(db).SaveAsync(
                new ScrapeResultCaptureQualityService().Assess(payload), UserId);
        }

        public async Task<JobRequirementsResult?> ReadAsync(Guid jobId, Guid? userId = null)
        {
            await using var db = CreateDb();
            return await new JobRequirementsReader(db).GetCurrentAsync(jobId, userId ?? UserId);
        }

        public async Task EditAsync(Guid jobId, string description)
        {
            await using var db = CreateDb();
            await new EfCoreScrapeResultStore(db).UpdateDescriptionAsync(jobId, UserId, description);
        }

        public async Task<Guid> AddLegacyAsync(string description, bool isDeleted = false)
        {
            await using var db = CreateDb();
            var job = new ScrapeResultEntity
            {
                Id = Guid.NewGuid(), UserId = UserId, Title = "Legacy job", Url = "https://example.com/legacy",
                Text = description, ExtractedAt = "2026-11-27", SourceHostname = "example.com",
                DetectedPageType = "job", JobDescription = description, IsDeleted = isDeleted,
                CaptureReviewStatus = CaptureReviewStatuses.NotRequired
            };
            db.ScrapeResults.Add(job);
            await db.SaveChangesAsync();
            return job.Id;
        }

        public async Task<int> RunAsync(CancellationToken cancellationToken = default)
        {
            await using var db = CreateDb();
            return await new JobRequirementsProcessor(new JobRequirementsWorkStore(db), ProviderOverride ?? Provider,
                Microsoft.Extensions.Options.Options.Create(Settings), Clock, NullLogger<JobRequirementsProcessor>.Instance)
                .RunOnceAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            File.Delete(_databasePath);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeProvider : IJobRequirementsAiClient
    {
        public Func<string, CancellationToken, Task<string>> Extract { get; set; } = (_, _) => Task.FromResult(
            """{"schemaVersion":1,"facts":[{"category":"skillsToolsKnowledge","value":"PostgreSQL","importance":"preferred","evidence":"PostgreSQL experience is a plus."}]}""");

        public Task<string> ExtractAsync(string description, CancellationToken cancellationToken) => Extract(description, cancellationToken);
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
