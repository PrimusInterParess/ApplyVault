namespace ApplyVault.Api.Infrastructure;

public interface IDistributedLockProvider
{
    Task<IAsyncDisposable?> TryAcquireAsync(
        string resourceName,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);
}
