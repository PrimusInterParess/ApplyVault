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

        return await page.PdfDataAsync(new PdfOptions
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
}
