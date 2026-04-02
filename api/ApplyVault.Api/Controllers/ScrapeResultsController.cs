using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApplyVault.Api.Controllers;

[ApiController]
[Route("api/scrape-results")]
public sealed class ScrapeResultsController(
    IScrapeResultStore store,
    IScrapeResultSaveService saveService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<SavedScrapeResult>>> GetAll(
        CancellationToken cancellationToken)
    {
        return Ok(await store.GetAllAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SavedScrapeResult>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await store.GetByIdAsync(id, cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<SaveScrapeResultResponse>> Create(
        [FromBody] ScrapeResultDto request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var savedResult = await saveService.SaveAsync(request, cancellationToken);
        var response = new SaveScrapeResultResponse(savedResult.Id, savedResult.SavedAt);

        return CreatedAtAction(nameof(GetById), new { id = savedResult.Id }, response);
    }

    [HttpPatch("{id:guid}/rejection")]
    public async Task<ActionResult<SavedScrapeResult>> UpdateRejection(
        Guid id,
        [FromBody] UpdateScrapeResultRejectionRequest request,
        CancellationToken cancellationToken)
    {
        var updatedResult = await store.SetRejectedAsync(id, request.IsRejected, cancellationToken);

        if (updatedResult is null)
        {
            return NotFound();
        }

        return Ok(updatedResult);
    }

    [HttpPatch("{id:guid}/description")]
    public async Task<ActionResult<SavedScrapeResult>> UpdateDescription(
        Guid id,
        [FromBody] UpdateScrapeResultDescriptionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            ModelState.AddModelError(nameof(request.Description), "Description is required.");
            return ValidationProblem(ModelState);
        }

        var updatedResult = await store.UpdateDescriptionAsync(id, request.Description.Trim(), cancellationToken);

        if (updatedResult is null)
        {
            return NotFound();
        }

        return Ok(updatedResult);
    }

    [HttpPatch("{id:guid}/interview-date")]
    public async Task<ActionResult<SavedScrapeResult>> UpdateInterviewDate(
        Guid id,
        [FromBody] UpdateScrapeResultInterviewDateRequest request,
        CancellationToken cancellationToken)
    {
        var updatedResult = await store.UpdateInterviewDateAsync(id, request.InterviewDate, cancellationToken);

        if (updatedResult is null)
        {
            return NotFound();
        }

        return Ok(updatedResult);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await store.DeleteAsync(id, cancellationToken);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    private void ValidateRequest(ScrapeResultDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            ModelState.AddModelError(nameof(request.Title), "Title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Url))
        {
            ModelState.AddModelError(nameof(request.Url), "Url is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            ModelState.AddModelError(nameof(request.Text), "Text is required.");
        }

        if (request.JobDetails is null)
        {
            ModelState.AddModelError(nameof(request.JobDetails), "JobDetails is required.");
        }
    }
}
