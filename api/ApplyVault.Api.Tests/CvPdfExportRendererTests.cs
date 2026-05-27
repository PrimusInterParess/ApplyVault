using ApplyVault.Api.Services;
using PdfSharp.Fonts;
using UglyToad.PdfPig;

namespace ApplyVault.Api.Tests;

public sealed class CvPdfExportRendererTests
{
    private static readonly byte[] TinyPngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    static CvPdfExportRendererTests()
    {
        if (GlobalFontSettings.FontResolver is null)
        {
            GlobalFontSettings.FontResolver = ApplyVaultPdfFontResolver.Instance;
        }
    }

    [Fact]
    public void Render_produces_pdf_with_structured_section_content()
    {
        var renderer = new CvPdfExportRenderer();
        var request = new CvExportRenderRequest(
            CvExportLayoutDefaults.Document(),
            [
                new CvExportSection(
                    "Experience",
                    CvSectionTypes.Experience,
                    0,
                    [
                        new CvExportEntry(
                            "Software Engineer",
                            "Acme Corp",
                            "2020 – 2024",
                            "Built reliable services.",
                            ["Shipped MVP"],
                            "C#, PostgreSQL")
                    ])
            ],
            null,
            null);

        var pdfBytes = renderer.Render(request);

        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdfBytes[..4]));
        var text = ExtractText(pdfBytes);
        Assert.Contains("Engineer", text);
        Assert.Contains("Shipped", text);
        Assert.Contains("Acme", text);
    }

    [Fact]
    public void Render_with_profile_photo_always_shows_when_bytes_present()
    {
        var renderer = new CvPdfExportRenderer();
        var hiddenPhotoLayout = new CvExportDocumentLayout(44, "hidden", 92, 40, 5);
        var request = new CvExportRenderRequest(
            hiddenPhotoLayout,
            [
                new CvExportSection(
                    "Summary",
                    CvSectionTypes.Summary,
                    0,
                    [
                        new CvExportEntry(
                            string.Empty,
                            null,
                            null,
                            "Experienced engineer with backend focus.",
                            [],
                            string.Empty)
                    ])
            ],
            TinyPngBytes,
            "image/png");

        var pdfBytes = renderer.Render(request);

        Assert.True(pdfBytes.Length > 500);
        var text = ExtractText(pdfBytes);
        Assert.Contains("Experienced", text);
        Assert.Contains("engineer", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_orders_sections_by_sort_order()
    {
        var renderer = new CvPdfExportRenderer();
        var request = new CvExportRenderRequest(
            CvExportLayoutDefaults.Document(),
            [
                new CvExportSection(
                    "Experience",
                    CvSectionTypes.Experience,
                    2,
                    [new CvExportEntry("Role B", null, null, string.Empty, [], string.Empty)]),
                new CvExportSection(
                    "Summary",
                    CvSectionTypes.Summary,
                    0,
                    [new CvExportEntry(string.Empty, null, null, "Summary text first.", [], string.Empty)]),
                new CvExportSection(
                    "Education",
                    CvSectionTypes.Education,
                    1,
                    [new CvExportEntry("Degree A", null, null, string.Empty, [], string.Empty)])
            ],
            null,
            null);

        var pdfBytes = renderer.Render(request);
        var text = ExtractText(pdfBytes);
        var summaryIndex = text.IndexOf("Summary", StringComparison.Ordinal);
        var educationIndex = text.IndexOf("Degree", StringComparison.Ordinal);
        var experienceIndex = text.IndexOf("Role", StringComparison.Ordinal);

        Assert.True(summaryIndex >= 0);
        Assert.True(educationIndex > summaryIndex);
        Assert.True(experienceIndex > educationIndex);
    }

    [Fact]
    public void Render_includes_contact_in_header()
    {
        var renderer = new CvPdfExportRenderer();
        var request = new CvExportRenderRequest(
            CvExportLayoutDefaults.Document(),
            [
                new CvExportSection(
                    "Contact",
                    CvSectionTypes.Custom,
                    0,
                    [
                        new CvExportEntry(
                            "Jane Doe",
                            null,
                            null,
                            string.Empty,
                            ["jane@example.com", "+45 12 34 56 78"],
                            string.Empty)
                    ]),
                new CvExportSection(
                    "Summary",
                    CvSectionTypes.Summary,
                    1,
                    [
                        new CvExportEntry(
                            string.Empty,
                            null,
                            null,
                            "Experienced software engineer.",
                            [],
                            string.Empty)
                    ]),
                new CvExportSection(
                    "Experience",
                    CvSectionTypes.Experience,
                    2,
                    [
                        new CvExportEntry(
                            "Software Engineer",
                            "Acme Corp",
                            "2020 – 2024",
                            string.Empty,
                            [],
                            string.Empty)
                    ])
            ],
            null,
            null);

        var text = ExtractText(renderer.Render(request));

        Assert.Contains("Jane", text);
        Assert.Contains("jane@example.com", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CONTACT", text);
        Assert.True(text.IndexOf("jane@example.com", StringComparison.OrdinalIgnoreCase)
            < text.IndexOf("EXPERIENCE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Render_empty_sections_throws()
    {
        var renderer = new CvPdfExportRenderer();
        var request = new CvExportRenderRequest(CvExportLayoutDefaults.Document(), [], null, null);

        Assert.Throws<InvalidOperationException>(() => renderer.Render(request));
    }

    private static string ExtractText(byte[] pdfBytes)
    {
        using var document = PdfDocument.Open(pdfBytes);

        return string.Join(' ', document.GetPages().SelectMany((page) => page.GetWords()).Select((word) => word.Text));
    }
}
