using ApplyVault.Api.Services;
using ApplyVault.Api.Services.HtmlExport;

namespace ApplyVault.Api.Tests;

/// <summary>
/// Guards multi-page PDF inset: shell padding must clone onto page 2+ fragments.
/// Default export target remains one page; this only covers overflow formatting.
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
            ".cv-body,\n.cv-page-pad {\n  -webkit-box-decoration-break: clone;\n  box-decoration-break: clone;\n}",
            css,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ModernTemplate_puts_padding_on_cloneable_inner_shell()
    {
        var html = Normalize(File.ReadAllText(Path.Combine(TemplatesDir, "template-modern.html")));

        Assert.Contains("class=\"cv-page-pad\"", html, StringComparison.Ordinal);
        Assert.Contains(".cv-sidebar > .cv-page-pad {", html, StringComparison.Ordinal);
        Assert.Contains(".cv-main > .cv-page-pad {", html, StringComparison.Ordinal);
        Assert.Contains(".cv-sidebar {\n      width: 34%;\n      background: var(--cv-sidebar-bg);\n      color: var(--cv-sidebar-ink);\n      padding: 0;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void CompactCss_targets_modern_inner_page_pad()
    {
        var css = Normalize(CvExportCompactCssBuilder.Build(new CvPdfRenderOptions(CompactLevel: 2)));

        Assert.Contains(".cv-layout .cv-sidebar > .cv-page-pad {", css, StringComparison.Ordinal);
        Assert.Contains(".cv-layout .cv-main > .cv-page-pad {", css, StringComparison.Ordinal);
    }

    private static string Normalize(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal);
}
