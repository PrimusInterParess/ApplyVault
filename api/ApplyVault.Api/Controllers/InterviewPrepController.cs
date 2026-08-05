using ApplyVault.Api.Models.InterviewPrep;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services;
using ApplyVault.Api.Services.InterviewPrep;
using ApplyVault.Api.Services.InterviewPrep.Coaching;
using ApplyVault.Api.Services.InterviewPrep.Domain;
using ApplyVault.Api.Services.InterviewPrep.Reporting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Controllers;

[ApiController]
[Route("api/interview-prep")]
[Authorize]
public sealed class InterviewPrepController(
    IAppUserService appUserService,
    IInterviewPrepSessionService sessionService,
    IInterviewPrepReportingService reportingService,
    IInterviewPrepCoachingService coachingService) : ControllerBase
{
    [HttpPost("sessions")]
    public async Task<ActionResult<InterviewPrepSessionSummaryDto>> CreateSession(
        [FromBody] InterviewPrepCreateSessionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await appUserService.GetRequiredUserAsync();
            var created = await sessionService.CreateAsync(user, request, cancellationToken);
            return CreatedAtAction(nameof(GetSession), new { id = created.Id }, created);
        }
        catch (InterviewPrepValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("sessions")]
    public async Task<ActionResult<InterviewPrepSessionListResponseDto>> ListSessions(
        CancellationToken cancellationToken)
    {
        var user = await appUserService.GetRequiredUserAsync();
        return Ok(await sessionService.ListAsync(user, cancellationToken));
    }

    [HttpGet("sessions/{id:guid}")]
    public async Task<ActionResult<InterviewPrepSessionDetailDto>> GetSession(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await appUserService.GetRequiredUserAsync();
            var detail = await sessionService.GetAsync(user, id, cancellationToken);
            Response.Headers.ETag = detail.ETag;
            return Ok(detail);
        }
        catch (InterviewPrepNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> DeleteSession(Guid id, CancellationToken cancellationToken)
    {
        var user = await appUserService.GetRequiredUserAsync();
        var deleted = await sessionService.DeleteAsync(user, id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("sessions/{id:guid}/prepare")]
    [EnableRateLimiting(RateLimitingOptions.PolicyInterviewPrep)]
    public Task<ActionResult<InterviewPrepSessionDetailDto>> Prepare(
        Guid id,
        CancellationToken cancellationToken) =>
        MutateAsync(id, sessionService.PrepareAsync, cancellationToken);

    [HttpPost("sessions/{id:guid}/start")]
    public Task<ActionResult<InterviewPrepSessionDetailDto>> Start(
        Guid id,
        CancellationToken cancellationToken) =>
        MutateAsync(id, sessionService.StartAsync, cancellationToken);

    [HttpPost("sessions/{id:guid}/pause")]
    public Task<ActionResult<InterviewPrepSessionDetailDto>> Pause(
        Guid id,
        CancellationToken cancellationToken) =>
        MutateAsync(id, sessionService.PauseAsync, cancellationToken);

    [HttpPost("sessions/{id:guid}/resume")]
    public Task<ActionResult<InterviewPrepSessionDetailDto>> Resume(
        Guid id,
        CancellationToken cancellationToken) =>
        MutateAsync(id, sessionService.ResumeAsync, cancellationToken);

    [HttpPost("sessions/{id:guid}/cancel")]
    public Task<ActionResult<InterviewPrepSessionDetailDto>> Cancel(
        Guid id,
        CancellationToken cancellationToken) =>
        MutateAsync(id, sessionService.CancelAsync, cancellationToken);

    [HttpPost("sessions/{id:guid}/complete")]
    public Task<ActionResult<InterviewPrepSessionDetailDto>> Complete(
        Guid id,
        CancellationToken cancellationToken) =>
        MutateAsync(id, sessionService.CompleteAsync, cancellationToken);

    [HttpPost("sessions/{id:guid}/turns")]
    [EnableRateLimiting(RateLimitingOptions.PolicyInterviewPrep)]
    public async Task<ActionResult<InterviewPrepTurnSubmitResponseDto>> SubmitTurn(
        Guid id,
        [FromBody] InterviewPrepSubmitTurnRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await appUserService.GetRequiredUserAsync();
            var result = await sessionService.SubmitTurnAsync(
                user,
                id,
                request,
                Request.Headers.IfMatch.ToString(),
                cancellationToken);
            Response.Headers.ETag = result.Session.ETag;
            return Ok(result);
        }
        catch (InterviewPrepNotFoundException)
        {
            return NotFound();
        }
        catch (InterviewPrepValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InterviewPrepConflictException ex)
        {
            return Conflict(new { message = ex.Message, code = ex.ErrorCode });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                message = "The session was modified by another request.",
                code = "interview_prep_concurrency_conflict"
            });
        }
    }

    [HttpGet("sessions/{id:guid}/transcript")]
    public async Task<ActionResult<InterviewPrepTranscriptDto>> GetTranscript(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await appUserService.GetRequiredUserAsync();
            return Ok(await reportingService.GetTranscriptAsync(user, id, cancellationToken));
        }
        catch (InterviewPrepNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("sessions/{id:guid}/report")]
    public async Task<ActionResult<InterviewPrepCandidateReportDto>> GetReport(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await appUserService.GetRequiredUserAsync();
            return Ok(await reportingService.GetReportAsync(user, id, cancellationToken));
        }
        catch (InterviewPrepNotFoundException)
        {
            return NotFound();
        }
        catch (InterviewPrepConflictException ex)
        {
            return Conflict(new { message = ex.Message, code = ex.ErrorCode });
        }
    }

    [HttpGet("sessions/{id:guid}/competencies")]
    public async Task<ActionResult<InterviewPrepCompetencyResultsDto>> GetCompetencies(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await appUserService.GetRequiredUserAsync();
            return Ok(await reportingService.GetCompetenciesAsync(user, id, cancellationToken));
        }
        catch (InterviewPrepNotFoundException)
        {
            return NotFound();
        }
        catch (InterviewPrepConflictException ex)
        {
            return Conflict(new { message = ex.Message, code = ex.ErrorCode });
        }
    }

    [HttpPost("sessions/{id:guid}/full-loop/next-stage")]
    public Task<ActionResult<InterviewPrepSessionDetailDto>> StartNextFullLoopStage(
        Guid id,
        CancellationToken cancellationToken) =>
        MutateAsync(id, sessionService.StartNextFullLoopStageAsync, cancellationToken);

    [HttpPost("sessions/{id:guid}/loop-guard/revisit")]
    public Task<ActionResult<InterviewPrepSessionDetailDto>> ApproveLoopGuardRevisit(
        Guid id,
        [FromBody] InterviewPrepLoopGuardRevisitRequest request,
        CancellationToken cancellationToken) =>
        MutateAsync(
            id,
            (user, sessionId, ifMatch, ct) =>
                sessionService.ApproveLoopGuardRevisitAsync(user, sessionId, request, ifMatch, ct),
            cancellationToken);

    [HttpGet("sessions/{id:guid}/panel-debrief")]
    public async Task<ActionResult<InterviewPrepPanelDebriefDto>> GetPanelDebrief(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await appUserService.GetRequiredUserAsync();
            return Ok(await sessionService.GetPanelDebriefAsync(user, id, cancellationToken));
        }
        catch (InterviewPrepNotFoundException)
        {
            return NotFound();
        }
        catch (InterviewPrepValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InterviewPrepConflictException ex)
        {
            return Conflict(new { message = ex.Message, code = ex.ErrorCode });
        }
    }

    [HttpPost("sessions/{id:guid}/turns/{turnId:guid}/review")]
    [EnableRateLimiting(RateLimitingOptions.PolicyInterviewPrep)]
    public async Task<ActionResult<InterviewPrepAnswerReviewDto>> RequestAnswerReview(
        Guid id,
        Guid turnId,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await appUserService.GetRequiredUserAsync();
            return Ok(await coachingService.RequestReviewAsync(user, id, turnId, cancellationToken));
        }
        catch (InterviewPrepNotFoundException)
        {
            return NotFound();
        }
        catch (InterviewPrepValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InterviewPrepConflictException ex)
        {
            return Conflict(new { message = ex.Message, code = ex.ErrorCode });
        }
    }

    [HttpPost("sessions/{id:guid}/turns/{turnId:guid}/retry")]
    [EnableRateLimiting(RateLimitingOptions.PolicyInterviewPrep)]
    public async Task<ActionResult<InterviewPrepAnswerRetryResultDto>> SubmitAnswerRetry(
        Guid id,
        Guid turnId,
        [FromBody] InterviewPrepSubmitAnswerRetryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await appUserService.GetRequiredUserAsync();
            return Ok(await coachingService.SubmitRetryAsync(user, id, turnId, request, cancellationToken));
        }
        catch (InterviewPrepNotFoundException)
        {
            return NotFound();
        }
        catch (InterviewPrepValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InterviewPrepConflictException ex)
        {
            return Conflict(new { message = ex.Message, code = ex.ErrorCode });
        }
    }

    [HttpGet("sessions/{id:guid}/turns/{turnId:guid}/retry")]
    public async Task<ActionResult<InterviewPrepAnswerRetryResultDto>> GetAnswerRetry(
        Guid id,
        Guid turnId,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await appUserService.GetRequiredUserAsync();
            return Ok(await coachingService.GetRetryAsync(user, id, turnId, cancellationToken));
        }
        catch (InterviewPrepNotFoundException)
        {
            return NotFound();
        }
        catch (InterviewPrepValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InterviewPrepConflictException ex)
        {
            return Conflict(new { message = ex.Message, code = ex.ErrorCode });
        }
    }

    private async Task<ActionResult<InterviewPrepSessionDetailDto>> MutateAsync(
        Guid id,
        Func<Data.AppUserEntity, Guid, string?, CancellationToken, Task<InterviewPrepSessionDetailDto>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await appUserService.GetRequiredUserAsync();
            var detail = await action(user, id, Request.Headers.IfMatch.ToString(), cancellationToken);
            Response.Headers.ETag = detail.ETag;
            return Ok(detail);
        }
        catch (InterviewPrepNotFoundException)
        {
            return NotFound();
        }
        catch (InterviewPrepValidationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InterviewPrepConflictException ex)
        {
            return Conflict(new { message = ex.Message, code = ex.ErrorCode });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                message = "The session was modified by another request.",
                code = "interview_prep_concurrency_conflict"
            });
        }
    }
}
