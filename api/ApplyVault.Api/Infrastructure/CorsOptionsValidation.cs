using ApplyVault.Api.Options;

namespace ApplyVault.Api.Infrastructure;

internal static class CorsOptionsValidation
{
    internal static bool Validate(CorsOptions options, bool requireHttps)
    {
        if (options.AllowedOrigins.Length == 0)
        {
            return false;
        }

        foreach (var origin in options.AllowedOrigins)
        {
            if (!IsValidOrigin(origin, requireHttps))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidOrigin(string origin, bool requireHttps)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (requireHttps)
        {
            if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        else if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                 && !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        var path = uri.AbsolutePath;
        return path is "" or "/";
    }
}
