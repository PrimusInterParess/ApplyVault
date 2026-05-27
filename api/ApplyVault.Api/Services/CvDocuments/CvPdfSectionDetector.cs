using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ApplyVault.Api.Services;

public sealed record CvPdfDetectedSection(
    string HeadingText,
    string NormalizedKey,
    int PageIndex,
    double YPoints);

public interface ICvPdfSectionDetector
{
    IReadOnlyList<CvPdfDetectedSection> DetectSections(Stream pdfStream);
}

public sealed class CvPdfSectionDetector : ICvPdfSectionDetector
{
    private static readonly string[] KnownSectionPatterns =
    [
        "professional experience",
        "employment history",
        "work experience",
        "personal projects",
        "side projects",
        "selected projects",
        "technical skills",
        "core competencies",
        "career history",
        "work history",
        "certifications",
        "publications",
        "qualifications",
        "achievements",
        "employment",
        "experience",
        "about me",
        "volunteer",
        "education",
        "languages",
        "references",
        "objective",
        "projects",
        "honors",
        "awards",
        "contact information",
        "contact",
        "summary",
        "profile",
        "skills"
    ];

    public IReadOnlyList<CvPdfDetectedSection> DetectSections(Stream pdfStream)
    {
        if (pdfStream.CanSeek)
        {
            pdfStream.Position = 0;
        }

        var candidates = new List<(string HeadingText, string NormalizedKey, int PageIndex, double YPoints)>();

        using var document = PdfDocument.Open(pdfStream);

        for (var pageIndex = 0; pageIndex < document.NumberOfPages; pageIndex++)
        {
            var page = document.GetPage(pageIndex + 1);
            var lines = ExtractLines(page);

            foreach (var line in lines)
            {
                if (!TryMatchSectionHeading(line.Text, out var normalizedKey))
                {
                    continue;
                }

                candidates.Add((line.Text.Trim(), normalizedKey, pageIndex, line.YPoints));
            }
        }

        return candidates
            .GroupBy((candidate) => candidate.NormalizedKey, StringComparer.OrdinalIgnoreCase)
            .Select((group) =>
            {
                var first = group.OrderBy((item) => item.PageIndex).ThenByDescending((item) => item.YPoints).First();
                return new CvPdfDetectedSection(first.HeadingText, first.NormalizedKey, first.PageIndex, first.YPoints);
            })
            .OrderBy((section) => section.PageIndex)
            .ThenByDescending((section) => section.YPoints)
            .ToArray();
    }

    internal static bool TryMatchSectionHeading(string text, out string normalizedKey)
    {
        normalizedKey = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = NormalizeHeading(text);

        if (trimmed.Length > 64)
        {
            return false;
        }

        foreach (var pattern in KnownSectionPatterns)
        {
            if (trimmed.Equals(pattern, StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith($"{pattern} ", StringComparison.OrdinalIgnoreCase))
            {
                normalizedKey = pattern;
                return true;
            }
        }

        return false;
    }

    internal static string NormalizeHeading(string text)
    {
        var trimmed = text.Trim();
        trimmed = Regex.Replace(trimmed, @"\s+", " ");
        trimmed = trimmed.Trim(':', '.', '-', '–', '—', '•', '·', '|', ' ');
        trimmed = Regex.Replace(trimmed, @"^[|\-–—•·\s]+", string.Empty);
        trimmed = Regex.Replace(trimmed, @"[|\-–—•·\s]+$", string.Empty);

        return trimmed.Trim();
    }

    private static IEnumerable<(string Text, double YPoints)> ExtractLines(Page page)
    {
        var words = page.GetWords().ToArray();

        if (words.Length == 0)
        {
            yield break;
        }

        var lines = words
            .GroupBy((word) => Math.Round(word.BoundingBox.Bottom, 0))
            .OrderByDescending((group) => group.Key)
            .Select((group) =>
            {
                var text = string.Join(
                    " ",
                    group
                        .OrderBy((word) => word.BoundingBox.Left)
                        .Select((word) => word.Text));

                return (Text: text.Trim(), YPoints: group.Key);
            })
            .Where((line) => !string.IsNullOrWhiteSpace(line.Text));

        foreach (var line in lines)
        {
            yield return line;
        }
    }
}
