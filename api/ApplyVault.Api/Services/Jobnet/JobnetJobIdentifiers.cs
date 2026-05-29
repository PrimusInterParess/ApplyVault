namespace ApplyVault.Api.Services.Jobnet;

internal static class JobnetJobIdentifiers
{
    /// <summary>
    /// Native Jobnet postings use GUID ids and support /FindJob/JobAdDetails/{id}.
    /// </summary>
    public static bool SupportsNativeDetailEndpoint(string? id)
    {
        return !string.IsNullOrWhiteSpace(id) && Guid.TryParse(id.Trim(), out _);
    }

    /// <summary>
    /// E-prefixed ids are EURES-imported listings. They return 400 on the native detail endpoint
    /// but are still listed on Jobnet and are included in Work in Denmark search results.
    /// </summary>
    public static bool IsEuresImported(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var normalizedId = id.Trim();
        return normalizedId.StartsWith("E", StringComparison.OrdinalIgnoreCase)
            && !Guid.TryParse(normalizedId, out _);
    }
}
