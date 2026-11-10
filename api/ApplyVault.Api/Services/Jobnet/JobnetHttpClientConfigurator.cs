using ApplyVault.Api.Options;

namespace ApplyVault.Api.Services.Jobnet;

internal static class JobnetHttpClientConfigurator
{
    public const string DefaultUserAgent = JobnetIntegrationOptions.DefaultUserAgent;

    public static void Configure(HttpClient httpClient, JobnetIntegrationOptions options)
    {
        httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.TimeoutSeconds));

        httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
            "Accept",
            "application/json, text/plain, */*");
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9,da;q=0.8");
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            string.IsNullOrWhiteSpace(options.UserAgent) ? DefaultUserAgent : options.UserAgent.Trim());
        var siteOrigin = ResolveSiteOrigin(options.BaseUrl);
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Referer", $"{siteOrigin}/find-job");
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Origin", siteOrigin);
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-csrf", "1");
    }

    private static string ResolveSiteOrigin(string baseUrl)
    {
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return uri.GetLeftPart(UriPartial.Authority);
        }

        return "https://workindenmark.jobnet.dk";
    }
}
