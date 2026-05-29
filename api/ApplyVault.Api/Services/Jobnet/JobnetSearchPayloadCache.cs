using System.Text.Json;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.Jobnet;

internal sealed class JobnetSearchPayloadCache(
    IDistributedCache cache,
    IOptions<JobnetIntegrationOptions> options)
{
    private const string KeyPrefix = "jobnet:payload:";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<JobnetSearchJobPayload?> GetAsync(
        string jobAdId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobAdId))
        {
            return null;
        }

        var cachedBytes = await cache.GetAsync(BuildStorageKey(jobAdId), cancellationToken).ConfigureAwait(false);

        if (cachedBytes is not { Length: > 0 })
        {
            return null;
        }

        return JsonSerializer.Deserialize<JobnetSearchJobPayload>(cachedBytes, SerializerOptions);
    }

    public async Task SetAsync(
        JobnetSearchJobPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(payload.JobAdId))
        {
            return;
        }

        var ttlMinutes = Math.Max(1, options.Value.RankedCacheTtlMinutes);
        var serialized = JsonSerializer.SerializeToUtf8Bytes(payload, SerializerOptions);

        await cache.SetAsync(
            BuildStorageKey(payload.JobAdId),
            serialized,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(ttlMinutes)
            },
            cancellationToken).ConfigureAwait(false);
    }

    private static string BuildStorageKey(string jobAdId) =>
        KeyPrefix + jobAdId.Trim().ToLowerInvariant();
}
