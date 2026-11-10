using ApplyVault.Api.Services;
using ApplyVault.Api.Services.HtmlExport;

namespace ApplyVault.Api.Tests;

/// <summary>
/// Guards multi-page PDF inset: the .cv-body shell padding must clone onto the page 2 fragment.
/// One page is recommended and two is the maximum; this only covers overflow formatting.
/// </summary>
public sealed class CvExportPrintCssMultiPageTests
{
    private static string TemplatesDir
    {
        get
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                var nested = Path.Combine(dir.FullName, "api", "ApplyVault.Api", "wwwroot", "cv-export-templates");
                if (Directory.Exists(nested))
                {
                    return nested;
                }

                var sibling = Path.Combine(dir.FullName, "ApplyVault.Api", "wwwroot", "cv-export-templates");
                if (Directory.Exists(sibling))
                {
                    return sibling;
                }
            }

            throw new DirectoryNotFoundException(
                $"Could not locate cv-export-templates from {AppContext.BaseDirectory}.");
        }
    }

    [Fact]
    public void PrintCss_clones_page_shell_padding_and_keeps_zero_page_margin()
    {
        var css = Normalize(File.ReadAllText(Path.Combine(TemplatesDir, "cv-export-print.css")));

        Assert.Contains("@page {\n  size: A4;\n  margin: 0;\n}", css, StringComparison.Ordinal);
        Assert.Contains(
            ".cv-body {\n  -webkit-box-decoration-break: clone;\n  box-decoration-break: clone;\n}",
            css,
            StringComparison.Ordinal);
        Assert.Contains("@page :first {\n  margin: 0;\n  @top-left {\n    content: none;\n  }\n}", css, StringComparison.Ordinal);
        Assert.Contains("content: var(--cv-running-name);", css, StringComparison.Ordinal);
        Assert.Contains("margin-left: var(--cv-page-pad-x);", css, StringComparison.Ordinal);
        Assert.Contains("font-size: 9pt;", css, StringComparison.Ordinal);
        Assert.Contains("@page :left {\n  margin-top: 8mm;", css, StringComparison.Ordinal);
        Assert.Contains("@page :right {\n  margin-top: 8mm;", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".cv-layout", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".cv-page-pad", css, StringComparison.Ordinal);
    }

    [Fact]
    public void PrintCss_continues_a_section_between_entries_and_keeps_each_entry_whole()
    {
        var css = Normalize(File.ReadAllText(Path.Combine(TemplatesDir, "cv-export-print.css")));

        Assert.Contains(".section {\n  break-inside: auto;\n  page-break-inside: auto;", css, StringComparison.Ordinal);
        Assert.Contains(".section-title {\n  break-after: avoid;\n  page-break-after: avoid;\n}", css, StringComparison.Ordinal);
        Assert.Contains(".entry {\n  break-inside: avoid;\n  page-break-inside: avoid;\n}", css, StringComparison.Ordinal);
        Assert.Contains(".entry-bullets {\n  break-inside: auto;\n  page-break-inside: auto;\n}", css, StringComparison.Ordinal);
        Assert.Contains(
            ".entry-bullets li {\n  break-inside: avoid;\n  page-break-inside: avoid;",
            css,
            StringComparison.Ordinal);
        Assert.Contains(
            ".entry-header {\n  flex-wrap: wrap;\n  gap: 4px 8px;\n  break-inside: avoid;\n  page-break-inside: avoid;\n}",
            css,
            StringComparison.Ordinal);
        Assert.Contains(
            ".entry-subtitle,\n.entry-summary {\n  break-inside: avoid;\n  page-break-inside: avoid;\n}",
            css,
            StringComparison.Ordinal);
        Assert.Contains(".cv-header {\n  break-inside: avoid;\n  page-break-inside: avoid;\n}", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Template_directory_contains_only_the_single_template()
    {
        var htmlFiles = Directory.GetFiles(TemplatesDir, "*.html")
            .Select(Path.GetFileName)
            .ToArray();

        Assert.Equal(["cv-template.html"], htmlFiles);
    }

    [Fact]
    public void Template_puts_padding_on_cloneable_cv_body_shell()
    {
        var html = Normalize(File.ReadAllText(Path.Combine(TemplatesDir, "cv-template.html")));

        Assert.Contains("class=\"cv-body\"", html, StringComparison.Ordinal);
        Assert.Contains(
            ".cv-body {\n      padding: var(--cv-page-pad-y) var(--cv-page-pad-right) var(--cv-page-pad-y) var(--cv-page-pad-x);",
            html,
            StringComparison.Ordinal);
        Assert.Contains("{{HeaderHtml}}", html, StringComparison.Ordinal);
        Assert.Contains("{{ProfilePhotoHtml}}", html, StringComparison.Ordinal);
        Assert.Contains("{{MainHtml}}", html, StringComparison.Ordinal);
        Assert.Contains("--cv-page-pad-right: var(--cv-page-pad-x);", html, StringComparison.Ordinal);
        Assert.Contains("--cv-running-name: {{RunningNameCss}};", html, StringComparison.Ordinal);
        Assert.Contains("font-family: \"Source Sans 3\", sans-serif;", html, StringComparison.Ordinal);
        Assert.Contains("font-weight: 600 700;", html, StringComparison.Ordinal);
        Assert.Contains(".entry-title { font-size: 10.5pt; font-weight: 600; }", html, StringComparison.Ordinal);
        Assert.Contains("url(\"fonts/SourceSans3-Regular.woff2\")", html, StringComparison.Ordinal);
        Assert.Contains("url(\"fonts/SourceSans3-Bold.woff2\")", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Gill Sans", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Trebuchet MS", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Segoe UI", html, StringComparison.Ordinal);
        Assert.DoesNotContain("{{SidebarHtml}}", html, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(TemplatesDir, "fonts", "SourceSans3-Regular.woff2")));
        Assert.True(File.Exists(Path.Combine(TemplatesDir, "fonts", "SourceSans3-Bold.woff2")));
    }

    [Fact]
    public void FontFaceInliner_embeds_shipped_woff2_files()
    {
        var html = File.ReadAllText(Path.Combine(TemplatesDir, "cv-template.html"));
        var inlined = CvExportFontFaceInliner.Inline(html, TemplatesDir);

        Assert.Equal(2, CountOccurrences(inlined, "data:font/woff2;base64,"));
        Assert.DoesNotContain("fonts/SourceSans3-", inlined, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;

        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    [Fact]
    public void CompactCss_targets_cv_body_shell()
    {
        var css = Normalize(CvExportCompactCssBuilder.Build(new CvPdfRenderOptions(CompactLevel: 2)));

        Assert.Contains(".cv-body {", css, StringComparison.Ordinal);
        Assert.Contains(".cv-body .cv-photo {", css, StringComparison.Ordinal);
    }

    private static string Normalize(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal);
}
