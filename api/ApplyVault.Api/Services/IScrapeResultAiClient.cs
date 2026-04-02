using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public interface IScrapeResultAiClient
{
    Task<ScrapeResultDto> EnrichAsync(ScrapeResultDto request, CancellationToken cancellationToken = default);
}
