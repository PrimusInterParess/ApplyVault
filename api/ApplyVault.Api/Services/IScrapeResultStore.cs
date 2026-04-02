using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public interface IScrapeResultStore
{
    Task<IReadOnlyCollection<SavedScrapeResult>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<SavedScrapeResult?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SavedScrapeResult> SaveAsync(ScrapeResultDto result, CancellationToken cancellationToken = default);

    Task<SavedScrapeResult?> SetRejectedAsync(Guid id, bool isRejected, CancellationToken cancellationToken = default);

    Task<SavedScrapeResult?> UpdateDescriptionAsync(
        Guid id,
        string description,
        CancellationToken cancellationToken = default);

    Task<SavedScrapeResult?> UpdateInterviewDateAsync(
        Guid id,
        DateOnly? interviewDate,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
