using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace ApplyVault.Api.Services.HtmlExport;

public sealed class HtmlCvPdfExporter(
    ICvExportHtmlDocumentBuilder htmlDocumentBuilder,
    PuppeteerBrowserHostedService browserHostedService,
    ILogger<HtmlCvPdfExporter> logger) : ICvHtmlCvPdfExporter
{
    public async Task<byte[]> ExportAsync(
        CvExportRenderRequest request,
        int templateId,
        CvPdfRenderOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedTemplateId = CvExportHtmlTemplateCatalog.NormalizeTemplateId(templateId);
        var finalHtml = await htmlDocumentBuilder
            .BuildAsync(request, resolvedTemplateId, options, cancellationToken)
            .ConfigureAwait(false);

        using var exportSlot = await browserHostedService.AcquireExportSlotAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await ExportWithBrowserRecoveryAsync(finalHtml, resolvedTemplateId, options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "HTML CV PDF export failed for template {TemplateId}.", resolvedTemplateId);
            throw;
        }
    }

    private async Task<byte[]> ExportWithBrowserRecoveryAsync(
        string finalHtml,
        int templateId,
        CvPdfRenderOptions? options,
        CancellationToken cancellationToken)
    {
        try
        {
            return await RenderPdfAsync(finalHtml, options, cancellationToken).ConfigureAwait(false);
        }
        catch (TargetClosedException exception)
        {
            logger.LogWarning(
                exception,
                "Chromium target closed during HTML CV PDF export for template {TemplateId}; relaunching browser and retrying once.",
                templateId);

            await browserHostedService.ResetBrowserAsync(cancellationToken).ConfigureAwait(false);
            return await RenderPdfAsync(finalHtml, options, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<byte[]> RenderPdfAsync(
        string finalHtml,
        CvPdfRenderOptions? options,
        CancellationToken cancellationToken)
    {
        var browser = await browserHostedService.GetBrowserAsync(cancellationToken).ConfigureAwait(false);
        await using var page = await browser.NewPageAsync().ConfigureAwait(false);

        await page.SetContentAsync(finalHtml).ConfigureAwait(false);

        // Document templates own page inset via CSS padding; @page margin is 0.
        // Keep Puppeteer margins at 0 so PDF does not double-inset vs preview.
        // CompactLevel spacing is applied in HTML CSS (CvExportCompactCssBuilder), not browser margins.
        _ = options;

        return await page.PdfDataAsync(new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            PreferCSSPageSize = true,
            MarginOptions = new MarginOptions
            {
                Top = "0",
                Bottom = "0",
                Left = "0",
                Right = "0"
            }
        }).ConfigureAwait(false);
    }
}
