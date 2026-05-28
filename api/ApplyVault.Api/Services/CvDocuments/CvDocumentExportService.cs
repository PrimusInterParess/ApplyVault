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
        int templateId = 1,
        CancellationToken cancellationToken = default);
}

public sealed class CvDocumentExportService(
    ICvStructuredDocumentService structuredDocumentService,
    ICvDocumentService cvDocumentService,
    ICvExportAiClient exportAiClient,
    ICvExportRenderDispatcher exportRenderDispatcher,
    IOptions<GoogleAiOptions> googleAiOptions,
    ILogger<CvDocumentExportService> logger) : ICvDocumentExportService
{
    public async Task<CvPdfExportResult> ExportPdfAsync(
        AppUserEntity user,
        int templateId = 1,
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

        var pdfBytes = await exportRenderDispatcher.RenderAsync(renderRequest, templateId, cancellationToken);

        return new CvPdfExportResult(pdfBytes, usedAi, notice);
    }
}
