using System.Text.Json;
using ApplyVault.Api.Services.CvSectionCatalog;

namespace ApplyVault.Api.Services.InterviewPrep.Planning;

/// <summary>
/// Resolves candidate display name from immutable CV snapshot (Contact / Name entry subtitle).
/// </summary>
public static class InterviewPrepCandidateDisplayNameResolver
{
    public static string? TryResolveFromCvSnapshotJson(string? cvSnapshotJson)
    {
        if (string.IsNullOrWhiteSpace(cvSnapshotJson))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(cvSnapshotJson);
            if (!doc.RootElement.TryGetProperty("sections", out var sections)
                || sections.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var section in sections.EnumerateArray())
            {
                var sectionType = ReadString(section, "sectionType") ?? ReadString(section, "SectionType");
                if (!string.Equals(sectionType, CvSectionTypes.Contact, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!section.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
                {
                    if (!section.TryGetProperty("Entries", out entries) || entries.ValueKind != JsonValueKind.Array)
                    {
                        return null;
                    }
                }

                foreach (var entry in entries.EnumerateArray())
                {
                    var title = ReadString(entry, "title") ?? ReadString(entry, "Title");
                    if (!string.Equals(title, "Name", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var subtitle = ReadString(entry, "subtitle") ?? ReadString(entry, "Subtitle");
                    if (IsPlausibleDisplayName(subtitle))
                    {
                        return subtitle!.Trim();
                    }
                }
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static bool IsPlausibleDisplayName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.Length is < 2 or > 80)
        {
            return false;
        }

        if (trimmed.Contains('@', StringComparison.Ordinal)
            || trimmed.Contains("http", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("www.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(trimmed, "SUMMARY", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Reject obvious mis-filed CV prose in the name slot.
        if (trimmed.Contains(" years of experience", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("Developer with", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }
}
