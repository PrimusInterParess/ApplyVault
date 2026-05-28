using ApplyVault.Api.Services;

namespace ApplyVault.Api.Tests;

public sealed class CvExportInlineParserTests
{
    [Fact]
    public void ParseRuns_splits_bold_segments()
    {
        var runs = CvExportInlineParser.ParseRuns("**Acme Corp** · 2020 – 2024");

        Assert.Contains(runs, (run) => run.Bold && run.Text == "Acme Corp");
        Assert.Contains(runs, (run) => !run.Bold && run.Text.Contains("2020", StringComparison.Ordinal));
    }

    [Fact]
    public void ParseRuns_splits_italic_segments()
    {
        var runs = CvExportInlineParser.ParseRuns("*emphasis*");

        Assert.Single(runs);
        Assert.True(runs[0].Italic);
        Assert.Equal("emphasis", runs[0].Text);
    }

    [Fact]
    public void ParseRuns_parses_links()
    {
        var runs = CvExportInlineParser.ParseRuns("See [docs](https://example.com) now");

        Assert.Contains(runs, (run) => run.LinkUrl == "https://example.com/" && run.Text == "docs");
    }

    [Fact]
    public void RenderHtml_outputs_strong_tags()
    {
        var html = CvExportInlineHtmlRenderer.Render("**Led** migration");

        Assert.Contains("<strong>Led</strong>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("**", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("https://example.com", "https://example.com/")]
    [InlineData("mailto:test@example.com", "mailto:test@example.com")]
    public void SanitizeLinkUrl_allows_safe_schemes(string url, string? expected)
    {
        Assert.Equal(expected, CvExportInlineParser.SanitizeLinkUrl(url));
    }
}
