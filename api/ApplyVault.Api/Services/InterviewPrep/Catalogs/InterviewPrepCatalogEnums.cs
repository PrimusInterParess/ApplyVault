using System.Text.Json;

namespace ApplyVault.Api.Services.InterviewPrep.Catalogs;

/// <summary>
/// Evidence classification for ledger entries (M4+) and plan expectations (M3).
/// Missing information is <see cref="Unknown"/>, never treated as weak.
/// </summary>
public enum InterviewEvidenceClassification
{
    Claimed,
    Observed,
    Corroborated,
    Absent,
    Unknown
}

public enum InterviewEvidenceStrength
{
    Strong,
    Moderate,
    Weak,
    Unknown
}

public enum InterviewEvidenceConfidence
{
    High,
    Medium,
    Low,
    Unknown
}

public enum InterviewCoverageState
{
    NotStarted,
    InProgress,
    Covered,
    GapsRemain,
    Unknown
}

public static class InterviewPrepCatalogNames
{
    public static string ToWire(Enum value) =>
        JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    public static bool TryParse<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (TEnum candidate in Enum.GetValues<TEnum>())
        {
            if (string.Equals(value, candidate.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, ToWire(candidate), StringComparison.OrdinalIgnoreCase))
            {
                result = candidate;
                return true;
            }
        }

        return false;
    }
}
