using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApplyVault.Api.Controllers;

[ApiController]
[Route("api/calendar-connections")]
[Authorize]
public sealed class CalendarConnectionsController(
    IAppUserService appUserService,
    ICalendarConnectionService calendarConnectionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConnectedCalendarAccountDto>>> GetAll()
    {
        var user = await appUserService.GetRequiredUserAsync();
        return Ok(await calendarConnectionService.GetConnectionsAsync(user));
    }

    [HttpPost("{provider}/start")]
    public async Task<ActionResult<CalendarAuthorizationStartResponse>> StartAuthorization(
        string provider,
        [FromBody] CalendarAuthorizationStartRequest? request)
    {
        if (!CalendarProviders.IsSupported(provider))
        {
            return NotFound();
        }

        var user = await appUserService.GetRequiredUserAsync();
        var authorizationUrl = calendarConnectionService.BuildAuthorizationUrl(user, provider, request?.ReturnUrl);
        return Ok(new CalendarAuthorizationStartResponse(authorizationUrl));
    }

    [AllowAnonymous]
    [HttpGet("{provider}/callback")]
    [EnableRateLimiting(RateLimitingOptions.PolicyOAuthCallback)]
    public async Task<IActionResult> CompleteAuthorization(
        string provider,
        [FromQuery] string code,
        [FromQuery] string state)
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
                state);

            return Redirect(redirectUrl);
        }
        catch (Exception exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var user = await appUserService.GetRequiredUserAsync();
        var deleted = await calendarConnectionService.DeleteConnectionAsync(user, id);
        return deleted ? NoContent() : NotFound();
    }
}
