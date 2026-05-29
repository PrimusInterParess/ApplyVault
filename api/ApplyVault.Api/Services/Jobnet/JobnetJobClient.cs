using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.Jobnet;

internal sealed class JobnetJobClient(
    JobnetJobSearchService searchService,
    IJobnetJobDetailComposer detailComposer) : IJobnetJobClient
{
    public Task<JobnetJobSearchResponse> SearchJobsAsync(
        JobnetJobSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        return searchService.SearchAsync(request, cancellationToken);
    }

    public Task<JobnetJobDetailResponse?> GetJobByIdAsync(
        string id,
        string requestLanguage,
        CancellationToken cancellationToken = default)
    {
        return detailComposer.ComposeAsync(id, cancellationToken);
    }
}
