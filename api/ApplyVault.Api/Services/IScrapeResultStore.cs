using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public interface IScrapeResultStore
{
    Task<IReadOnlyCollection<SavedScrapeResult>> GetAllAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<SavedScrapeResult?> GetByIdAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<SavedScrapeResult> SaveAsync(
        AssessedScrapeResult result,
        Guid? userId,
        CancellationToken cancellationToken = default);

    Task<SavedScrapeResult?> UpdateCaptureReviewAsync(
        Guid id,
        Guid userId,
        UpdateScrapeResultCaptureReviewRequest request,
        CancellationToken cancellationToken = default);

    Task<SavedScrapeResult?> SetRejectedAsync(
        Guid id,
        Guid userId,
        bool isRejected,
        CancellationToken cancellationToken = default);

    Task<SavedScrapeResult?> UpdateDescriptionAsync(
        Guid id,
        Guid userId,
        string description,
        CancellationToken cancellationToken = default);

    Task<SavedScrapeResult?> UpsertInterviewEventAsync(
        Guid id,
        Guid userId,
        UpdateInterviewEventRequest request,
        CancellationToken cancellationToken = default);

    Task<SavedScrapeResult?> ClearInterviewEventAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}
