using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services.HtmlExport;
using Microsoft.Extensions.Logging;

namespace ApplyVault.Api.Services;

public sealed record CvHtmlExportResult(
    string Html,
    int ResolvedTemplateId,
    int CompactLevel = 0,
    string? Notice = null);

public interface ICvDocumentExportService
{
    Task<CvPdfExportResult> ExportPdfAsync(
        AppUserEntity user,
        CvPdfExportOptions options,
        CancellationToken cancellationToken = default);

    Task<CvHtmlExportResult> ExportHtmlAsync(
        AppUserEntity user,
        CvPdfExportOptions options,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Exports the saved structured CV as HTML/PDF. No AI rewrite — FE canvas and BE export
/// share the same structured payload (fill-the-gaps / layout only).
/// </summary>
public sealed class CvDocumentExportService(
    ICvStructuredDocumentService structuredDocumentService,
    ICvDocumentService cvDocumentService,
    ICvExportRenderDispatcher exportRenderDispatcher,
    ICvExportHtmlDocumentBuilder htmlDocumentBuilder,
    ICvPdfPageCounter pdfPageCounter,
    ILogger<CvDocumentExportService> logger) : ICvDocumentExportService
{
    public async Task<CvPdfExportResult> ExportPdfAsync(
        AppUserEntity user,
        CvPdfExportOptions options,
        CancellationToken cancellationToken = default)
    {
        var resolvedTemplateId = CvExportHtmlTemplateCatalog.NormalizeTemplateId(options.TemplateId);
        var renderRequest = await BuildRenderRequestAsync(user, cancellationToken).ConfigureAwait(false);

        var normalizedOptions = options with { TemplateId = resolvedTemplateId };
        var (compactLevel, pageCount, pdfBytes) = await ResolveCompactLevelAsync(
            renderRequest,
            normalizedOptions,
            cancellationToken).ConfigureAwait(false);

        var notice = AppendCompactNotices(null, compactLevel, pageCount, normalizedOptions.MaxPages);

        return new CvPdfExportResult(
            pdfBytes,
            pageCount,
            normalizedOptions.MaxPages,
            ExceedsMaxPages(pageCount, normalizedOptions.MaxPages),
            UsedAi: false,
            notice);
    }

    public async Task<CvHtmlExportResult> ExportHtmlAsync(
        AppUserEntity user,
        CvPdfExportOptions options,
        CancellationToken cancellationToken = default)
    {
        var resolvedTemplateId = CvExportHtmlTemplateCatalog.NormalizeTemplateId(options.TemplateId);
        var renderRequest = await BuildRenderRequestAsync(user, cancellationToken).ConfigureAwait(false);

        var normalizedOptions = options with { TemplateId = resolvedTemplateId };

        // Unlimited maxPages stays Normal (compact 0) without a Puppeteer search — same as PDF.
        // When maxPages is set, resolve via the shared PDF page-count ramp.
        int compactLevel;
        string? notice = null;
        if (!normalizedOptions.MaxPages.HasValue)
        {
            compactLevel = 0;
        }
        else
        {
            var (resolvedLevel, pageCount, _) = await ResolveCompactLevelAsync(
                renderRequest,
                normalizedOptions,
                cancellationToken).ConfigureAwait(false);
            compactLevel = resolvedLevel;
            notice = AppendCompactNotices(notice, compactLevel, pageCount, normalizedOptions.MaxPages);
        }

        var html = await htmlDocumentBuilder
            .BuildAsync(renderRequest, resolvedTemplateId, ToRenderOptions(compactLevel), cancellationToken)
            .ConfigureAwait(false);

        return new CvHtmlExportResult(html, resolvedTemplateId, compactLevel, notice);
    }

    private async Task<CvExportRenderRequest> BuildRenderRequestAsync(
        AppUserEntity user,
        CancellationToken cancellationToken)
    {
        var structured = await structuredDocumentService.GetStructuredAsync(user, cancellationToken)
            ?? throw new InvalidOperationException("Create or upload a CV before exporting structured content.");

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

        logger.LogDebug(
            "Exporting structured CV for user {UserId} with {SectionCount} sections (no AI polish).",
            user.Id,
            structured.Sections.Count);

        return CvExportMapping.FromStructuredDocument(structured, profilePhotoBytes, profilePhotoContentType);
    }

    /// <summary>
    /// Shared compact search used by PDF export and HTML preview.
    /// When <see cref="CvPdfExportOptions.MaxPages"/> is null, returns CompactLevel 0 (Normal)
    /// after a single render — same semantics as the former PDF-only ramp.
    /// </summary>
    private async Task<(int CompactLevel, int PageCount, byte[] PdfBytes)> ResolveCompactLevelAsync(
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
            var renderOptions = ToRenderOptions(compactLevel);
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
                return (compactLevel, pageCount, pdfBytes);
            }
        }

        return (bestCompactLevel, bestPageCount, bestPdfBytes!);
    }

    private static CvPdfRenderOptions ToRenderOptions(int compactLevel) =>
        compactLevel == 0
            ? CvPdfRenderOptions.Normal
            : new CvPdfRenderOptions(compactLevel);

    private static bool ExceedsMaxPages(int pageCount, int? maxPages) =>
        maxPages is int limit && pageCount > limit;

    private static string? AppendCompactNotices(
        string? notice,
        int compactLevel,
        int pageCount,
        int? maxPages)
    {
        if (ExceedsMaxPages(pageCount, maxPages))
        {
            return AppendNotice(
                notice,
                $"This export is {pageCount} pages after compacting; your limit is {maxPages}.");
        }

        if (compactLevel > 0)
        {
            return AppendNotice(
                notice,
                $"Layout was compacted to fit the selected {maxPages}-page limit.");
        }

        return notice;
    }

    private static string AppendNotice(string? existingNotice, string notice) =>
        string.IsNullOrWhiteSpace(existingNotice)
            ? notice
            : $"{existingNotice} {notice}";
}
