using ApplyVault.Api.Models;

using ApplyVault.Api.Options;

using ApplyVault.Api.Services;

using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.RateLimiting;



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

    public async Task<ActionResult<IReadOnlyCollection<SavedScrapeResult>>> GetAll()

    {

        var user = await appUserService.GetRequiredUserAsync();

        return Ok(await store.GetAllAsync(user.Id));

    }



    [HttpGet("{id:guid}")]

    public async Task<ActionResult<SavedScrapeResult>> GetById(Guid id)

    {

        var user = await appUserService.GetRequiredUserAsync();

        var result = await store.GetByIdAsync(id, user.Id);



        if (result is null)

        {

            return NotFound();

        }



        return Ok(result);

    }



    [HttpPost]
    [EnableRateLimiting(RateLimitingOptions.PolicyScrapeIngest)]
    public async Task<ActionResult<SaveScrapeResultResponse>> Create([FromBody] ScrapeResultDto request)

    {

        ValidateRequest(request);



        if (!ModelState.IsValid)

        {

            return ValidationProblem(ModelState);

        }



        var user = await appUserService.GetRequiredUserAsync();

        // Let long-running enrichment/save work finish even if the extension closes the HTTP request.

        var savedResult = await saveService.SaveAsync(

            request,

            user.Id,

            applicationLifetime.ApplicationStopping);

        var response = new SaveScrapeResultResponse(savedResult.Id, savedResult.SavedAt);



        return CreatedAtAction(nameof(GetById), new { id = savedResult.Id }, response);

    }



    [HttpPatch("{id:guid}/rejection")]

    public async Task<ActionResult<SavedScrapeResult>> UpdateRejection(

        Guid id,

        [FromBody] UpdateScrapeResultRejectionRequest request)

    {

        var user = await appUserService.GetRequiredUserAsync();

        var updatedResult = await store.SetRejectedAsync(id, user.Id, request.IsRejected);



        if (updatedResult is null)

        {

            return NotFound();

        }



        return Ok(updatedResult);

    }



    [HttpPatch("{id:guid}/description")]

    public async Task<ActionResult<SavedScrapeResult>> UpdateDescription(

        Guid id,

        [FromBody] UpdateScrapeResultDescriptionRequest request)

    {

        var user = await appUserService.GetRequiredUserAsync();



        if (string.IsNullOrWhiteSpace(request.Description))

        {

            ModelState.AddModelError(nameof(request.Description), "Description is required.");

            return ValidationProblem(ModelState);

        }



        var updatedResult = await store.UpdateDescriptionAsync(

            id,

            user.Id,

            request.Description.Trim());



        if (updatedResult is null)

        {

            return NotFound();

        }



        return Ok(updatedResult);

    }



    [HttpPatch("{id:guid}/capture-review")]

    public async Task<ActionResult<SavedScrapeResult>> UpdateCaptureReview(

        Guid id,

        [FromBody] UpdateScrapeResultCaptureReviewRequest request)

    {

        var user = await appUserService.GetRequiredUserAsync();

        var updatedResult = await store.UpdateCaptureReviewAsync(id, user.Id, request);



        if (updatedResult is null)

        {

            return NotFound();

        }



        return Ok(updatedResult);

    }



    [HttpPut("{id:guid}/interview-event")]

    public async Task<ActionResult<SavedScrapeResult>> UpsertInterviewEvent(

        Guid id,

        [FromBody] UpdateInterviewEventRequest request)

    {

        var user = await appUserService.GetRequiredUserAsync();



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



        var updatedResult = await store.UpsertInterviewEventAsync(id, user.Id, request);



        if (updatedResult is null)

        {

            return NotFound();

        }



        return Ok(updatedResult);

    }



    [HttpDelete("{id:guid}/interview-event")]

    public async Task<ActionResult<SavedScrapeResult>> ClearInterviewEvent(Guid id)

    {

        var user = await appUserService.GetRequiredUserAsync();

        var updatedResult = await store.ClearInterviewEventAsync(id, user.Id);



        if (updatedResult is null)

        {

            return NotFound();

        }



        return Ok(updatedResult);

    }



    [HttpPost("{id:guid}/calendar-events")]

    public async Task<ActionResult<CalendarEventLinkDto>> SyncCalendarEvent(

        Guid id,

        [FromBody] CreateCalendarEventRequest request)

    {

        var user = await appUserService.GetRequiredUserAsync();

        var calendarEvent = await calendarEventService.SyncEventAsync(

            user,

            id,

            request.ConnectedAccountId);



        return Ok(calendarEvent);

    }



    [HttpDelete("{id:guid}")]

    public async Task<IActionResult> Delete(Guid id)

    {

        var user = await appUserService.GetRequiredUserAsync();

        var deleted = await store.DeleteAsync(id, user.Id);



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


