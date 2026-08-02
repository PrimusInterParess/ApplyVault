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
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}
