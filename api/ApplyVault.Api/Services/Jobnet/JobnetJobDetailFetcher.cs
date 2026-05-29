namespace ApplyVault.Api.Services.Jobnet;

internal sealed class JobnetJobDetailFetcher(
    JobnetApiClient apiClient,
    JobnetSearchPayloadCache searchPayloadCache)
{
    public async Task<JobnetRawDetail?> FetchAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var normalizedId = id.Trim();

        if (JobnetJobIdentifiers.SupportsNativeDetailEndpoint(normalizedId))
        {
            var detail = await apiClient.GetJobByIdAsync(normalizedId, cancellationToken);

            if (detail is null)
            {
                return null;
            }

            return new JobnetRawDetail(
                JobnetDescriptionSource.NativeDetail,
                JobnetJobMapper.MapDetail(normalizedId, detail));
        }

        if (!JobnetJobIdentifiers.IsEuresImported(normalizedId))
        {
            return null;
        }

        var searchJob = await searchPayloadCache.GetAsync(normalizedId, cancellationToken);

        if (searchJob is null)
        {
            searchJob = await apiClient.FindSearchJobByIdAsync(normalizedId, cancellationToken);
        }

        if (searchJob is null)
        {
            return null;
        }

        return new JobnetRawDetail(
            JobnetDescriptionSource.SearchFallback,
            JobnetJobMapper.MapDetailFromSearch(normalizedId, searchJob));
    }
}
