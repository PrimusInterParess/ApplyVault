using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public interface IScrapeResultEnrichmentService
{
    Task<ScrapeResultDto> EnrichLowConfidenceFieldsAsync(
        AssessedScrapeResult assessment,
        CancellationToken cancellationToken = default);
}
