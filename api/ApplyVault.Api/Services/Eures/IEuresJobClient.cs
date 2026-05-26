using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services.Eures;

public interface IEuresJobClient
{
    Task<EuresJobSearchResponse> SearchJobsAsync(
        EuresJobSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<EuresJobDetailResponse?> GetJobByIdAsync(
        string id,
        string requestLanguage,
        CancellationToken cancellationToken = default);
}
