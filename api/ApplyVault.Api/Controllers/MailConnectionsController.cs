using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApplyVault.Api.Controllers;

[ApiController]
[Route("api/mail-connections")]
[Authorize]
public sealed class MailConnectionsController(
    IAppUserService appUserService,
    IMailConnectionService mailConnectionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConnectedMailAccountDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        return Ok(await mailConnectionService.GetConnectionsAsync(user, cancellationToken));
    }

    [HttpPost("{provider}/start")]
    public async Task<ActionResult<MailAuthorizationStartResponse>> StartAuthorization(
        string provider,
        [FromBody] MailAuthorizationStartRequest? request,
        CancellationToken cancellationToken)
    {
        if (!MailProviders.IsSupported(provider))
        {
            return NotFound();
        }

        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var authorizationUrl = mailConnectionService.BuildAuthorizationUrl(user, provider, request?.ReturnUrl);
        return Ok(new MailAuthorizationStartResponse(authorizationUrl));
    }

    [AllowAnonymous]
    [HttpGet("{provider}/callback")]
    public async Task<IActionResult> CompleteAuthorization(
        string provider,
        [FromQuery] string code,
        [FromQuery] string state,
        CancellationToken cancellationToken)
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
        var deleted = await mailConnectionService.DeleteConnectionAsync(user, id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
