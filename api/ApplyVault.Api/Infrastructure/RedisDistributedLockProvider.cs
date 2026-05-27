using StackExchange.Redis;

namespace ApplyVault.Api.Infrastructure;

internal sealed class RedisDistributedLockProvider(IConnectionMultiplexer multiplexer) : IDistributedLockProvider
{
    private const string ReleaseScript = """
        if redis.call('get', KEYS[1]) == ARGV[1] then
            return redis.call('del', KEYS[1])
        end
        return 0
        """;

    public async Task<IAsyncDisposable?> TryAcquireAsync(
        string resourceName,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var database = multiplexer.GetDatabase();
        var lockKey = $"lock:{resourceName}";
        var token = Guid.NewGuid().ToString("N");
        var acquired = await database.StringSetAsync(
                lockKey,
                token,
                ttl,
                When.NotExists)
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!acquired)
        {
            return null;
        }

        return new RedisLockHandle(database, lockKey, token);
    }

    private sealed class RedisLockHandle(
        IDatabase database,
        string lockKey,
        string token) : IAsyncDisposable
    {
        private int _released;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 1)
            {
                return;
            }

            await database.ScriptEvaluateAsync(
                    ReleaseScript,
                    [lockKey],
                    [token])
                .ConfigureAwait(false);
        }
    }
}
