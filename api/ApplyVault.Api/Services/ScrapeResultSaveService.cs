using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public sealed class ScrapeResultSaveService(
    IScrapeResultEnrichmentService enrichmentService,
    IScrapeResultStore store) : IScrapeResultSaveService
{
    public async Task<SavedScrapeResult> SaveAsync(
        ScrapeResultDto request,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var enrichedRequest = await enrichmentService.EnrichIfNeededAsync(request, cancellationToken);
        return await store.SaveAsync(enrichedRequest, userId, cancellationToken);
    }
}
