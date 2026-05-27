using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApplyVault.Api.Models;
using Microsoft.Extensions.Caching.Distributed;

namespace ApplyVault.Api.Services.Eures;

internal sealed class EuresRankedResultsCache(IDistributedCache cache)
{
    private static readonly TimeSpan RankedResultsCacheLifetime = TimeSpan.FromMinutes(5);
    private const string KeyPrefix = "eures:ranked:";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<EuresJobListingDto[]> GetOrCreateAsync(
        string cacheKey,
        Func<CancellationToken, Task<EuresJobListingDto[]>> factory,
        CancellationToken cancellationToken = default)
    {
        var storageKey = BuildStorageKey(cacheKey);
        var cachedBytes = await cache.GetAsync(storageKey, cancellationToken).ConfigureAwait(false);

        if (cachedBytes is { Length: > 0 })
        {
            var cachedJobs = JsonSerializer.Deserialize<EuresJobListingDto[]>(cachedBytes, SerializerOptions);
            if (cachedJobs is not null)
            {
                return cachedJobs;
            }
        }

        var rankedJobs = await factory(cancellationToken).ConfigureAwait(false);
        var payload = JsonSerializer.SerializeToUtf8Bytes(rankedJobs, SerializerOptions);

        await cache.SetAsync(
            storageKey,
            payload,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = RankedResultsCacheLifetime
            },
            cancellationToken).ConfigureAwait(false);

        return rankedJobs;
    }

    private static string BuildStorageKey(string cacheKey)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey));
        var fingerprint = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return KeyPrefix + fingerprint;
    }
}
