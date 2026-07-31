using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.HtmlExport;

public interface ICvExportHtmlDocumentBuilder
{
    Task<string> BuildAsync(
        CvExportRenderRequest request,
        int templateId,
        CvPdfRenderOptions? options = null,
        CancellationToken cancellationToken = default);
}

public sealed class CvExportHtmlDocumentBuilder(
    IWebHostEnvironment environment,
    IOptions<CvHtmlExportOptions> options) : ICvExportHtmlDocumentBuilder
{
    public async Task<string> BuildAsync(
        CvExportRenderRequest request,
        int templateId,
        CvPdfRenderOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedTemplateId = CvExportHtmlTemplateCatalog.NormalizeTemplateId(templateId);
        var templateFileName = CvExportHtmlTemplateCatalog.GetHtmlTemplateFileName(resolvedTemplateId)
            ?? throw new InvalidOperationException($"HTML template {resolvedTemplateId} is not configured.");

        var templatePath = ResolveTemplatePath(templateFileName);

        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"CV HTML template was not found: {templateFileName}", templatePath);
        }

        var templateHtml = await File.ReadAllTextAsync(templatePath, cancellationToken).ConfigureAwait(false);
        return InjectPrintStyles(
            CvExportHtmlMapper.ApplyTemplate(templateHtml, request, resolvedTemplateId),
            options);
    }

    private string ResolveTemplatePath(string templateFileName) =>
        Path.Combine(ResolveTemplatesDirectory(), templateFileName);

    private string ResolveTemplatesDirectory()
    {
        var subfolder = options.Value.TemplatesSubfolder.Trim().Trim('/');
        var webRoot = environment.WebRootPath;

        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(environment.ContentRootPath, "wwwroot");
        }

        return Path.Combine(webRoot, subfolder);
    }

    private string InjectPrintStyles(string html, CvPdfRenderOptions? renderOptions)
    {
        var printCssPath = Path.Combine(ResolveTemplatesDirectory(), "cv-export-print.css");

        if (!File.Exists(printCssPath))
        {
            return html;
        }

        var printCss = File.ReadAllText(printCssPath);
        var compactCss = BuildCompactCss(renderOptions);

        const string headClose = "</head>";

        if (!html.Contains(headClose, StringComparison.OrdinalIgnoreCase))
        {
            return html;
        }

        return html.Replace(
            headClose,
            $"<style>{printCss}{compactCss}</style>{headClose}",
            StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCompactCss(CvPdfRenderOptions? renderOptions)
    {
        var compactLevel = Math.Clamp(renderOptions?.CompactLevel ?? 0, 0, CvPdfRenderOptions.MaxCompactLevel);

        if (compactLevel == 0)
        {
            return string.Empty;
        }

        var (fontScale, lineHeight, sectionMargin, entryMargin, bulletMargin, photoSize) = compactLevel switch
        {
            1 => (0.94m, 1.28m, 12, 9, 3, 96),
            2 => (0.88m, 1.2m, 9, 7, 2, 88),
            3 => (0.82m, 1.12m, 7, 5, 1, 76),
            4 => (0.76m, 1.05m, 5, 3, 0, 64),
            _ => (1m, 1.4m, 16, 12, 4, 108)
        };

        return $$"""

html body {
  font-size: {{FormatPercent(fontScale)}} !important;
  line-height: {{lineHeight:0.##}} !important;
}

.section {
  margin-bottom: {{sectionMargin}}px !important;
}

.section-title {
  margin-bottom: {{Math.Max(3, sectionMargin / 2)}}px !important;
  padding-bottom: 2px !important;
}

.entry {
  margin-bottom: {{entryMargin}}px !important;
}

.entry-summary,
.entry-bullets {
  margin-top: {{Math.Max(2, entryMargin / 2)}}px !important;
}

.entry-bullets li {
  margin-bottom: {{bulletMargin}}px !important;
}

.entry-tech {
  margin-top: 2px !important;
}

.cv-sidebar,
.cv-main {
  padding: {{Math.Max(8, sectionMargin)}}px {{Math.Max(10, sectionMargin + 2)}}px !important;
}

.cv-photo {
  width: {{photoSize}}px !important;
  height: {{photoSize}}px !important;
  max-width: {{photoSize}}px !important;
  max-height: {{photoSize}}px !important;
}
""";
    }

    private static string FormatPercent(decimal value) => $"{value * 100m:0.#}%";
}
