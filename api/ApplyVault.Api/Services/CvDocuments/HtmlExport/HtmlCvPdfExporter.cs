using System.Text;
using System.Text.RegularExpressions;
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
        CvPdfRenderOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var finalHtml = await htmlDocumentBuilder
            .BuildAsync(request, options, cancellationToken)
            .ConfigureAwait(false);

        using var exportSlot = await browserHostedService.AcquireExportSlotAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return await ExportWithBrowserRecoveryAsync(finalHtml, options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "HTML CV PDF export failed.");
            throw;
        }
    }

    private async Task<byte[]> ExportWithBrowserRecoveryAsync(
        string finalHtml,
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
                "Chromium target closed during HTML CV PDF export; relaunching browser and retrying once.");

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

        await page.SetContentAsync(
            finalHtml,
            new NavigationOptions
            {
                WaitUntil = [WaitUntilNavigation.Load]
            }).ConfigureAwait(false);
        await page.EvaluateFunctionAsync("() => document.fonts.ready").ConfigureAwait(false);

        // The template owns page inset via CSS padding (.cv-body);
        // @page + Puppeteer margins stay 0 so PDF matches preview.
        // Print CSS clones that padding onto each page fragment (multi-page overflow).
        // CompactLevel spacing is applied in HTML CSS (CvExportCompactCssBuilder), not browser margins.
        _ = options;

        var pdf = await page.PdfDataAsync(new PdfOptions
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

        return CvPdfLinkTargets.OpenInNewWindow(pdf);
    }
}

/// <summary>
/// Chromium's print-to-PDF keeps link actions in the same tab. Mark each URI action to open a new window.
/// </summary>
internal static partial class CvPdfLinkTargets
{
    public static byte[] OpenInNewWindow(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var updated = UriActionPattern().Replace(text, "/S /URI /NewWindow true");

        return updated == text ? pdf : Encoding.Latin1.GetBytes(updated);
    }

    [GeneratedRegex(@"/S\s*/URI(?!\s*/NewWindow)", RegexOptions.CultureInvariant)]
    private static partial Regex UriActionPattern();
}
