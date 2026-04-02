using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public interface IScrapeResultSaveService
{
    Task<SavedScrapeResult> SaveAsync(
        ScrapeResultDto request,
        Guid? userId,
        CancellationToken cancellationToken = default);
}
