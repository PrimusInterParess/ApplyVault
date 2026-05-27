using ApplyVault.Api.Services;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace ApplyVault.Api.Tests;

public sealed class CvPdfSectionDetectorTests
{
    [Fact]
    public void DetectSections_FindsKnownHeadings()
    {
        EnsureFontResolver();

        var pdfBytes = CreatePdfWithLines(["Experience", "Company A — Developer", "Projects", "ApplyVault"]);

        using var stream = new MemoryStream(pdfBytes);
        var detector = new CvPdfSectionDetector();
        var sections = detector.DetectSections(stream);

        Assert.Contains(sections, (section) =>
            section.NormalizedKey.Equals("experience", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(sections, (section) =>
            section.NormalizedKey.Equals("projects", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryMatchSectionHeading_MatchesWorkExperience()
    {
        var matched = CvPdfSectionDetector.TryMatchSectionHeading("Work Experience", out var normalizedKey);

        Assert.True(matched);
        Assert.Equal("work experience", normalizedKey);
    }

    private static byte[] CreatePdfWithLines(IReadOnlyList<string> lines)
    {
        using var document = new PdfDocument();
        var page = document.AddPage();
        using var graphics = XGraphics.FromPdfPage(page);
        var font = new XFont("Arial", 14, XFontStyleEx.Bold);
        var y = 50d;

        foreach (var line in lines)
        {
            graphics.DrawString(line, font, XBrushes.Black, 50, y);
            y += 24;
        }

        using var stream = new MemoryStream();
        document.Save(stream);
        return stream.ToArray();
    }

    private static void EnsureFontResolver()
    {
        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.FontResolver = ApplyVaultPdfFontResolver.Instance;
        }
    }
}
