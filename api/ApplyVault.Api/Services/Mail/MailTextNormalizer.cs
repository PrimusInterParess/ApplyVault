using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ApplyVault.Api.Services;

internal static class MailTextNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var transliterated = value
            .Replace("Æ", "Ae", StringComparison.Ordinal)
            .Replace("æ", "ae", StringComparison.Ordinal)
            .Replace("Ø", "Oe", StringComparison.Ordinal)
            .Replace("ø", "oe", StringComparison.Ordinal)
            .Replace("Å", "Aa", StringComparison.Ordinal)
            .Replace("å", "aa", StringComparison.Ordinal);
        var decomposed = transliterated.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length * 2);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            switch (char.ToLowerInvariant(character))
            {
                case '\u2013':
                case '\u2014':
                case '\u2212':
                    builder.Append('-');
                    break;
                case '\u00b7':
                    builder.Append(' ');
                    break;
                default:
                    builder.Append(char.ToLowerInvariant(character));
                    break;
            }
        }

        return Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
    }

    public static string BuildSearchText(GmailMessage message) =>
        Normalize($"{message.Subject} {message.From} {message.Snippet} {message.BodyText}");

    public static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
