using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.Jobnet;

internal sealed class JobnetRankedResultsCache(
    IDistributedCache cache,
    IOptions<JobnetIntegrationOptions> options)
{
    private const string KeyPrefix = "jobnet:ranked:";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<JobnetRankedSearchSnapshot> GetOrCreateAsync(
        string cacheKey,
        Func<CancellationToken, Task<JobnetRankedSearchSnapshot>> factory,
        CancellationToken cancellationToken = default)
    {
        var storageKey = BuildStorageKey(cacheKey);
        var cachedBytes = await cache.GetAsync(storageKey, cancellationToken).ConfigureAwait(false);

        if (cachedBytes is { Length: > 0 })
        {
            var cachedSnapshot = JsonSerializer.Deserialize<JobnetRankedSearchSnapshot>(cachedBytes, SerializerOptions);
            if (cachedSnapshot is not null)
            {
                return cachedSnapshot;
            }
        }

        var rankedSnapshot = await factory(cancellationToken).ConfigureAwait(false);
        var ttlMinutes = Math.Max(1, options.Value.RankedCacheTtlMinutes);
        var payload = JsonSerializer.SerializeToUtf8Bytes(rankedSnapshot, SerializerOptions);

        await cache.SetAsync(
            storageKey,
            payload,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(ttlMinutes)
            },
            cancellationToken).ConfigureAwait(false);

        return rankedSnapshot;
    }

    private static string BuildStorageKey(string cacheKey)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey));
        var fingerprint = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return KeyPrefix + fingerprint;
    }
}
