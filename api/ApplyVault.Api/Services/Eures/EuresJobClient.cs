using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services.Eures;

internal sealed class EuresJobClient(
    EuresJobSearchService searchService,
    EuresApiClient apiClient) : IEuresJobClient
{
    public Task<EuresJobSearchResponse> SearchJobsAsync(
        EuresJobSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        return searchService.SearchAsync(request, cancellationToken);
    }

    public async Task<EuresJobDetailResponse?> GetJobByIdAsync(
        string id,
        string requestLanguage,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var detail = await apiClient.GetJobByIdAsync(id, requestLanguage, cancellationToken);
        return detail is null ? null : EuresJobMapper.MapDetail(detail, requestLanguage);
    }
}
