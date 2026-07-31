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
/// Always targets a single page via the compact CSS ramp (no user-facing page option).
/// </summary>
public sealed class CvDocumentExportService(
    ICvStructuredDocumentService structuredDocumentService,
    ICvDocumentService cvDocumentService,
    ICvExportRenderDispatcher exportRenderDispatcher,
    ICvExportHtmlDocumentBuilder htmlDocumentBuilder,
    ICvPdfPageCounter pdfPageCounter,
    ILogger<CvDocumentExportService> logger) : ICvDocumentExportService
{
    /// <summary>Fixed export page target — compact ramp tries to fit this many pages.</summary>
    private const int TargetPageLimit = 1;

    public async Task<CvPdfExportResult> ExportPdfAsync(
        AppUserEntity user,
        CvPdfExportOptions options,
        CancellationToken cancellationToken = default)
    {
        var resolvedTemplateId = CvExportHtmlTemplateCatalog.NormalizeTemplateId(options.TemplateId);
        var renderRequest = await BuildRenderRequestAsync(user, cancellationToken).ConfigureAwait(false);

        var (compactLevel, pageCount, pdfBytes) = await ResolveCompactLevelAsync(
            renderRequest,
            resolvedTemplateId,
            cancellationToken).ConfigureAwait(false);

        var notice = AppendCompactNotices(null, compactLevel, pageCount);

        return new CvPdfExportResult(
            pdfBytes,
            pageCount,
            ExceedsPageTarget(pageCount),
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

        var (compactLevel, pageCount, _) = await ResolveCompactLevelAsync(
            renderRequest,
            resolvedTemplateId,
            cancellationToken).ConfigureAwait(false);
        var notice = AppendCompactNotices(null, compactLevel, pageCount);

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
    /// Ramps CompactLevel until the PDF fits <see cref="TargetPageLimit"/> pages, else best effort.
    /// </summary>
    private async Task<(int CompactLevel, int PageCount, byte[] PdfBytes)> ResolveCompactLevelAsync(
        CvExportRenderRequest renderRequest,
        int templateId,
        CancellationToken cancellationToken)
    {
        byte[]? bestPdfBytes = null;
        var bestPageCount = int.MaxValue;
        var bestCompactLevel = 0;

        for (var compactLevel = 0; compactLevel <= CvPdfRenderOptions.MaxCompactLevel; compactLevel++)
        {
            var renderOptions = ToRenderOptions(compactLevel);
            var pdfBytes = await exportRenderDispatcher
                .RenderAsync(renderRequest, templateId, renderOptions, cancellationToken)
                .ConfigureAwait(false);
            var pageCount = pdfPageCounter.CountPages(pdfBytes);

            if (pageCount < bestPageCount)
            {
                bestPdfBytes = pdfBytes;
                bestPageCount = pageCount;
                bestCompactLevel = compactLevel;
            }

            if (pageCount <= TargetPageLimit)
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

    private static bool ExceedsPageTarget(int pageCount) => pageCount > TargetPageLimit;

    private static string? AppendCompactNotices(string? notice, int compactLevel, int pageCount)
    {
        if (ExceedsPageTarget(pageCount))
        {
            return AppendNotice(
                notice,
                $"This export is {pageCount} pages after compacting; it could not fit on one page.");
        }

        if (compactLevel > 0)
        {
            return AppendNotice(notice, "Layout was compacted to fit on one page.");
        }

        return notice;
    }

    private static string AppendNotice(string? existingNotice, string notice) =>
        string.IsNullOrWhiteSpace(existingNotice)
            ? notice
            : $"{existingNotice} {notice}";
}
