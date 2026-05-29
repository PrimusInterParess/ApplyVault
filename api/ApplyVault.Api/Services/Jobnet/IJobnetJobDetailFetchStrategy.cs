namespace ApplyVault.Api.Services.Jobnet;

internal interface IJobnetJobDetailFetchStrategy
{
    bool CanHandle(string id);

    Task<JobnetRawDetail?> FetchAsync(string id, CancellationToken cancellationToken);
}
