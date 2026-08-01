namespace ApplyVault.Api.Services;

public enum CvPdfExtractionQuality
{
    Good = 0,
    Sparse = 1,
    Empty = 2
}

public sealed record CvPdfExtractedLine(int PageIndex, double YPoints, string Text);

public sealed record CvPdfExtractionResult(
    IReadOnlyList<CvPdfExtractedLine> Lines,
    IReadOnlyList<CvPdfRawSection> Sections,
    int PageCount,
    int CharCount,
    int WordCount,
    CvPdfExtractionQuality Quality);
