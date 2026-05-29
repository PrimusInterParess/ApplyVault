namespace ApplyVault.Api.Services.Jobnet;

internal sealed class JobnetSearchFallbackFetchStrategy(JobnetApiClient apiClient) : IJobnetJobDetailFetchStrategy
{
    public bool CanHandle(string id) => JobnetJobIdentifiers.IsEuresImported(id);

    public async Task<JobnetRawDetail?> FetchAsync(string id, CancellationToken cancellationToken)
    {
        var searchJob = await apiClient.FindSearchJobByIdAsync(id, cancellationToken);

        if (searchJob is null)
        {
            return null;
        }

        return new JobnetRawDetail(
            JobnetDescriptionSource.SearchFallback,
            JobnetJobMapper.MapDetailFromSearch(id.Trim(), searchJob));
    }
}
