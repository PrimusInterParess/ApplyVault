using ApplyVault.Api.Services;
using ApplyVault.Api.Services.HtmlExport;

namespace ApplyVault.Api.Tests;

public sealed class CvExportCompactCssBuilderTests
{
    [Fact]
    public void Build_level_0_returns_empty()
    {
        var css = CvExportCompactCssBuilder.Build(CvPdfRenderOptions.Normal);

        Assert.Equal(string.Empty, css);
    }

    [Fact]
    public void Build_level_4_respects_section_entry_and_photo_floors()
    {
        var css = Normalize(CvExportCompactCssBuilder.Build(new CvPdfRenderOptions(CompactLevel: 4)));

        Assert.Contains("margin-bottom: 4px !important;", css, StringComparison.Ordinal);
        Assert.Contains(".entry {\n  margin-bottom: 3px !important;\n}", css, StringComparison.Ordinal);
        Assert.Contains("--cv-page-pad-y: 8mm !important;", css, StringComparison.Ordinal);
        Assert.Contains("--cv-page-pad-x: 9mm !important;", css, StringComparison.Ordinal);
        Assert.Contains("--cv-space-section: 4pt !important;", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".cv-classic", css, StringComparison.Ordinal);
        Assert.Contains(".cv-body {\n  padding: 8mm 10mm 8mm 10mm !important;\n}", css, StringComparison.Ordinal);
        Assert.Contains(".cv-layout .cv-sidebar > .cv-page-pad {\n  padding: 8mm 7mm 8mm 9mm !important;\n}", css, StringComparison.Ordinal);
        Assert.Contains(".cv-layout .cv-sidebar .cv-photo {\n  width: 96px !important;", css, StringComparison.Ordinal);
        Assert.Contains(".cv-body .cv-photo {\n  width: 104px !important;", css, StringComparison.Ordinal);
        Assert.DoesNotContain("width: 80px !important;", css, StringComparison.Ordinal);
        Assert.DoesNotContain("width: 72px !important;", css, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, 120, 148, 12, 12)]
    [InlineData(2, 108, 132, 10, 11)]
    [InlineData(3, 100, 116, 9, 10)]
    [InlineData(4, 96, 104, 8, 9)]
    public void Build_photo_and_pad_ramp_matches_ux_spec(
        int level,
        int modernPhoto,
        int minimalPhoto,
        int padY,
        int padX)
    {
        var css = Normalize(CvExportCompactCssBuilder.Build(new CvPdfRenderOptions(CompactLevel: level)));

        Assert.True(modernPhoto >= 96);
        Assert.True(minimalPhoto >= 96);
        Assert.Contains($".cv-layout .cv-sidebar .cv-photo {{\n  width: {modernPhoto}px !important;", css, StringComparison.Ordinal);
        Assert.Contains($".cv-body .cv-photo {{\n  width: {minimalPhoto}px !important;", css, StringComparison.Ordinal);
        Assert.Contains($"--cv-page-pad-y: {padY}mm !important;", css, StringComparison.Ordinal);
        Assert.Contains($"--cv-page-pad-x: {padX}mm !important;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_level_1_shrinks_section_rhythm()
    {
        var css = Normalize(CvExportCompactCssBuilder.Build(new CvPdfRenderOptions(CompactLevel: 1)));

        Assert.Contains(".section {\n  margin-bottom: 8px !important;\n}", css, StringComparison.Ordinal);
        Assert.Contains("--cv-space-section: 8pt !important;", css, StringComparison.Ordinal);
        Assert.Contains("--cv-page-pad-y: 12mm !important;", css, StringComparison.Ordinal);
    }

    private static string Normalize(string css) =>
        css.Replace("\r\n", "\n", StringComparison.Ordinal);
}
