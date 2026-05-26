using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services.Eures;

internal sealed class EuresJobSaveService(
    IEuresJobClient euresJobClient,
    IScrapeResultStore scrapeResultStore,
    IScrapeResultSaveService scrapeResultSaveService) : IEuresJobSaveService
{
    public async Task<SaveEuresJobResponse?> SaveAsync(
        string id,
        string requestLanguage,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var detail = await euresJobClient.GetJobByIdAsync(id, requestLanguage, cancellationToken);

        if (detail is null)
        {
            return null;
        }

        var scrapeResult = EuresScrapeResultMapper.MapToScrapeResult(detail);
        var existingResult = await scrapeResultStore.GetByUrlAsync(userId, scrapeResult.Url, cancellationToken);

        if (existingResult is not null)
        {
            return new SaveEuresJobResponse(existingResult.Id, existingResult.SavedAt, AlreadyExists: true);
        }

        var savedResult = await scrapeResultSaveService.SaveAsync(scrapeResult, userId, cancellationToken);
        return new SaveEuresJobResponse(savedResult.Id, savedResult.SavedAt, AlreadyExists: false);
    }
}
