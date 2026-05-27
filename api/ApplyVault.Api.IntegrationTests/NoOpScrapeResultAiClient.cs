using ApplyVault.Api.Models;
using ApplyVault.Api.Services;

namespace ApplyVault.Api.IntegrationTests;

internal sealed class NoOpScrapeResultAiClient : IScrapeResultAiClient
{
    public Task<ScrapeResultDto> EnrichAsync(
        ScrapeResultDto request,
        string? repairGuidance,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(request);
}
