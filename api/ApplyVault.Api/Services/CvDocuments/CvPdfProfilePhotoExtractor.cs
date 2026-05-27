using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace ApplyVault.Api.Services;

public sealed record CvPdfProfilePhotoResult(byte[] ImageBytes, string ContentType);

public interface ICvPdfProfilePhotoExtractor
{
    CvPdfProfilePhotoResult? TryExtractProfilePhoto(Stream pdfStream);
}

public sealed class CvPdfProfilePhotoExtractor : ICvPdfProfilePhotoExtractor
{
    private const double MinDimensionPoints = 50;
    private const double MinAspectRatio = 0.7;
    private const double MaxAspectRatio = 1.4;
    private const double MaxBannerAspectRatio = 2.5;

    public CvPdfProfilePhotoResult? TryExtractProfilePhoto(Stream pdfStream)
    {
        if (pdfStream.CanSeek)
        {
            pdfStream.Position = 0;
        }

        using var document = PdfDocument.Open(pdfStream);
        IPdfImage? bestCandidate = null;
        var bestScore = 0d;

        var pagesToScan = Math.Min(document.NumberOfPages, 2);

        for (var pageIndex = 0; pageIndex < pagesToScan; pageIndex++)
        {
            var page = document.GetPage(pageIndex + 1);
            var pageHeight = page.Height;
            var upperHalfThreshold = pageHeight / 2;

            foreach (var image in page.GetImages())
            {
                var bounds = image.BoundingBox;
                var width = bounds.Width;
                var height = bounds.Height;

                if (width < MinDimensionPoints || height < MinDimensionPoints)
                {
                    continue;
                }

                var aspectRatio = width / height;

                if (aspectRatio < MinAspectRatio || aspectRatio > MaxAspectRatio)
                {
                    continue;
                }

                if (aspectRatio > MaxBannerAspectRatio || 1 / aspectRatio > MaxBannerAspectRatio)
                {
                    continue;
                }

                var centerY = bounds.Bottom + (height / 2);

                if (centerY < upperHalfThreshold)
                {
                    continue;
                }

                var area = width * height;
                var upperHalfBonus = centerY >= upperHalfThreshold ? 1.2 : 1;
                var score = area * upperHalfBonus;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCandidate = image;
                }
            }
        }

        if (bestCandidate is null)
        {
            return null;
        }

        return ExtractImageBytes(bestCandidate);
    }

    private static CvPdfProfilePhotoResult? ExtractImageBytes(IPdfImage image)
    {
        if (image.TryGetPng(out var pngBytes) && pngBytes.Length > 0)
        {
            return new CvPdfProfilePhotoResult(pngBytes, "image/png");
        }

        var rawBytes = image.RawBytes.ToArray();

        if (rawBytes.Length >= 3 && rawBytes[0] == 0xFF && rawBytes[1] == 0xD8 && rawBytes[2] == 0xFF)
        {
            return new CvPdfProfilePhotoResult(rawBytes, "image/jpeg");
        }

        return null;
    }
}
