using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public interface IScrapeResultEnrichmentService
{
    Task<ScrapeResultDto> EnrichIfNeededAsync(ScrapeResultDto request, CancellationToken cancellationToken = default);
}
