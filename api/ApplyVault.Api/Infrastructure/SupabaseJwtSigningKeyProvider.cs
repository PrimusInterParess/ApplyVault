using ApplyVault.Api.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ApplyVault.Api.Infrastructure;

public sealed class SupabaseJwtSigningKeyProvider(
    IHttpClientFactory httpClientFactory,
    IMemoryCache memoryCache,
    IOptions<SupabaseOptions> supabaseOptions,
    ILogger<SupabaseJwtSigningKeyProvider> logger)
{
    public const string HttpClientName = nameof(SupabaseJwtSigningKeyProvider);

    public IEnumerable<SecurityKey> Resolve(string? kid)
    {
        var supabase = supabaseOptions.Value;
        if (string.IsNullOrWhiteSpace(supabase.Url))
        {
            return [];
        }

        var jwksUri = $"{supabase.Url.TrimEnd('/')}/auth/v1/.well-known/jwks.json";
        var keySet = LoadJsonWebKeySet(jwksUri, refresh: false);
        var keys = ResolveKeys(keySet, kid);
        if (keys.Any())
        {
            return keys;
        }

        logger.LogWarning(
            "Supabase JWKS cache miss for kid {Kid}. Refreshing keys from {JwksUri}.",
            kid ?? "(none)",
            jwksUri);

        keySet = LoadJsonWebKeySet(jwksUri, refresh: true);
        keys = ResolveKeys(keySet, kid);

        if (!keys.Any())
        {
            var availableKids = keySet.GetSigningKeys()
                .Select((key) => key.KeyId ?? "(null)")
                .ToArray();

            logger.LogWarning(
                "No Supabase signing key matched kid {Kid}. Available kids: {AvailableKids}",
                kid ?? "(none)",
                string.Join(", ", availableKids));
        }

        return keys;
    }

    private JsonWebKeySet LoadJsonWebKeySet(string jwksUri, bool refresh)
    {
        if (refresh)
        {
            memoryCache.Remove(jwksUri);
        }

        return memoryCache.GetOrCreate(jwksUri, (entry) =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6);

            var client = httpClientFactory.CreateClient(HttpClientName);
            var json = client.GetStringAsync(jwksUri).GetAwaiter().GetResult();
            var keySet = new JsonWebKeySet(json);

            logger.LogInformation(
                "Loaded Supabase JWKS from {JwksUri}. Keys: {KeyIds}",
                jwksUri,
                string.Join(", ", keySet.GetSigningKeys().Select((key) => key.KeyId ?? "(null)")));

            return keySet;
        }) ?? throw new InvalidOperationException($"Unable to load Supabase JWKS from {jwksUri}.");
    }

    private static IEnumerable<SecurityKey> ResolveKeys(JsonWebKeySet keySet, string? kid)
    {
        var keys = keySet.GetSigningKeys();

        if (string.IsNullOrWhiteSpace(kid))
        {
            return keys;
        }

        return keys.Where((key) => string.Equals(key.KeyId, kid, StringComparison.Ordinal));
    }
}
