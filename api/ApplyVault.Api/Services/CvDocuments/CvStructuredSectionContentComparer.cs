using System.Text;
using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

/// <summary>
/// Readable content fingerprint for Assist Current vs Proposed / no-op detection.
/// Mirrors FE <c>formatSectionForAssistCompare</c> field coverage (not id/sortOrder),
/// with Skills bullets↔techStack normalized the same way as the update client.
/// </summary>
internal static class CvStructuredSectionContentComparer
{
    public static bool Equals(CvStructuredSectionDto left, CvStructuredSectionDto right) =>
        Fingerprint(left) == Fingerprint(right);

    public static string Fingerprint(CvStructuredSectionDto section)
    {
        var builder = new StringBuilder();
        var heading = section.Heading.Trim();
        builder.AppendLine(string.IsNullOrWhiteSpace(heading) ? section.SectionType : heading);
        var isSkills = section.SectionType.Equals(CvSectionTypes.Skills, StringComparison.OrdinalIgnoreCase);

        foreach (var entry in section.Entries.OrderBy((entry) => entry.SortOrder))
        {
            var (bullets, techStack) = NormalizeSkillsShape(entry, isSkills);

            AppendIfPresent(builder, entry.Title);
            AppendIfPresent(builder, entry.Subtitle);
            AppendIfPresent(builder, entry.DateRange);
            AppendIfPresent(builder, entry.Summary);

            foreach (var bullet in bullets)
            {
                builder.AppendLine($"• {bullet}");
            }

            AppendIfPresent(builder, techStack);
            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }

    private static (IReadOnlyList<string> Bullets, string TechStack) NormalizeSkillsShape(
        CvStructuredEntryDto entry,
        bool isSkills)
    {
        var bullets = entry.Bullets
            .Where((bullet) => !string.IsNullOrWhiteSpace(bullet))
            .Select((bullet) => bullet.Trim())
            .ToArray();
        var techStack = entry.TechStack?.Trim() ?? string.Empty;

        if (!isSkills || bullets.Length == 0)
        {
            return (bullets, techStack);
        }

        if (string.IsNullOrWhiteSpace(techStack))
        {
            techStack = string.Join(", ", bullets);
        }

        return ([], techStack);
    }

    private static void AppendIfPresent(StringBuilder builder, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        builder.AppendLine(value.Trim());
    }
}
