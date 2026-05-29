using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using ApplyVault.Api.Services.Jobnet;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Tests;

public sealed class JobnetJobSaveServiceTests
{
    [Fact]
    public async Task SaveAsync_WhenDetailNotFound_ReturnsNull()
    {
        var service = CreateService(detail: null, out _, out var saveServiceSpy);

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
            SourceHostname = "jobnet.dk",
            DetectedPageType = "jobnet-job",
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
                    JobnetScrapeResultMapper.MapToScrapeResult(detail),
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
        Assert.False(response.AlreadyExists);
        Assert.Single(saveServiceSpy.Calls);
        Assert.Equal("jobnet-job", saveServiceSpy.Calls[0].Request.JobDetails.DetectedPageType);
    }

    private static JobnetJobSaveService CreateService(
        JobnetJobDetailResponse? detail,
        out ApplyVaultDbContext dbContext,
        out SaveServiceSpy saveServiceSpy)
    {
        dbContext = CreateDbContext();
        return CreateService(detail, dbContext, out saveServiceSpy);
    }

    private static JobnetJobSaveService CreateService(
        JobnetJobDetailResponse? detail,
        ApplyVaultDbContext dbContext,
        out SaveServiceSpy saveServiceSpy,
        Func<ScrapeResultDto, Guid, SavedScrapeResult>? onSave = null)
    {
        saveServiceSpy = new SaveServiceSpy(onSave);
        var store = new EfCoreScrapeResultStore(dbContext);
        var client = new StubJobnetJobClient(detail);

        return new JobnetJobSaveService(client, store, saveServiceSpy);
    }

    private static JobnetJobDetailResponse CreateMappedDetail(string applicationUrl)
    {
        var payload = JobnetTestData.CreateDetailJob(
            "Backend Developer",
            "Contoso",
            "Build APIs",
            workInDenmark: true,
            applicationUrl: applicationUrl);

        var mapped = JobnetJobMapper.MapDetail("job-1", payload);
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

    private sealed class StubJobnetJobClient(JobnetJobDetailResponse? detail) : IJobnetJobClient
    {
        public Task<JobnetJobSearchResponse> SearchJobsAsync(
            JobnetJobSearchRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<JobnetJobDetailResponse?> GetJobByIdAsync(
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
