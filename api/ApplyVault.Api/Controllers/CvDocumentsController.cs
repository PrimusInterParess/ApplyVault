using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApplyVault.Api.Controllers;

[ApiController]
[Route("api/cv-documents")]
[Authorize]
public sealed class CvDocumentsController(
    IAppUserService appUserService,
    ICvDocumentService cvDocumentService,
    ICvPdfProjectsMergeService cvPdfProjectsMergeService,
    ICvPdfSectionDetectionService cvPdfSectionDetectionService,
    ICvStructuredDocumentService cvStructuredDocumentService,
    ICvStructuredImportService cvStructuredImportService,
    ICvStructuredExportService cvStructuredExportService) : ControllerBase
{
    [HttpGet("current")]
    public async Task<ActionResult<CvDocumentDto>> GetCurrent(CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var document = await cvDocumentService.GetCurrentAsync(user, cancellationToken);

        return document is null ? NotFound() : Ok(document);
    }

    [HttpPost("current")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<CvDocumentDto>> UploadCurrent(
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
    public async Task<IActionResult> GetCurrentContent(CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var content = await cvDocumentService.OpenContentAsync(user, cancellationToken);

        if (content is null)
        {
            return NotFound();
        }

        return new FileStreamResult(content.Content, content.ContentType)
        {
            EnableRangeProcessing = true
        };
    }

    [HttpGet("current/sections")]
    public async Task<ActionResult<IReadOnlyList<CvPdfSectionDto>>> GetCurrentSections(
        CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);

        try
        {
            return Ok(await cvPdfSectionDetectionService.DetectCurrentDocumentSectionsAsync(user, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("current/merge-projects")]
    public async Task<ActionResult<CvDocumentDto>> MergeProjects(CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);

        try
        {
            return Ok(await cvPdfProjectsMergeService.MergeProjectsAsync(user, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpDelete("current")]
    public async Task<IActionResult> DeleteCurrent(CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var deleted = await cvDocumentService.DeleteAsync(user, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("current/structured")]
    public async Task<ActionResult<CvStructuredDocumentDto>> GetStructured(CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var structured = await cvStructuredDocumentService.GetStructuredAsync(user, cancellationToken);
        return structured is null ? NotFound() : Ok(structured);
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

    [HttpPost("current/import")]
    public async Task<ActionResult<CvStructuredImportPreviewDto>> PreviewImport(
        CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);

        try
        {
            return Ok(await cvStructuredImportService.PreviewImportAsync(user, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("current/import/confirm")]
    public async Task<ActionResult<CvStructuredDocumentDto>> ConfirmImport(
        [FromBody] SaveCvStructuredDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);

        try
        {
            return Ok(await cvStructuredImportService.ConfirmImportAsync(user, request, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("current/sections/{sectionId:guid}/entries/from-summary")]
    public async Task<ActionResult<CvStructuredEntryDto>> InsertFromSummary(
        Guid sectionId,
        [FromBody] InsertCvEntryFromSummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);

        try
        {
            return Ok(await cvStructuredDocumentService.InsertEntryFromSummaryAsync(
                user,
                sectionId,
                request,
                cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("current/export")]
    public async Task<ActionResult<CvDocumentDto>> ExportStructured(CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);

        try
        {
            return Ok(await cvStructuredExportService.ExportAsync(user, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}
