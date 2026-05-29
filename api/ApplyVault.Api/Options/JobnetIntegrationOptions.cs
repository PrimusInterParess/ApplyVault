namespace ApplyVault.Api.Options;

public sealed class JobnetIntegrationOptions
{
    public const string SectionName = "JobnetIntegration";

    public const string DefaultUserAgent = "ApplyVault/1.0 (+https://github.com/yborisov/jobapplications)";

    public string BaseUrl { get; set; } = "https://jobnet.dk/bff";

    public string SearchPath { get; set; } = "/FindJob/Search";

    public string DetailPathTemplate { get; set; } = "/FindJob/JobAdDetails/{id}";

    public string UserAgent { get; set; } = DefaultUserAgent;

    public bool WorkInDenmarkOnly { get; set; } = true;

    public int MaxResultsPerPage { get; set; } = 50;

    public int RankedCacheTtlMinutes { get; set; } = 5;

    public int TimeoutSeconds { get; set; } = 30;

    public int MaxUpstreamScanPages { get; set; } = 10;

    public int ScanResultsPerPage { get; set; } = 10;

    public int MaxCachedRankedResults { get; set; } = 500;

    public int MaxClassificationDetailFetches { get; set; } = 30;

    public int SearchMaxRetryAttempts { get; set; } = 3;

    public int MaxDetailFetchConcurrency { get; set; } = 6;

    public int ClassificationCacheTtlMinutes { get; set; } = 60;

    public int DefaultKmRadius { get; set; } = 50;

    public string DefaultOrderType { get; set; } = "BestMatch";
}
