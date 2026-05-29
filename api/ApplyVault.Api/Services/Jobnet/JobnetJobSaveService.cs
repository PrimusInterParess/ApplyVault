using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services.Jobnet;

internal sealed class JobnetJobSaveService(
    IJobnetJobClient jobnetJobClient,
    IScrapeResultStore scrapeResultStore,
    IScrapeResultSaveService scrapeResultSaveService) : IJobnetJobSaveService
{
    public async Task<SaveJobnetJobResponse?> SaveAsync(
        string id,
        string requestLanguage,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var detail = await jobnetJobClient.GetJobByIdAsync(id, requestLanguage, cancellationToken);

        if (detail is null)
        {
            return null;
        }

        var scrapeResult = JobnetScrapeResultMapper.MapToScrapeResult(detail);
        var existingResult = await scrapeResultStore.GetByUrlAsync(userId, scrapeResult.Url, cancellationToken);

        if (existingResult is not null)
        {
            return new SaveJobnetJobResponse(existingResult.Id, existingResult.SavedAt, AlreadyExists: true);
        }

        var savedResult = await scrapeResultSaveService.SaveAsync(scrapeResult, userId, cancellationToken);
        return new SaveJobnetJobResponse(savedResult.Id, savedResult.SavedAt, AlreadyExists: false);
    }
}
