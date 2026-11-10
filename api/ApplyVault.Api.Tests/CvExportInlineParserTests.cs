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
    [InlineData("test@example.com", "mailto:test@example.com")]
    public void SanitizeLinkUrl_allows_safe_schemes(string url, string? expected)
    {
        Assert.Equal(expected, CvExportInlineParser.SanitizeLinkUrl(url));
    }

    [Fact]
    public void Render_leaves_a_bare_url_as_text()
    {
        var html = CvExportInlineHtmlRenderer.Render("https://github.com/jane");

        Assert.DoesNotContain("<a ", html, StringComparison.Ordinal);
        Assert.Contains("https://github.com/jane", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://github.com/jane", "https://github.com/jane")]
    [InlineData("http://github.com/jane", "http://github.com/jane")]
    [InlineData("github.com/jane", "https://github.com/jane")]
    [InlineData("www.github.com/jane", "https://www.github.com/jane")]
    [InlineData("linkedin.com/in/jane", "https://linkedin.com/in/jane")]
    [InlineData("www.linkedin.com/in/jane", "https://www.linkedin.com/in/jane")]
    [InlineData("https://example.com/portfolio", "https://example.com/portfolio")]
    public void RenderContactLine_links_bare_http_and_profile_hosts(string line, string href)
    {
        var html = CvExportInlineHtmlRenderer.RenderContactLine(line);

        Assert.Contains(
            $"<a href=\"{href}\" target=\"_blank\" rel=\"noopener noreferrer\">{line}</a>",
            html,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("jane@example.com")]
    [InlineData("+45 12 34 56 78")]
    [InlineData("example.com/portfolio")]
    [InlineData("See https://github.com/jane")]
    public void RenderContactLine_leaves_other_lines_unlinked(string line)
    {
        var html = CvExportInlineHtmlRenderer.RenderContactLine(line);

        Assert.DoesNotContain("<a ", html, StringComparison.Ordinal);
    }
}
