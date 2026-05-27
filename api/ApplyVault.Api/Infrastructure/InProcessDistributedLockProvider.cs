using System.Collections.Concurrent;

namespace ApplyVault.Api.Infrastructure;

/// <summary>
/// Process-local lock used when <c>ConnectionStrings:Redis</c> is not configured (single API replica).
/// </summary>
internal sealed class InProcessDistributedLockProvider : IDistributedLockProvider
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

    public async Task<IAsyncDisposable?> TryAcquireAsync(
        string resourceName,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        _ = ttl;
        var gate = Gates.GetOrAdd(resourceName, static (_) => new SemaphoreSlim(1, 1));

        if (!await gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new InProcessLockHandle(gate);
    }

    private sealed class InProcessLockHandle(SemaphoreSlim gate) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            gate.Release();
            return ValueTask.CompletedTask;
        }
    }
}
