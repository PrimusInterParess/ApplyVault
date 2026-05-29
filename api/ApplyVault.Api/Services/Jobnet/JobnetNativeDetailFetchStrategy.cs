namespace ApplyVault.Api.Services.Jobnet;

internal sealed class JobnetNativeDetailFetchStrategy(JobnetApiClient apiClient) : IJobnetJobDetailFetchStrategy
{
    public bool CanHandle(string id) => JobnetJobIdentifiers.SupportsNativeDetailEndpoint(id);

    public async Task<JobnetRawDetail?> FetchAsync(string id, CancellationToken cancellationToken)
    {
        var detail = await apiClient.GetJobByIdAsync(id, cancellationToken);

        if (detail is null)
        {
            return null;
        }

        return new JobnetRawDetail(
            JobnetDescriptionSource.NativeDetail,
            JobnetJobMapper.MapDetail(id.Trim(), detail));
    }
}
