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

        Assert.Contains("margin-bottom: 11px !important;", css, StringComparison.Ordinal);
        Assert.Contains(".entry {\n  margin-bottom: 8px !important;\n}", css, StringComparison.Ordinal);
        Assert.Contains("--cv-page-pad-y: 10mm !important;", css, StringComparison.Ordinal);
        Assert.Contains("--cv-page-pad-x: 12mm !important;", css, StringComparison.Ordinal);
        Assert.Contains(".cv-classic {\n  padding: var(--cv-page-pad-y) var(--cv-page-pad-x) !important;\n}", css, StringComparison.Ordinal);
        Assert.Contains(".cv-body {\n  padding: 10mm 12mm !important;\n}", css, StringComparison.Ordinal);
        Assert.Contains(".cv-layout .cv-sidebar {\n  padding: 10mm 8mm 10mm 12mm !important;\n}", css, StringComparison.Ordinal);
        Assert.Contains(".cv-classic .cv-photo {\n  width: 96px !important;", css, StringComparison.Ordinal);
        Assert.Contains(".cv-layout .cv-sidebar .cv-photo {\n  width: 96px !important;", css, StringComparison.Ordinal);
        Assert.Contains(".cv-body .cv-photo {\n  width: 96px !important;", css, StringComparison.Ordinal);
        Assert.DoesNotContain("margin-bottom: 5px !important;", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".entry {\n  margin-bottom: 3px !important;\n}", css, StringComparison.Ordinal);
        Assert.DoesNotContain("width: 80px !important;", css, StringComparison.Ordinal);
        Assert.DoesNotContain("width: 72px !important;", css, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, 128, 124, 120, 14, 16)]
    [InlineData(2, 116, 112, 108, 12, 14)]
    [InlineData(3, 104, 100, 96, 11, 13)]
    [InlineData(4, 96, 96, 96, 10, 12)]
    public void Build_photo_and_pad_ramp_matches_ux_spec(
        int level,
        int classicPhoto,
        int modernPhoto,
        int minimalPhoto,
        int padY,
        int padX)
    {
        var css = Normalize(CvExportCompactCssBuilder.Build(new CvPdfRenderOptions(CompactLevel: level)));

        Assert.True(classicPhoto >= 96);
        Assert.True(modernPhoto >= 96);
        Assert.True(minimalPhoto >= 96);
        Assert.Contains($".cv-classic .cv-photo {{\n  width: {classicPhoto}px !important;", css, StringComparison.Ordinal);
        Assert.Contains($".cv-layout .cv-sidebar .cv-photo {{\n  width: {modernPhoto}px !important;", css, StringComparison.Ordinal);
        Assert.Contains($".cv-body .cv-photo {{\n  width: {minimalPhoto}px !important;", css, StringComparison.Ordinal);
        Assert.Contains($"--cv-page-pad-y: {padY}mm !important;", css, StringComparison.Ordinal);
        Assert.Contains($"--cv-page-pad-x: {padX}mm !important;", css, StringComparison.Ordinal);
    }

    private static string Normalize(string css) =>
        css.Replace("\r\n", "\n", StringComparison.Ordinal);
}
