using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace ApplyVault.Api.Services.HtmlExport;

public sealed class HtmlCvPdfExporter(
    IWebHostEnvironment environment,
    PuppeteerBrowserHostedService browserHostedService,
    IOptions<CvHtmlExportOptions> options,
    ILogger<HtmlCvPdfExporter> logger) : ICvHtmlCvPdfExporter
{
    private readonly SemaphoreSlim _exportLock = new(
        Math.Max(1, options.Value.MaxConcurrentExports),
        Math.Max(1, options.Value.MaxConcurrentExports));

    public async Task<byte[]> ExportAsync(
        CvExportRenderRequest request,
        int templateId,
        CvPdfRenderOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var templateFileName = CvExportHtmlTemplateCatalog.GetHtmlTemplateFileName(templateId)
            ?? throw new InvalidOperationException($"HTML template {templateId} is not configured.");

        var templatePath = ResolveTemplatePath(templateFileName);

        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"CV HTML template was not found: {templateFileName}", templatePath);
        }

        var templateHtml = await File.ReadAllTextAsync(templatePath, cancellationToken).ConfigureAwait(false);
        var finalHtml = InjectPrintStyles(
            CvExportHtmlMapper.ApplyTemplate(templateHtml, request, templateId),
            options);

        await _exportLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var browser = await browserHostedService.GetBrowserAsync(cancellationToken).ConfigureAwait(false);
            await using var page = await browser.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(finalHtml).ConfigureAwait(false);

            var pdfBytes = await page.PdfDataAsync(new PdfOptions
            {
                Format = PaperFormat.A4,
                PrintBackground = true,
                PreferCSSPageSize = true,
                MarginOptions = new MarginOptions
                {
                    Top = ResolvePdfMargin(options, normalMillimeters: 10),
                    Bottom = ResolvePdfMargin(options, normalMillimeters: 10),
                    Left = ResolvePdfMargin(options, normalMillimeters: 12),
                    Right = ResolvePdfMargin(options, normalMillimeters: 12)
                }
            }).ConfigureAwait(false);

            return pdfBytes;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "HTML CV PDF export failed for template {TemplateId}.", templateId);
            throw;
        }
        finally
        {
            _exportLock.Release();
        }
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

    private string InjectPrintStyles(string html, CvPdfRenderOptions? options)
    {
        var printCssPath = Path.Combine(ResolveTemplatesDirectory(), "cv-export-print.css");

        if (!File.Exists(printCssPath))
        {
            return html;
        }

        var printCss = File.ReadAllText(printCssPath);
        var compactCss = BuildCompactCss(options);

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

    private static string ResolvePdfMargin(CvPdfRenderOptions? options, int normalMillimeters)
    {
        var compactLevel = Math.Clamp(options?.CompactLevel ?? 0, 0, CvPdfRenderOptions.MaxCompactLevel);
        var scale = compactLevel switch
        {
            1 => 0.9m,
            2 => 0.8m,
            3 => 0.7m,
            4 => 0.6m,
            _ => 1m
        };

        return $"{Math.Max(4, normalMillimeters * scale):0.#}mm";
    }

    private static string BuildCompactCss(CvPdfRenderOptions? options)
    {
        var compactLevel = Math.Clamp(options?.CompactLevel ?? 0, 0, CvPdfRenderOptions.MaxCompactLevel);

        if (compactLevel == 0)
        {
            return string.Empty;
        }

        var (fontScale, lineHeight, sectionMargin, entryMargin, bulletMargin, photoSize) = compactLevel switch
        {
            1 => (0.94m, 1.28m, 12, 9, 3, 82),
            2 => (0.88m, 1.2m, 9, 7, 2, 72),
            3 => (0.82m, 1.12m, 7, 5, 1, 62),
            4 => (0.76m, 1.05m, 5, 3, 0, 52),
            _ => (1m, 1.4m, 16, 12, 4, 96)
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
  height: auto !important;
  max-width: {{photoSize}}px !important;
  max-height: {{photoSize}}px !important;
}
""";
    }

    private static string FormatPercent(decimal value) => $"{value * 100m:0.#}%";
}
