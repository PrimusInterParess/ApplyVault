namespace ApplyVault.Api.Services.Eures;

/// <summary>
/// Closed allowlists for public EURES search filter fields (slice 2 / #6).
/// Wire values match upstream jv-search JSON (string publicationPeriod; lowercase schedule codes).
/// </summary>
internal static class EuresSearchFilterCodes
{
    public static readonly HashSet<string> SortSearch = new(StringComparer.OrdinalIgnoreCase)
    {
        "MOST_RECENT",
        "BEST_MATCH"
    };

    /// <summary>
    /// Upstream accepts LAST_DAY / LAST_THREE_DAYS / LAST_WEEK / LAST_MONTH (and LAST_VISIT when authenticated).
    /// Product ship set for this slice: week + month. No LAST_THREE_MONTHS exists upstream (400).
    /// </summary>
    public static readonly HashSet<string> PublicationPeriods = new(StringComparer.OrdinalIgnoreCase)
    {
        "LAST_WEEK",
        "LAST_MONTH"
    };

    public static readonly HashSet<string> PositionScheduleCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "fulltime",
        "parttime"
    };

    public const int MaxPositionScheduleCodes = 5;

    public static string CanonicalSortSearch(string value) => value.Trim().ToUpperInvariant();

    public static string CanonicalPublicationPeriod(string value) => value.Trim().ToUpperInvariant();

    public static string CanonicalPositionScheduleCode(string value) => value.Trim().ToLowerInvariant();
}
