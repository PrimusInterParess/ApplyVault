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
    ICvStructuredDocumentService cvStructuredDocumentService,
    ICvStructuredImportService cvStructuredImportService,
    ICvDocumentExportService cvDocumentExportService) : ControllerBase
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

    [HttpGet("current/export/download")]
    public async Task<IActionResult> DownloadFormattedExport(CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);

        try
        {
            var exportResult = await cvDocumentExportService.ExportPdfAsync(user, cancellationToken);
            var document = await cvDocumentService.GetCurrentAsync(user, cancellationToken);
            var fileName = document is null
                ? "cv-export.pdf"
                : $"{Path.GetFileNameWithoutExtension(document.OriginalFileName)}-export.pdf";

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
}
