using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.Jobnet;

internal sealed class JobnetJobClient(
    JobnetJobSearchService searchService,
    JobnetApiClient apiClient,
    IOptions<JobnetIntegrationOptions> options) : IJobnetJobClient
{
    public Task<JobnetJobSearchResponse> SearchJobsAsync(
        JobnetJobSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        return searchService.SearchAsync(request, cancellationToken);
    }

    public async Task<JobnetJobDetailResponse?> GetJobByIdAsync(
        string id,
        string requestLanguage,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var detail = await apiClient.GetJobByIdAsync(id, cancellationToken);

        if (detail is not null)
        {
            var mapped = JobnetJobMapper.MapDetail(id.Trim(), detail);

            if (options.Value.WorkInDenmarkOnly && !mapped.WorkInDenmark)
            {
                return null;
            }

            return mapped;
        }

        var searchJob = await apiClient.FindSearchJobByIdAsync(id, cancellationToken);

        if (searchJob is null)
        {
            return null;
        }

        var searchMapped = JobnetJobMapper.MapDetailFromSearch(id.Trim(), searchJob);

        if (options.Value.WorkInDenmarkOnly && !searchMapped.WorkInDenmark)
        {
            return null;
        }

        return searchMapped;
    }
}
