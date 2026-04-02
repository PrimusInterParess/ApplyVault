using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApplyVault.Api.Controllers;

[ApiController]
[Route("api/calendar-connections")]
[Authorize]
public sealed class CalendarConnectionsController(
    IAppUserService appUserService,
    ICalendarConnectionService calendarConnectionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConnectedCalendarAccountDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        return Ok(await calendarConnectionService.GetConnectionsAsync(user, cancellationToken));
    }

    [HttpPost("{provider}/start")]
    public async Task<ActionResult<CalendarAuthorizationStartResponse>> StartAuthorization(
        string provider,
        [FromBody] CalendarAuthorizationStartRequest? request,
        CancellationToken cancellationToken)
    {
        if (!CalendarProviders.IsSupported(provider))
        {
            return NotFound();
        }

        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var authorizationUrl = calendarConnectionService.BuildAuthorizationUrl(user, provider, request?.ReturnUrl);
        return Ok(new CalendarAuthorizationStartResponse(authorizationUrl));
    }

    [AllowAnonymous]
    [HttpGet("{provider}/callback")]
    public async Task<IActionResult> CompleteAuthorization(
        string provider,
        [FromQuery] string code,
        [FromQuery] string state,
        CancellationToken cancellationToken)
    {
        if (!CalendarProviders.IsSupported(provider))
        {
            return NotFound();
        }

        try
        {
            var redirectUrl = await calendarConnectionService.CompleteAuthorizationAsync(
                provider,
                code,
                state,
                cancellationToken);

            return Redirect(redirectUrl);
        }
        catch (Exception exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var deleted = await calendarConnectionService.DeleteConnectionAsync(user, id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
