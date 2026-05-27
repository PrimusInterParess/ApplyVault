using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Tests;

public sealed class ScrapeResultTenancyTests
{
    [Fact]
    public async Task GetAllAsync_returns_only_jobs_owned_by_user()
    {
        await using var dbContext = CreateDbContext();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        dbContext.ScrapeResults.AddRange(
            CreateJob(userA, "https://jobs.example.com/a"),
            CreateJob(userB, "https://jobs.example.com/b"));
        await dbContext.SaveChangesAsync();

        var store = new EfCoreScrapeResultStore(dbContext);
        var results = await store.GetAllAsync(userA);

        Assert.Single(results);
        Assert.Equal("https://jobs.example.com/a", results.First().Payload.Url);
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_when_job_belongs_to_another_user()
    {
        await using var dbContext = CreateDbContext();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        dbContext.ScrapeResults.Add(CreateJob(userB, "https://jobs.example.com/b", jobId));
        await dbContext.SaveChangesAsync();

        var store = new EfCoreScrapeResultStore(dbContext);
        var result = await store.GetByIdAsync(jobId, userA);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByUrlAsync_does_not_return_another_users_job_with_same_url()
    {
        await using var dbContext = CreateDbContext();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        const string url = "https://jobs.example.com/shared-posting";
        dbContext.ScrapeResults.AddRange(
            CreateJob(userA, url),
            CreateJob(userB, url));
        await dbContext.SaveChangesAsync();

        var store = new EfCoreScrapeResultStore(dbContext);
        var result = await store.GetByUrlAsync(userA, url);

        Assert.NotNull(result);
        Assert.Equal(url, result.Payload.Url);
    }

    [Fact]
    public async Task DeleteAsync_returns_false_for_another_users_job()
    {
        await using var dbContext = CreateDbContext();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        dbContext.ScrapeResults.Add(CreateJob(userB, "https://jobs.example.com/b", jobId));
        await dbContext.SaveChangesAsync();

        var store = new EfCoreScrapeResultStore(dbContext);
        var deleted = await store.DeleteAsync(jobId, userA);

        Assert.False(deleted);
        var entity = await dbContext.ScrapeResults.SingleAsync((job) => job.Id == jobId);
        Assert.False(entity.IsDeleted);
    }

    [Fact]
    public async Task SaveAsync_persists_user_ownership()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var store = new EfCoreScrapeResultStore(dbContext);
        var assessed = CreateAssessedResult("https://jobs.example.com/new");

        var saved = await store.SaveAsync(assessed, userId);

        var entity = await dbContext.ScrapeResults.SingleAsync((job) => job.Id == saved.Id);
        Assert.Equal(userId, entity.UserId);
    }

    private static ApplyVaultDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplyVaultDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplyVaultDbContext(options);
    }

    private static ScrapeResultEntity CreateJob(
        Guid userId,
        string url,
        Guid? id = null)
    {
        var savedAt = DateTimeOffset.UtcNow;

        return new ScrapeResultEntity
        {
            Id = id ?? Guid.NewGuid(),
            UserId = userId,
            SavedAt = savedAt,
            IsRejected = false,
            IsDeleted = false,
            Title = "Backend Developer",
            Url = url,
            Text = "Build APIs",
            TextLength = 10,
            ExtractedAt = savedAt.ToString("O"),
            SourceHostname = "jobs.example.com",
            DetectedPageType = "jobPosting",
            JobTitle = "Backend Developer",
            CompanyName = "Contoso",
            CaptureReviewStatus = CaptureReviewStatuses.NotRequired
        };
    }

    private static AssessedScrapeResult CreateAssessedResult(string url)
    {
        var payload = new ScrapeResultDto(
            "Backend Developer",
            url,
            "Build APIs",
            10,
            DateTimeOffset.UtcNow.ToString("O"),
            new JobDetailsDto(
                "jobs.example.com",
                "jobPosting",
                "Backend Developer",
                "Contoso",
                "Remote",
                "Build APIs",
                null,
                null,
                []));

        var field = new ScrapeResultFieldAssessment(0.95, null);
        var quality = new ScrapeResultCaptureQualityAssessment(
            0.95,
            field,
            field,
            field,
            field);

        return new AssessedScrapeResult(payload, quality);
    }
}
