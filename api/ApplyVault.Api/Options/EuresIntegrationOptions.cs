namespace ApplyVault.Api.Options;

public sealed class EuresIntegrationOptions
{
    public const string SectionName = "EuresIntegration";

    public string BaseUrl { get; set; } = "https://europa.eu/eures/api";

    public string DefaultLocationCode { get; set; } = "dk";

    public int MaxResultsPerPage { get; set; } = 50;

    public int MaxUpstreamScanPages { get; set; } = 10;

    public int MaxCachedRankedResults { get; set; } = 500;

    public int TimeoutSeconds { get; set; } = 30;
}
