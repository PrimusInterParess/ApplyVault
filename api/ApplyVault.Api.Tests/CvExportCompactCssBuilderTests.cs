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
        Assert.Contains("--cv-page-pad-x: 10mm !important;", css, StringComparison.Ordinal);
        Assert.Contains("--cv-page-pad-right: 10mm !important;", css, StringComparison.Ordinal);
        Assert.Contains("--cv-space-section: 4pt !important;", css, StringComparison.Ordinal);
        Assert.Contains(".cv-body {\n  padding: 8mm 10mm 8mm 10mm !important;\n}", css, StringComparison.Ordinal);
        Assert.Contains(".cv-body .cv-photo {\n  width: 104px !important;", css, StringComparison.Ordinal);
        Assert.DoesNotContain("width: 80px !important;", css, StringComparison.Ordinal);
        Assert.DoesNotContain("width: 72px !important;", css, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, 148, 12, 14)]
    [InlineData(2, 132, 10, 12)]
    [InlineData(3, 116, 9, 11)]
    [InlineData(4, 104, 8, 10)]
    public void Build_photo_and_pad_ramp_matches_ux_spec(
        int level,
        int photo,
        int padY,
        int padX)
    {
        var css = Normalize(CvExportCompactCssBuilder.Build(new CvPdfRenderOptions(CompactLevel: level)));

        Assert.True(photo >= 96);
        Assert.Contains($".cv-body .cv-photo {{\n  width: {photo}px !important;", css, StringComparison.Ordinal);
        Assert.Contains($"--cv-page-pad-y: {padY}mm !important;", css, StringComparison.Ordinal);
        Assert.Contains($"--cv-page-pad-x: {padX}mm !important;", css, StringComparison.Ordinal);
        Assert.Contains($"--cv-page-pad-right: {padX}mm !important;", css, StringComparison.Ordinal);
        Assert.Contains(
            $".cv-body {{\n  padding: {padY}mm {padX}mm {padY}mm {padX}mm !important;\n}}",
            css,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, "0.96", 18)]
    [InlineData(2, "0.92", 17)]
    [InlineData(3, "0.86", 16)]
    [InlineData(4, "0.8", 15)]
    public void Build_scales_template_type_and_keeps_the_job_title_at_least_as_large_as_the_body(
        int level,
        string scale,
        int namePt)
    {
        var css = Normalize(CvExportCompactCssBuilder.Build(new CvPdfRenderOptions(CompactLevel: level)));

        Assert.Contains($"--cv-font-scale: {scale};", css, StringComparison.Ordinal);
        Assert.Contains("html {\n  font-size: 10.5pt;\n}", css, StringComparison.Ordinal);
        Assert.Contains(
            "html body {\n  font-size: calc(10.5pt * var(--cv-font-scale)) !important;",
            css,
            StringComparison.Ordinal);
        Assert.Contains(
            "font-size: calc(8.5pt * var(--cv-font-scale)) !important;",
            css,
            StringComparison.Ordinal);
        Assert.Contains(
            ".entry-title {\n  font-size: calc(10.5pt * var(--cv-font-scale)) !important;\n}",
            css,
            StringComparison.Ordinal);
        Assert.Contains(
            ".entry-date,\n.cv-contact-line {\n  font-size: calc(9.5pt * var(--cv-font-scale)) !important;\n}",
            css,
            StringComparison.Ordinal);
        Assert.Contains($"font-size: {namePt}pt !important;", css, StringComparison.Ordinal);
        Assert.DoesNotContain("html body {\n  font-size: 92%", css, StringComparison.Ordinal);
        Assert.DoesNotContain("html body {\n  font-size: 86%", css, StringComparison.Ordinal);
        Assert.DoesNotContain("html body {\n  font-size: 80%", css, StringComparison.Ordinal);
        Assert.DoesNotContain("html body {\n  font-size: 74%", css, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Build_emits_no_sidebar_layout_rules(int level)
    {
        var css = CvExportCompactCssBuilder.Build(new CvPdfRenderOptions(CompactLevel: level));

        Assert.DoesNotContain(".cv-layout", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".cv-page-pad", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".cv-sidebar", css, StringComparison.Ordinal);
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
