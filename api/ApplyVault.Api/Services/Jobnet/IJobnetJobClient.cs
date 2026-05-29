using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services.Jobnet;

public interface IJobnetJobClient
{
    Task<JobnetJobSearchResponse> SearchJobsAsync(
        JobnetJobSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<JobnetJobDetailResponse?> GetJobByIdAsync(
        string id,
        string requestLanguage,
        CancellationToken cancellationToken = default);
}
