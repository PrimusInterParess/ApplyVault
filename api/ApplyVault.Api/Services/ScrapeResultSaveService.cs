using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public sealed class ScrapeResultSaveService(
    IScrapeResultEnrichmentService enrichmentService,
    IScrapeResultCaptureQualityService captureQualityService,
    IScrapeResultStore store) : IScrapeResultSaveService
{
    public async Task<SavedScrapeResult> SaveAsync(
        ScrapeResultDto request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var initialCapture = captureQualityService.Assess(request);
        var enrichedRequest = await enrichmentService.EnrichLowConfidenceFieldsAsync(
            initialCapture,
            cancellationToken);
        var finalCapture = captureQualityService.Assess(enrichedRequest);

        return await store.SaveAsync(finalCapture, userId, cancellationToken);
    }
}
