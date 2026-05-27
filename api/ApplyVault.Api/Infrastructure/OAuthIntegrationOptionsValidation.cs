using ApplyVault.Api.Options;

namespace ApplyVault.Api.Infrastructure;

internal static class OAuthIntegrationOptionsValidation
{
    internal static bool ValidateCalendarIntegration(CalendarIntegrationOptions options, bool requireHttps)
    {
        var googleConfigured = IsProviderConfigured(options.Google.ClientId);
        var microsoftConfigured = IsProviderConfigured(options.Microsoft.ClientId);

        if (!googleConfigured && !microsoftConfigured)
        {
            return true;
        }

        if (googleConfigured && !IsProviderComplete(
                options.Google.ClientId,
                options.Google.ClientSecret,
                options.Google.RedirectUri,
                requireHttps))
        {
            return false;
        }

        if (microsoftConfigured && !IsProviderComplete(
                options.Microsoft.ClientId,
                options.Microsoft.ClientSecret,
                options.Microsoft.RedirectUri,
                requireHttps))
        {
            return false;
        }

        return IsPostConnectRedirectComplete(options.PostConnectRedirectUrl, requireHttps);
    }

    internal static bool ValidateMailIntegration(MailIntegrationOptions options, bool requireHttps)
    {
        if (!options.Enabled)
        {
            return true;
        }

        return IsProviderComplete(
                   options.Gmail.ClientId,
                   options.Gmail.ClientSecret,
                   options.Gmail.RedirectUri,
                   requireHttps)
               && IsPostConnectRedirectComplete(options.PostConnectRedirectUrl, requireHttps);
    }

    private static bool IsProviderConfigured(string? clientId) =>
        !string.IsNullOrWhiteSpace(clientId);

    private static bool IsProviderComplete(
        string clientId,
        string clientSecret,
        string redirectUri,
        bool requireHttps) =>
        !string.IsNullOrWhiteSpace(clientId)
        && !string.IsNullOrWhiteSpace(clientSecret)
        && IsRedirectUriComplete(redirectUri, requireHttps);

    private static bool IsPostConnectRedirectComplete(string postConnectRedirectUrl, bool requireHttps) =>
        !string.IsNullOrWhiteSpace(postConnectRedirectUrl)
        && (!requireHttps || IsHttpsUrl(postConnectRedirectUrl));

    private static bool IsRedirectUriComplete(string redirectUri, bool requireHttps) =>
        !string.IsNullOrWhiteSpace(redirectUri)
        && (!requireHttps || IsHttpsUrl(redirectUri));

    private static bool IsHttpsUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
