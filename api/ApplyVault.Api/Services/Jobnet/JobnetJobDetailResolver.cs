namespace ApplyVault.Api.Services.Jobnet;

internal sealed class JobnetJobDetailResolver(IEnumerable<IJobnetJobDetailFetchStrategy> strategies)
{
    public Task<JobnetRawDetail?> FetchAsync(string id, CancellationToken cancellationToken)
    {
        var strategy = strategies.FirstOrDefault((candidate) => candidate.CanHandle(id));
        return strategy?.FetchAsync(id, cancellationToken) ?? Task.FromResult<JobnetRawDetail?>(null);
    }
}
