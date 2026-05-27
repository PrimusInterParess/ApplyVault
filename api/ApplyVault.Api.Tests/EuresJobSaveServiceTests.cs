using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using ApplyVault.Api.Services.Eures;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Tests;

public sealed class EuresJobSaveServiceTests
{
    [Fact]
    public async Task SaveAsync_WhenDetailNotFound_ReturnsNull()
    {
        var service = CreateService(
            detail: null,
            out _,
            out var saveServiceSpy);

        var response = await service.SaveAsync("missing-job", "en", Guid.NewGuid());

        Assert.Null(response);
        Assert.Empty(saveServiceSpy.Calls);
    }

    [Fact]
    public async Task SaveAsync_WhenUrlAlreadySaved_ReturnsExistingWithoutCallingSaveService()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        const string savedUrl = "https://jobs.example.com/apply";
        var existingSavedAt = new DateTimeOffset(2026, 1, 10, 8, 0, 0, TimeSpan.Zero);
        var existingId = Guid.NewGuid();

        dbContext.ScrapeResults.Add(new ScrapeResultEntity
        {
            Id = existingId,
            UserId = userId,
            SavedAt = existingSavedAt,
            IsRejected = false,
            IsDeleted = false,
            Title = "Backend Developer",
            Url = savedUrl,
            Text = "Build APIs",
            TextLength = 10,
            ExtractedAt = existingSavedAt.ToString("O"),
            SourceHostname = "europa.eu",
            DetectedPageType = "eures-job",
            JobTitle = "Backend Developer",
            CompanyName = "Contoso",
            CaptureReviewStatus = CaptureReviewStatuses.NotRequired
        });
        await dbContext.SaveChangesAsync();

        var detail = CreateMappedDetail(applicationUrl: savedUrl);
        var service = CreateService(detail, dbContext, out var saveServiceSpy);

        var response = await service.SaveAsync("job-1", "en", userId);

        Assert.NotNull(response);
        Assert.Equal(existingId, response.Id);
        Assert.Equal(existingSavedAt, response.SavedAt);
        Assert.True(response.AlreadyExists);
        Assert.Empty(saveServiceSpy.Calls);
    }

    [Fact]
    public async Task SaveAsync_WhenNewListing_SavesAndReturnsCreatedResponse()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.NewGuid();
        var savedAt = new DateTimeOffset(2026, 5, 26, 12, 0, 0, TimeSpan.Zero);
        var savedId = Guid.NewGuid();
        var detail = CreateMappedDetail(applicationUrl: "https://jobs.example.com/new-role");

        var service = CreateService(
            detail,
            dbContext,
            out var saveServiceSpy,
            onSave: (_, requestedUserId) =>
            {
                Assert.Equal(userId, requestedUserId);
                return new SavedScrapeResult(
                    savedId,
                    savedAt,
                    false,
                    null,
                    null,
                    [],
                    EuresScrapeResultMapper.MapToScrapeResult(detail),
                    new CaptureQualityDto(
                        CaptureReviewStatuses.NotRequired,
                        false,
                        0.95,
                        CreateField("Backend Developer"),
                        CreateField("Contoso"),
                        CreateField("Copenhagen, DK"),
                        CreateField("Build APIs")),
                    null);
            });

        var response = await service.SaveAsync("job-1", "en", userId);

        Assert.NotNull(response);
        Assert.Equal(savedId, response.Id);
        Assert.Equal(savedAt, response.SavedAt);
        Assert.False(response.AlreadyExists);
        Assert.Single(saveServiceSpy.Calls);
        Assert.Equal(userId, saveServiceSpy.Calls[0].UserId);
        Assert.Equal("https://jobs.example.com/new-role", saveServiceSpy.Calls[0].Request.Url);
        Assert.Equal("eures-job", saveServiceSpy.Calls[0].Request.JobDetails.DetectedPageType);
    }

    [Fact]
    public async Task GetByUrlAsync_ReturnsMatchingJobForUser()
    {
        await using var dbContext = CreateDbContext();
        var store = new EfCoreScrapeResultStore(dbContext);
        var userId = Guid.NewGuid();
        const string url = "https://jobs.example.com/shared";

        dbContext.ScrapeResults.Add(new ScrapeResultEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SavedAt = DateTimeOffset.UtcNow,
            IsRejected = false,
            IsDeleted = false,
            Title = "Saved role",
            Url = url,
            Text = "Saved role description",
            TextLength = 22,
            ExtractedAt = DateTimeOffset.UtcNow.ToString("O"),
            SourceHostname = "europa.eu",
            DetectedPageType = "eures-job",
            JobTitle = "Saved role",
            CaptureReviewStatus = CaptureReviewStatuses.NotRequired
        });
        await dbContext.SaveChangesAsync();

        var result = await store.GetByUrlAsync(userId, url);

        Assert.NotNull(result);
        Assert.Equal(url, result.Payload.Url);
    }

    private static EuresJobSaveService CreateService(
        EuresJobDetailResponse? detail,
        out ApplyVaultDbContext dbContext,
        out SaveServiceSpy saveServiceSpy)
    {
        dbContext = CreateDbContext();
        return CreateService(detail, dbContext, out saveServiceSpy);
    }

    private static EuresJobSaveService CreateService(
        EuresJobDetailResponse? detail,
        ApplyVaultDbContext dbContext,
        out SaveServiceSpy saveServiceSpy,
        Func<ScrapeResultDto, Guid, SavedScrapeResult>? onSave = null)
    {
        saveServiceSpy = new SaveServiceSpy(onSave);
        var store = new EfCoreScrapeResultStore(dbContext);
        var client = new StubEuresJobClient(detail);

        return new EuresJobSaveService(client, store, saveServiceSpy);
    }

    private static EuresJobDetailResponse CreateMappedDetail(string applicationUrl)
    {
        var payload = EuresTestData.CreateDetailJob(
            "job-1",
            "Backend Developer",
            "Contoso",
            "Build APIs");

        var mapped = EuresJobMapper.MapDetail(payload, "en");
        return mapped with { ApplicationUrl = applicationUrl, SourceUrl = applicationUrl };
    }

    private static ApplyVaultDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplyVaultDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplyVaultDbContext(options);
    }

    private static CaptureQualityFieldDto CreateField(string? value) =>
        new(value, value, null, 0.95, false, null);

    private sealed class StubEuresJobClient(EuresJobDetailResponse? detail) : IEuresJobClient
    {
        public Task<EuresJobSearchResponse> SearchJobsAsync(
            EuresJobSearchRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EuresJobDetailResponse?> GetJobByIdAsync(
            string id,
            string requestLanguage,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(detail);
    }

    private sealed class SaveServiceSpy(Func<ScrapeResultDto, Guid, SavedScrapeResult>? onSave = null)
        : IScrapeResultSaveService
    {
        public List<(ScrapeResultDto Request, Guid UserId)> Calls { get; } = [];

        public Task<SavedScrapeResult> SaveAsync(
            ScrapeResultDto request,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((request, userId));

            if (onSave is not null)
            {
                return Task.FromResult(onSave(request, userId));
            }

            throw new InvalidOperationException("SaveAsync was not configured for this test.");
        }
    }
}
