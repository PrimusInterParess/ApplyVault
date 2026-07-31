using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using ApplyVault.Api.Services.CvSectionCatalog;
using ApplyVault.Api.Services.HtmlExport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApplyVault.Api.Controllers;

[ApiController]
[Route("api/cv-documents")]
[Authorize]
public sealed class CvDocumentsController(
    IAppUserService appUserService,
    ICvDocumentService cvDocumentService,
    ICvStructuredDocumentService cvStructuredDocumentService,
    ICvStructuredImportService cvStructuredImportService,
    ICvStructuredUpdateService cvStructuredUpdateService,
    ICvStructuredSuggestionsService cvStructuredSuggestionsService,
    ICvDocumentExportService cvDocumentExportService,
    ICvSectionCatalog sectionCatalog) : ControllerBase
{
    private const int MaxExportPageLimit = 5;

    [HttpGet("current")]
    public async Task<ActionResult<CvDocumentDto>> GetCurrent(CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var document = await cvDocumentService.GetCurrentAsync(user, cancellationToken);

        return document is null ? NotFound() : Ok(document);
    }

    [HttpPost("current")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<CvDocumentUploadResultDto>> UploadCurrent(
        IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length <= 0)
        {
            return BadRequest("Upload a PDF file before saving.");
        }

        var user = await appUserService.GetRequiredUserAsync(cancellationToken);

        try
        {
            return Ok(await cvDocumentService.UploadAsync(user, file, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpGet("current/content")]
    public Task<IActionResult> GetCurrentContent(CancellationToken cancellationToken = default) =>
        GetOriginalContent(cancellationToken);

    [HttpGet("current/content/original")]
    public async Task<IActionResult> GetOriginalContent(CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var content = await cvDocumentService.OpenOriginalContentAsync(user, cancellationToken);

        if (content is null)
        {
            return NotFound();
        }

        return new FileStreamResult(content.Content, content.ContentType)
        {
            EnableRangeProcessing = true
        };
    }

    [HttpGet("current/content/original/download")]
    public async Task<IActionResult> DownloadOriginalContent(CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var content = await cvDocumentService.OpenOriginalContentAsync(user, cancellationToken);

        if (content is null)
        {
            return NotFound();
        }

        return new FileStreamResult(content.Content, content.ContentType)
        {
            EnableRangeProcessing = true,
            FileDownloadName = content.FileName
        };
    }

    [HttpGet("current/profile-photo")]
    public async Task<IActionResult> GetProfilePhoto(CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var content = await cvDocumentService.OpenProfilePhotoAsync(user, cancellationToken);

        if (content is null)
        {
            return NotFound();
        }

        return new FileStreamResult(content.Content, content.ContentType)
        {
            EnableRangeProcessing = true
        };
    }

    [HttpPut("current/profile-photo")]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<ActionResult<CvDocumentDto>> UploadProfilePhoto(
        IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length <= 0)
        {
            return BadRequest("Upload an image file before saving.");
        }

        var user = await appUserService.GetRequiredUserAsync(cancellationToken);

        try
        {
            return Ok(await cvDocumentService.UploadProfilePhotoAsync(user, file, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpDelete("current/profile-photo")]
    public async Task<ActionResult<CvDocumentDto>> DeleteProfilePhoto(CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var document = await cvDocumentService.DeleteProfilePhotoAsync(user, cancellationToken);

        return document is null ? NotFound() : Ok(document);
    }

    [HttpDelete("current")]
    public async Task<IActionResult> DeleteCurrent(CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var deleted = await cvDocumentService.DeleteAsync(user, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("current/start-blank")]
    public async Task<ActionResult<CvDocumentDto>> StartBlank(CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        return Ok(await cvDocumentService.StartBlankAsync(user, cancellationToken));
    }

    [HttpPut("current/export-preferences")]
    public async Task<ActionResult<CvDocumentDto>> UpdateExportPreferences(
        [FromBody] CvExportPreferencesDto? preferences,
        CancellationToken cancellationToken = default)
    {
        if (preferences is null)
        {
            return BadRequest("Export preferences are required.");
        }

        if (preferences.MaxPages is < 1 or > MaxExportPageLimit)
        {
            return BadRequest($"maxPages must be between 1 and {MaxExportPageLimit}.");
        }

        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var document = await cvDocumentService.UpdateExportPreferencesAsync(
            user,
            new CvExportPreferencesDto(
                CvExportHtmlTemplateCatalog.NormalizeTemplateId(preferences.TemplateId),
                preferences.MaxPages),
            cancellationToken);

        return document is null ? NotFound() : Ok(document);
    }

    [HttpGet("section-catalog")]
    [AllowAnonymous]
    public ActionResult<CvSectionCatalogDto> GetSectionCatalog() => Ok(sectionCatalog.ToApiDto());

    [HttpGet("current/structured")]
    public async Task<ActionResult<CvStructuredDocumentDto>> GetStructured(CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var structured = await cvStructuredDocumentService.GetStructuredAsync(user, cancellationToken);
        return structured is null ? NotFound() : Ok(structured);
    }

    [HttpGet("current/export/download")]
    public async Task<IActionResult> DownloadFormattedExport(
        [FromQuery] int? templateId = null,
        [FromQuery] int? maxPages = null,
        CancellationToken cancellationToken = default)
    {
        if (maxPages is < 1 or > MaxExportPageLimit)
        {
            return BadRequest($"maxPages must be between 1 and {MaxExportPageLimit}.");
        }

        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var resolved = await ResolveExportOptionsAsync(user, templateId, maxPages, cancellationToken);

        try
        {
            var exportResult = await cvDocumentExportService.ExportPdfAsync(
                user,
                new CvPdfExportOptions(resolved.TemplateId, resolved.MaxPages),
                cancellationToken);
            var structured = await cvStructuredDocumentService.GetStructuredAsync(user, cancellationToken);
            var fileName = CvExportDownloadFileName.BuildForExport(structured, resolved.TemplateId);

            AppendExportMetadataHeaders(exportResult);
            Response.Headers["X-Cv-Export-Template-Id"] = resolved.TemplateId.ToString();

            return File(exportResult.PdfBytes, "application/pdf", fileName);
        }
        catch (InvalidOperationException exception)
        {
            var message = exception.Message;

            if (message.Contains("before exporting", StringComparison.OrdinalIgnoreCase)
                || message.Contains("No structured", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound();
            }

            return BadRequest(message);
        }
    }

    [HttpGet("current/export/preview")]
    public async Task<IActionResult> PreviewFormattedExport(
        [FromQuery] int? templateId = null,
        [FromQuery] int? maxPages = null,
        CancellationToken cancellationToken = default)
    {
        if (maxPages is < 1 or > MaxExportPageLimit)
        {
            return BadRequest($"maxPages must be between 1 and {MaxExportPageLimit}.");
        }

        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var resolved = await ResolveExportOptionsAsync(user, templateId, maxPages, cancellationToken);

        try
        {
            var exportResult = await cvDocumentExportService.ExportHtmlAsync(
                user,
                new CvPdfExportOptions(resolved.TemplateId, resolved.MaxPages),
                cancellationToken);

            Response.Headers["X-Cv-Export-Template-Id"] = exportResult.ResolvedTemplateId.ToString();
            Response.Headers["X-Cv-Export-Compact-Level"] = exportResult.CompactLevel.ToString();

            if (!string.IsNullOrWhiteSpace(exportResult.Notice))
            {
                Response.Headers["X-Cv-Export-Notice"] = Uri.EscapeDataString(exportResult.Notice);
            }

            return Content(exportResult.Html, "text/html; charset=utf-8");
        }
        catch (InvalidOperationException exception)
        {
            var message = exception.Message;

            if (message.Contains("before exporting", StringComparison.OrdinalIgnoreCase)
                || message.Contains("No structured", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound();
            }

            return BadRequest(message);
        }
    }

    private async Task<(int TemplateId, int? MaxPages)> ResolveExportOptionsAsync(
        AppUserEntity user,
        int? templateId,
        int? maxPages,
        CancellationToken cancellationToken)
    {
        var document = templateId is null || maxPages is null
            ? await cvDocumentService.GetCurrentAsync(user, cancellationToken)
            : null;

        var resolvedTemplateId = CvExportHtmlTemplateCatalog.NormalizeTemplateId(
            templateId ?? document?.TemplateId ?? CvExportHtmlTemplateCatalog.DefaultTemplateId);
        var resolvedMaxPages = maxPages ?? document?.MaxPages;

        return (resolvedTemplateId, resolvedMaxPages);
    }

    private void AppendExportMetadataHeaders(CvPdfExportResult exportResult)
    {
        Response.Headers["X-Cv-Export-Page-Count"] = exportResult.PageCount.ToString();
        Response.Headers["X-Cv-Export-Max-Pages"] = exportResult.MaxPages?.ToString() ?? string.Empty;
        Response.Headers["X-Cv-Export-Exceeds-Limit"] = exportResult.ExceedsMaxPages ? "true" : "false";

        if (!string.IsNullOrWhiteSpace(exportResult.Notice))
        {
            Response.Headers["X-Cv-Export-Notice"] = Uri.EscapeDataString(exportResult.Notice);
        }
    }

    [HttpPost("current/structured/reimport")]
    public async Task<ActionResult<CvStructuredReimportResultDto>> ReimportStructured(
        CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);

        try
        {
            var result = await cvStructuredImportService.ReimportAndPersistAsync(user, cancellationToken);

            if (!result.Import.Succeeded)
            {
                return BadRequest(result.Import.Notice ?? "Structured re-import failed.");
            }

            return Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPut("current/structured")]
    public async Task<ActionResult<CvStructuredDocumentDto>> SaveStructured(
        [FromBody] SaveCvStructuredDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);

        try
        {
            return Ok(await cvStructuredDocumentService.SaveStructuredAsync(
                user,
                request,
                markImported: false,
                cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("current/structured/ai-update")]
    public async Task<ActionResult<CvStructuredDocumentDto>> UpdateStructuredWithAi(
        [FromBody] UpdateCvStructuredWithAiRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);

        try
        {
            return Ok(await cvStructuredUpdateService.UpdateWithAiAsync(user, request, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("current/structured/ai-suggestions")]
    public async Task<ActionResult<CvImprovementSuggestionsDto>> GenerateStructuredSuggestionsWithAi(
        [FromBody] GenerateCvImprovementSuggestionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);

        try
        {
            return Ok(await cvStructuredSuggestionsService.GenerateAsync(user, request, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}
