namespace ApplyVault.Api.Options;

public sealed class ScrapeResultEnrichmentOptions
{
    public const string SectionName = "ScrapeResultEnrichment";

    public bool Enabled { get; set; } = true;

    public bool FailOnAiError { get; set; }
}
