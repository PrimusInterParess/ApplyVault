using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public interface IScrapeResultAiClient
{
    Task<ScrapeResultDto> EnrichAsync(
        ScrapeResultDto request,
        string? repairGuidance,
        CancellationToken cancellationToken = default);
}
