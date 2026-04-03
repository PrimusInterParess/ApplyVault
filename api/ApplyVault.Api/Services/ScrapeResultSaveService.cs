using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public sealed class ScrapeResultSaveService(
    IScrapeResultEnrichmentService enrichmentService,
    IScrapeResultCaptureQualityService captureQualityService,
    IScrapeResultStore store) : IScrapeResultSaveService
{
    public async Task<SavedScrapeResult> SaveAsync(
        ScrapeResultDto request,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var initialAssessment = captureQualityService.Assess(request);
        var enrichedRequest = await enrichmentService.EnrichLowConfidenceFieldsAsync(
            initialAssessment,
            cancellationToken);
        var finalAssessment = captureQualityService.Assess(enrichedRequest);
        return await store.SaveAsync(finalAssessment, userId, cancellationToken);
    }
}
