using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApplyVault.Api.Controllers;

[ApiController]
[Route("api/interview-prep")]
[Authorize]
public sealed class InterviewPrepController(
    IAppUserService appUserService,
    IInterviewPrepService interviewPrepService) : ControllerBase
{
    [HttpPost("sessions")]
    public async Task<ActionResult<InterviewPrepSessionSummaryDto>> CreateSession(
        [FromBody] InterviewPrepCreateSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);

        try
        {
            var created = await interviewPrepService.CreateSessionAsync(user, request, cancellationToken);
            return CreatedAtAction(nameof(GetSession), new { id = created.Id }, created);
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

    [HttpGet("sessions")]
    public async Task<ActionResult<InterviewPrepSessionListResponseDto>> ListSessions(
        [FromQuery] int take = 20,
        [FromQuery] int skip = 0,
        CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        return Ok(await interviewPrepService.ListSessionsAsync(user, take, skip, cancellationToken));
    }

    [HttpGet("sessions/{id:guid}")]
    public async Task<ActionResult<InterviewPrepSessionDetailDto>> GetSession(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);

        try
        {
            return Ok(await interviewPrepService.GetSessionAsync(user, id, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> DeleteSession(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var deleted = await interviewPrepService.DeleteSessionAsync(user, id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [HttpPost("turns")]
    public async Task<ActionResult<InterviewPrepTurnResponseDto>> CreateTurn(
        [FromBody] InterviewPrepTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);

        try
        {
            return Ok(await interviewPrepService.CreateTurnAsync(user, request, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InterviewPrepSessionConflictException exception)
        {
            return Conflict(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}
