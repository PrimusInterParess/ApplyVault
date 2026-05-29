using System.Text;
using System.Text.Json;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.Jobnet;

internal sealed class JobnetClassificationCache(
    IDistributedCache cache,
    IOptions<JobnetIntegrationOptions> options)
{
    private const string KeyPrefix = "jobnet:wid:";

    public async Task<bool?> GetWorkInDenmarkAsync(
        string jobAdId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobAdId))
        {
            return null;
        }

        var cachedBytes = await cache.GetAsync(BuildStorageKey(jobAdId), cancellationToken).ConfigureAwait(false);

        if (cachedBytes is not { Length: 1 })
        {
            return null;
        }

        return cachedBytes[0] switch
        {
            1 => true,
            0 => false,
            _ => null
        };
    }

    public async Task SetWorkInDenmarkAsync(
        string jobAdId,
        bool workInDenmark,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobAdId))
        {
            return;
        }

        var ttlMinutes = Math.Max(1, options.Value.ClassificationCacheTtlMinutes);
        var payload = new[] { (byte)(workInDenmark ? 1 : 0) };

        await cache.SetAsync(
            BuildStorageKey(jobAdId),
            payload,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(ttlMinutes)
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static string BuildStorageKey(string jobAdId)
    {
        return KeyPrefix + jobAdId.Trim().ToLowerInvariant();
    }
}
