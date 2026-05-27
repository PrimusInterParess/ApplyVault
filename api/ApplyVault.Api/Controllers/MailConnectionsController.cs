using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApplyVault.Api.Controllers;

[ApiController]
[Route("api/mail-connections")]
[Authorize]
public sealed class MailConnectionsController(
    IAppUserService appUserService,
    IMailConnectionService mailConnectionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConnectedMailAccountDto>>> GetAll()
    {
        var user = await appUserService.GetRequiredUserAsync();
        return Ok(await mailConnectionService.GetConnectionsAsync(user));
    }

    [HttpPost("{provider}/start")]
    public async Task<ActionResult<MailAuthorizationStartResponse>> StartAuthorization(
        string provider,
        [FromBody] MailAuthorizationStartRequest? request)
    {
        if (!MailProviders.IsSupported(provider))
        {
            return NotFound();
        }

        var user = await appUserService.GetRequiredUserAsync();
        var authorizationUrl = mailConnectionService.BuildAuthorizationUrl(user, provider, request?.ReturnUrl);
        return Ok(new MailAuthorizationStartResponse(authorizationUrl));
    }

    [AllowAnonymous]
    [HttpGet("{provider}/callback")]
    [EnableRateLimiting(RateLimitingOptions.PolicyOAuthCallback)]
    public async Task<IActionResult> CompleteAuthorization(
        string provider,
        [FromQuery] string code,
        [FromQuery] string state)
    {
        if (!MailProviders.IsSupported(provider))
        {
            return NotFound();
        }

        try
        {
            var redirectUrl = await mailConnectionService.CompleteAuthorizationAsync(
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
        var deleted = await mailConnectionService.DeleteConnectionAsync(user, id);
        return deleted ? NoContent() : NotFound();
    }
}
