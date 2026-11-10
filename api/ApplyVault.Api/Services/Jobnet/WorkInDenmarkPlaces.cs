namespace ApplyVault.Api.Services.Jobnet;

internal static class WorkInDenmarkPlaces
{
    public const int CommuteDistanceKm = 50;

    private static readonly Dictionary<string, string> PostalCodeByPlace = new(StringComparer.OrdinalIgnoreCase)
    {
        ["copenhagen"] = "1050",
        ["aarhus"] = "8000",
        ["odense"] = "5000",
        ["aalborg"] = "9000",
        ["randers"] = "8900"
    };

    public static bool TryResolve(string? place, out string? canonicalPlace, out string? postalCode)
    {
        if (string.IsNullOrWhiteSpace(place))
        {
            canonicalPlace = null;
            postalCode = null;
            return true;
        }

        var key = place.Trim().ToLowerInvariant();

        if (!PostalCodeByPlace.TryGetValue(key, out var resolvedPostalCode))
        {
            canonicalPlace = null;
            postalCode = null;
            return false;
        }

        canonicalPlace = key;
        postalCode = resolvedPostalCode;
        return true;
    }
}
