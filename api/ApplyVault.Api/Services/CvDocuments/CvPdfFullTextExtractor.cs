using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ApplyVault.Api.Services;

public sealed record CvPdfRawSection(string Heading, string NormalizedKey, int PageIndex, string Text);

public interface ICvPdfFullTextExtractor
{
    IReadOnlyList<CvPdfRawSection> ExtractSections(Stream pdfStream);
}

public sealed class CvPdfFullTextExtractor : ICvPdfFullTextExtractor
{
    public IReadOnlyList<CvPdfRawSection> ExtractSections(Stream pdfStream)
    {
        if (pdfStream.CanSeek)
        {
            pdfStream.Position = 0;
        }

        var orderedLines = new List<(int PageIndex, double YPoints, string Text)>();

        using (var document = PdfDocument.Open(pdfStream))
        {
            for (var pageIndex = 0; pageIndex < document.NumberOfPages; pageIndex++)
            {
                var page = document.GetPage(pageIndex + 1);

                foreach (var line in ExtractLines(page))
                {
                    orderedLines.Add((pageIndex, line.YPoints, line.Text));
                }
            }
        }

        if (orderedLines.Count == 0)
        {
            return [];
        }

        orderedLines.Sort(static (left, right) =>
        {
            var pageCompare = left.PageIndex.CompareTo(right.PageIndex);

            return pageCompare != 0 ? pageCompare : right.YPoints.CompareTo(left.YPoints);
        });

        var sections = new List<(string Heading, string NormalizedKey, int PageIndex, List<string> BodyLines)>();
        List<string>? currentBody = null;
        string currentHeading = "Profile";
        string currentNormalizedKey = "summary";
        var currentPageIndex = 0;

        foreach (var line in orderedLines)
        {
            if (CvPdfSectionDetector.TryMatchSectionHeading(line.Text, out var normalizedKey))
            {
                FlushSection();

                currentHeading = line.Text.Trim();
                currentNormalizedKey = normalizedKey;
                currentPageIndex = line.PageIndex;
                currentBody = [];
                continue;
            }

            currentBody ??= [];
            currentBody.Add(line.Text);
        }

        FlushSection();
        return sections
            .Select((section) => new CvPdfRawSection(
                section.Heading,
                section.NormalizedKey,
                section.PageIndex,
                string.Join('\n', section.BodyLines)))
            .ToArray();

        void FlushSection()
        {
            if (currentBody is null)
            {
                return;
            }

            sections.Add((currentHeading, currentNormalizedKey, currentPageIndex, currentBody));
            currentBody = null;
        }
    }

    private static IEnumerable<(string Text, double YPoints)> ExtractLines(Page page)
    {
        var words = page.GetWords().ToArray();

        if (words.Length == 0)
        {
            yield break;
        }

        foreach (var group in words
                     .GroupBy((word) => Math.Round(word.BoundingBox.Bottom, 0))
                     .OrderByDescending((group) => group.Key))
        {
            var text = string.Join(
                " ",
                group
                    .OrderBy((word) => word.BoundingBox.Left)
                    .Select((word) => word.Text))
                .Trim();

            if (!string.IsNullOrWhiteSpace(text))
            {
                yield return (text, group.Key);
            }
        }
    }
}
