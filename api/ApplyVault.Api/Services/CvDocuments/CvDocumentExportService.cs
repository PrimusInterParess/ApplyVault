using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services.HtmlExport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public interface ICvDocumentExportService
{
    Task<CvPdfExportResult> ExportPdfAsync(
        AppUserEntity user,
        CvPdfExportOptions options,
        CancellationToken cancellationToken = default);
}

public sealed class CvDocumentExportService(
    ICvStructuredDocumentService structuredDocumentService,
    ICvDocumentService cvDocumentService,
    ICvExportAiClient exportAiClient,
    ICvExportRenderDispatcher exportRenderDispatcher,
    ICvPdfPageCounter pdfPageCounter,
    IOptions<GoogleAiOptions> googleAiOptions,
    ILogger<CvDocumentExportService> logger) : ICvDocumentExportService
{
    public async Task<CvPdfExportResult> ExportPdfAsync(
        AppUserEntity user,
        CvPdfExportOptions options,
        CancellationToken cancellationToken = default)
    {
        var structured = await structuredDocumentService.GetStructuredAsync(user, cancellationToken)
            ?? throw new InvalidOperationException("Upload a CV PDF before exporting structured content.");

        if (structured.Sections.Count == 0)
        {
            throw new InvalidOperationException("No structured CV sections are available to export.");
        }

        byte[]? profilePhotoBytes = null;
        string? profilePhotoContentType = null;

        var profilePhoto = await cvDocumentService.OpenProfilePhotoAsync(user, cancellationToken);

        if (profilePhoto is not null)
        {
            await using (profilePhoto.Content)
            {
                using var memoryStream = new MemoryStream();
                await profilePhoto.Content.CopyToAsync(memoryStream, cancellationToken);
                profilePhotoBytes = memoryStream.ToArray();
                profilePhotoContentType = profilePhoto.ContentType;
            }
        }

        var renderRequest = CvExportMapping.FromStructuredDocument(structured, profilePhotoBytes, profilePhotoContentType);
        var usedAi = false;
        string? notice = null;

        if (googleAiOptions.Value.Enabled)
        {
            try
            {
                var aiInput = CvExportPolishPayloadBuilder.FromDocument(structured);
                var polished = await exportAiClient.PolishAsync(aiInput, cancellationToken);

                if (polished.Sections.Count > 0)
                {
                    renderRequest = CvExportMapping.FromImportResult(
                        polished,
                        structured,
                        profilePhotoBytes,
                        profilePhotoContentType);
                    usedAi = true;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "CV export AI polish failed; falling back to saved structured content.");
                notice = "AI polish was unavailable; exported using saved content.";
            }
        }

        var (pdfBytes, pageCount, compactLevel) = await RenderWithinPageLimitAsync(
            renderRequest,
            options,
            cancellationToken);
        var exceedsMaxPages = options.MaxPages is int maxPages && pageCount > maxPages;

        if (exceedsMaxPages)
        {
            notice = AppendNotice(
                notice,
                $"This export is {pageCount} pages after compacting; your limit is {options.MaxPages}.");
        }
        else if (compactLevel > 0)
        {
            notice = AppendNotice(
                notice,
                $"Layout was compacted to fit the selected {options.MaxPages}-page limit.");
        }

        return new CvPdfExportResult(
            pdfBytes,
            pageCount,
            options.MaxPages,
            exceedsMaxPages,
            usedAi,
            notice);
    }

    private async Task<(byte[] PdfBytes, int PageCount, int CompactLevel)> RenderWithinPageLimitAsync(
        CvExportRenderRequest renderRequest,
        CvPdfExportOptions options,
        CancellationToken cancellationToken)
    {
        byte[]? bestPdfBytes = null;
        var bestPageCount = int.MaxValue;
        var bestCompactLevel = 0;

        var maxCompactLevel = options.MaxPages.HasValue ? CvPdfRenderOptions.MaxCompactLevel : 0;

        for (var compactLevel = 0; compactLevel <= maxCompactLevel; compactLevel++)
        {
            var renderOptions = compactLevel == 0
                ? CvPdfRenderOptions.Normal
                : new CvPdfRenderOptions(compactLevel);
            var pdfBytes = await exportRenderDispatcher
                .RenderAsync(renderRequest, options.TemplateId, renderOptions, cancellationToken)
                .ConfigureAwait(false);
            var pageCount = pdfPageCounter.CountPages(pdfBytes);

            if (pageCount < bestPageCount)
            {
                bestPdfBytes = pdfBytes;
                bestPageCount = pageCount;
                bestCompactLevel = compactLevel;
            }

            if (!options.MaxPages.HasValue || pageCount <= options.MaxPages.Value)
            {
                return (pdfBytes, pageCount, compactLevel);
            }
        }

        return (bestPdfBytes!, bestPageCount, bestCompactLevel);
    }

    private static string AppendNotice(string? existingNotice, string notice) =>
        string.IsNullOrWhiteSpace(existingNotice)
            ? notice
            : $"{existingNotice} {notice}";
}
