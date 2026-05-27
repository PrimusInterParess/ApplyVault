namespace ApplyVault.Api.Services;

internal static class CvExportTextNormalizer
{
    public static string Field(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : CollapseWhitespace(value.Trim());

    public static IReadOnlyList<string> Bullets(IReadOnlyList<string> bullets) =>
        bullets
            .Select((bullet) => Field(bullet))
            .Where((bullet) => bullet.Length > 0)
            .ToArray();

    public static IReadOnlyList<string> Paragraphs(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CollapseWhitespace)
            .Where((paragraph) => paragraph.Length > 0)
            .ToArray();
    }

    public static IReadOnlyList<string> TechItems(string? techStack)
    {
        var normalized = Field(techStack);

        if (normalized.Length == 0)
        {
            return [];
        }

        return normalized
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(CollapseWhitespace)
            .Where((item) => item.Length > 0)
            .ToArray();
    }

    private static string CollapseWhitespace(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
