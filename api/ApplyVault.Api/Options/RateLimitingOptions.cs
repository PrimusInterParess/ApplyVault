namespace ApplyVault.Api.Options;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public const string PolicyGlobalApi = "global-api";
    public const string PolicyScrapeIngest = "scrape-ingest";
    public const string PolicyEuresSearch = "eures-search";
    public const string PolicyOAuthCallback = "oauth-callback";

    public bool Enabled { get; set; } = true;

    public RateLimitPolicyOptions GlobalApi { get; set; } = new()
    {
        PermitLimit = 200,
        WindowSeconds = 60
    };

    public RateLimitPolicyOptions ScrapeIngest { get; set; } = new()
    {
        PermitLimit = 30,
        WindowSeconds = 60
    };

    public RateLimitPolicyOptions EuresSearch { get; set; } = new()
    {
        PermitLimit = 20,
        WindowSeconds = 60
    };

    public RateLimitPolicyOptions OAuthCallback { get; set; } = new()
    {
        PermitLimit = 10,
        WindowSeconds = 60
    };
}

public sealed class RateLimitPolicyOptions
{
    public int PermitLimit { get; set; } = 30;

    public int WindowSeconds { get; set; } = 60;
}
