using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApplyVault.Api.Controllers;

[ApiController]
[Route("api/scrape-results")]
 [Authorize]
public sealed class ScrapeResultsController(
    IAppUserService appUserService,
    IScrapeResultStore store,
    IScrapeResultSaveService saveService,
    ICalendarEventService calendarEventService,
    IHostApplicationLifetime applicationLifetime) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<SavedScrapeResult>>> GetAll(
        CancellationToken cancellationToken)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        return Ok(await store.GetAllAsync(user.Id, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SavedScrapeResult>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var result = await store.GetByIdAsync(id, user.Id, cancellationToken);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [AllowAnonymous]
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

        var currentUser = await appUserService.TryGetCurrentUserAsync(cancellationToken);
        // Let long-running enrichment/save work finish even if the extension closes the HTTP request.
        var savedResult = await saveService.SaveAsync(
            request,
            currentUser?.Id,
            applicationLifetime.ApplicationStopping);
        var response = new SaveScrapeResultResponse(savedResult.Id, savedResult.SavedAt);

        return CreatedAtAction(nameof(GetById), new { id = savedResult.Id }, response);
    }

    [HttpPatch("{id:guid}/rejection")]
    public async Task<ActionResult<SavedScrapeResult>> UpdateRejection(
        Guid id,
        [FromBody] UpdateScrapeResultRejectionRequest request,
        CancellationToken cancellationToken)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var updatedResult = await store.SetRejectedAsync(id, user.Id, request.IsRejected, cancellationToken);

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
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            ModelState.AddModelError(nameof(request.Description), "Description is required.");
            return ValidationProblem(ModelState);
        }

        var updatedResult = await store.UpdateDescriptionAsync(
            id,
            user.Id,
            request.Description.Trim(),
            cancellationToken);

        if (updatedResult is null)
        {
            return NotFound();
        }

        return Ok(updatedResult);
    }

    [HttpPatch("{id:guid}/capture-review")]
    public async Task<ActionResult<SavedScrapeResult>> UpdateCaptureReview(
        Guid id,
        [FromBody] UpdateScrapeResultCaptureReviewRequest request,
        CancellationToken cancellationToken)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var updatedResult = await store.UpdateCaptureReviewAsync(id, user.Id, request, cancellationToken);

        if (updatedResult is null)
        {
            return NotFound();
        }

        return Ok(updatedResult);
    }

    [HttpPut("{id:guid}/interview-event")]
    public async Task<ActionResult<SavedScrapeResult>> UpsertInterviewEvent(
        Guid id,
        [FromBody] UpdateInterviewEventRequest request,
        CancellationToken cancellationToken)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);

        if (request.EndUtc <= request.StartUtc)
        {
            ModelState.AddModelError(nameof(request.EndUtc), "EndUtc must be later than StartUtc.");
            return ValidationProblem(ModelState);
        }

        if (string.IsNullOrWhiteSpace(request.TimeZone))
        {
            ModelState.AddModelError(nameof(request.TimeZone), "TimeZone is required.");
            return ValidationProblem(ModelState);
        }

        var updatedResult = await store.UpsertInterviewEventAsync(id, user.Id, request, cancellationToken);

        if (updatedResult is null)
        {
            return NotFound();
        }

        return Ok(updatedResult);
    }

    [HttpDelete("{id:guid}/interview-event")]
    public async Task<ActionResult<SavedScrapeResult>> ClearInterviewEvent(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var updatedResult = await store.ClearInterviewEventAsync(id, user.Id, cancellationToken);

        if (updatedResult is null)
        {
            return NotFound();
        }

        return Ok(updatedResult);
    }

    [HttpPost("{id:guid}/calendar-events")]
    public async Task<ActionResult<CalendarEventLinkDto>> SyncCalendarEvent(
        Guid id,
        [FromBody] CreateCalendarEventRequest request,
        CancellationToken cancellationToken)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var calendarEvent = await calendarEventService.SyncEventAsync(
            user,
            id,
            request.ConnectedAccountId,
            cancellationToken);

        return Ok(calendarEvent);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var deleted = await store.DeleteAsync(id, user.Id, cancellationToken);

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
