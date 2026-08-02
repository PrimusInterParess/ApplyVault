using System.Text.RegularExpressions;

namespace ApplyVault.Api.Services;

/// <summary>
/// Light defense-in-depth cleanup when the model echoes section/entry ids into free-text.
/// Structured sectionId/entryId fields are left untouched by callers.
/// </summary>
internal static partial class CvAiUserFacingText
{
    private static readonly Regex LabeledIdInParens = LabeledIdInParensRegex();
    private static readonly Regex LabeledIdBare = LabeledIdBareRegex();
    private static readonly Regex BareGuid = BareGuidRegex();
    private static readonly Regex ExcessWhitespace = ExcessWhitespaceRegex();
    private static readonly Regex TrailingSeparator = TrailingSeparatorRegex();
    private static readonly Regex LeadingSeparator = LeadingSeparatorRegex();

    public static string StripIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value ?? string.Empty;
        }

        var cleaned = LabeledIdInParens.Replace(value, string.Empty);
        cleaned = LabeledIdBare.Replace(cleaned, string.Empty);
        cleaned = BareGuid.Replace(cleaned, string.Empty);
        cleaned = ExcessWhitespace.Replace(cleaned, " ").Trim();
        cleaned = TrailingSeparator.Replace(cleaned, string.Empty).Trim();
        cleaned = LeadingSeparator.Replace(cleaned, string.Empty).Trim();
        return cleaned;
    }

    [GeneratedRegex(
        @"\s*\(\s*(?:id|sectionId|entryId)\s*[:=]\s*[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LabeledIdInParensRegex();

    [GeneratedRegex(
        @"\b(?:id|sectionId|entryId)\s*[:=]\s*[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LabeledIdBareRegex();

    [GeneratedRegex(
        @"\b[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex BareGuidRegex();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex ExcessWhitespaceRegex();

    [GeneratedRegex(@"\s*[—–,:;-]+\s*$")]
    private static partial Regex TrailingSeparatorRegex();

    [GeneratedRegex(@"^\s*[—–,:;-]+\s*")]
    private static partial Regex LeadingSeparatorRegex();
}
