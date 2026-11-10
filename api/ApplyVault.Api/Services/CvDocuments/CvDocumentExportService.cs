using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services.HtmlExport;
using Microsoft.Extensions.Logging;

namespace ApplyVault.Api.Services;

public sealed record CvHtmlExportResult(
    string Html,
    int CompactLevel = 0,
    string? Notice = null);

public interface ICvDocumentExportService
{
    Task<CvPdfExportResult> ExportPdfAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default);

    Task<CvHtmlExportResult> ExportHtmlAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Exports the saved structured CV as HTML/PDF using the single CV template. No AI rewrite —
/// FE canvas and BE export share the same structured payload (fill-the-gaps / layout only).
/// Page policy: one page recommended, two pages maximum. The compact CSS ramp first tries to
/// reach one page; failing that it takes one step tighter than the loosest level that still
/// fits two pages; failing that it returns the fewest-pages render and flags
/// <see cref="CvPdfExportResult.ExceedsMaxPages"/>.
/// </summary>
public sealed class CvDocumentExportService(
    ICvStructuredDocumentService structuredDocumentService,
    ICvDocumentService cvDocumentService,
    ICvHtmlCvPdfExporter htmlCvPdfExporter,
    ICvExportHtmlDocumentBuilder htmlDocumentBuilder,
    ICvPdfPageCounter pdfPageCounter,
    ILogger<CvDocumentExportService> logger) : ICvDocumentExportService
{
    /// <summary>Recommended page count — the compact ramp stops as soon as this is reached.</summary>
    private const int RecommendedPageCount = 1;

    /// <summary>Hard maximum — exports above this are flagged as exceeding the limit.</summary>
    private const int MaxPageCount = 2;

    public async Task<CvPdfExportResult> ExportPdfAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default)
    {
        var renderRequest = await BuildRenderRequestAsync(user, cancellationToken).ConfigureAwait(false);

        var (compactLevel, pageCount, pdfBytes) = await ResolveCompactLevelAsync(
            renderRequest,
            cancellationToken).ConfigureAwait(false);

        return new CvPdfExportResult(
            pdfBytes,
            pageCount,
            pageCount > MaxPageCount,
            UsedAi: false,
            BuildNotice(compactLevel, pageCount));
    }

    public async Task<CvHtmlExportResult> ExportHtmlAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default)
    {
        var renderRequest = await BuildRenderRequestAsync(user, cancellationToken).ConfigureAwait(false);

        var (compactLevel, pageCount, _) = await ResolveCompactLevelAsync(
            renderRequest,
            cancellationToken).ConfigureAwait(false);

        var html = await htmlDocumentBuilder
            .BuildAsync(renderRequest, ToRenderOptions(compactLevel), cancellationToken)
            .ConfigureAwait(false);

        return new CvHtmlExportResult(html, compactLevel, BuildNotice(compactLevel, pageCount));
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
    /// Returns the first level that fits <see cref="RecommendedPageCount"/>.
    /// Otherwise returns one step tighter than the loosest level that fits <see cref="MaxPageCount"/>,
    /// or that loosest level when the next step exceeds two pages or is already the last level.
    /// When nothing fits two pages, returns the level with the fewest pages.
    /// </summary>
    private async Task<(int CompactLevel, int PageCount, byte[] PdfBytes)> ResolveCompactLevelAsync(
        CvExportRenderRequest renderRequest,
        CancellationToken cancellationToken)
    {
        (int CompactLevel, int PageCount, byte[] PdfBytes)? loosestTwoPage = null;
        (int CompactLevel, int PageCount, byte[] PdfBytes)? fewestPages = null;

        for (var compactLevel = 0; compactLevel <= CvPdfRenderOptions.MaxCompactLevel; compactLevel++)
        {
            var renderOptions = ToRenderOptions(compactLevel);
            var pdfBytes = await htmlCvPdfExporter
                .ExportAsync(renderRequest, renderOptions, cancellationToken)
                .ConfigureAwait(false);
            var pageCount = pdfPageCounter.CountPages(pdfBytes);
            var candidate = (compactLevel, pageCount, pdfBytes);

            if (pageCount <= RecommendedPageCount)
            {
                return candidate;
            }

            if (fewestPages is null || pageCount < fewestPages.Value.PageCount)
            {
                fewestPages = candidate;
            }

            if (loosestTwoPage is null)
            {
                if (pageCount <= MaxPageCount)
                {
                    loosestTwoPage = candidate;
                }

                continue;
            }

            if (compactLevel == loosestTwoPage.Value.CompactLevel + 1)
            {
                return pageCount <= MaxPageCount
                    ? candidate
                    : loosestTwoPage.Value;
            }
        }

        return loosestTwoPage ?? fewestPages!.Value;
    }

    private static CvPdfRenderOptions ToRenderOptions(int compactLevel) =>
        compactLevel == 0
            ? CvPdfRenderOptions.Normal
            : new CvPdfRenderOptions(compactLevel);

    private static string? BuildNotice(int compactLevel, int pageCount)
    {
        if (pageCount > MaxPageCount)
        {
            return $"This export is {pageCount} pages after compacting; the maximum is two pages.";
        }

        if (pageCount == MaxPageCount)
        {
            return "Your CV is 2 pages; one page is recommended — consider trimming content.";
        }

        if (compactLevel > 0)
        {
            return "Layout was compacted to fit on one page.";
        }

        return null;
    }
}
