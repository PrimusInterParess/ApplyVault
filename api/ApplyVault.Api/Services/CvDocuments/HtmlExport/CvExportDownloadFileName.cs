using System.Text;
using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services.HtmlExport;

/// <summary>
/// Builds Content-Disposition download names for formatted CV PDF exports:
/// <c>{PersonName}-CV.pdf</c>, falling back to <c>CV.pdf</c>.
/// </summary>
public static class CvExportDownloadFileName
{
    private const string Suffix = "CV";

    public static string Build(string? personName)
    {
        var personSegment = SanitizeSegment(personName);
        var baseName = string.IsNullOrWhiteSpace(personSegment)
            ? Suffix
            : $"{personSegment}-{Suffix}";

        return $"{baseName}.pdf";
    }

    public static string BuildForExport(CvStructuredDocumentDto? structured) =>
        Build(ResolvePersonName(structured));

    public static string? ResolvePersonName(CvStructuredDocumentDto? structured)
    {
        if (structured is null)
        {
            return null;
        }

        foreach (var section in structured.Sections)
        {
            if (!IsContactSection(section))
            {
                continue;
            }

            foreach (var entry in section.Entries)
            {
                if (entry.Title.Equals("Name", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(entry.Subtitle))
                {
                    return entry.Subtitle.Trim();
                }
            }
        }

        return null;
    }

    private static bool IsContactSection(CvStructuredSectionDto section) =>
        section.SectionType.Equals("Contact", StringComparison.OrdinalIgnoreCase)
        || section.Heading.Equals("Contact", StringComparison.OrdinalIgnoreCase);

    private static string SanitizeSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(normalized.Length);
        var lastWasHyphen = false;

        foreach (var ch in normalized)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!lastWasHyphen && builder.Length > 0)
                {
                    builder.Append('-');
                    lastWasHyphen = true;
                }

                continue;
            }

            if (ch is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*' || char.IsControl(ch))
            {
                continue;
            }

            builder.Append(ch);
            lastWasHyphen = false;
        }

        return builder.ToString().Trim('-');
    }
}
