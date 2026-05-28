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
        var finalHtml = InjectPrintStyles(CvExportHtmlMapper.ApplyTemplate(templateHtml, request, templateId));

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
                    Top = "10mm",
                    Bottom = "10mm",
                    Left = "12mm",
                    Right = "12mm"
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

    private string InjectPrintStyles(string html)
    {
        var printCssPath = Path.Combine(ResolveTemplatesDirectory(), "cv-export-print.css");

        if (!File.Exists(printCssPath))
        {
            return html;
        }

        var printCss = File.ReadAllText(printCssPath);

        const string headClose = "</head>";

        if (!html.Contains(headClose, StringComparison.OrdinalIgnoreCase))
        {
            return html;
        }

        return html.Replace(
            headClose,
            $"<style>{printCss}</style>{headClose}",
            StringComparison.OrdinalIgnoreCase);
    }
}
