using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApplyVault.Api.Controllers;

[ApiController]
[Route("api/github-connections")]
[Authorize]
public sealed class GitHubConnectionsController(
    IAppUserService appUserService,
    IGitHubConnectionService gitHubConnectionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConnectedGitHubAccountDto>>> GetAll()
    {
        var user = await appUserService.GetRequiredUserAsync();
        return Ok(await gitHubConnectionService.GetConnectionsAsync(user));
    }

    [HttpPost("{provider}/start")]
    public async Task<ActionResult<GitHubAuthorizationStartResponse>> StartAuthorization(
        string provider,
        [FromBody] GitHubAuthorizationStartRequest? request)
    {
        if (!GitHubProviders.IsSupported(provider))
        {
            return NotFound();
        }

        var user = await appUserService.GetRequiredUserAsync();
        var authorizationUrl = gitHubConnectionService.BuildAuthorizationUrl(user, provider, request?.ReturnUrl);
        return Ok(new GitHubAuthorizationStartResponse(authorizationUrl));
    }

    [AllowAnonymous]
    [HttpGet("{provider}/callback")]
    [EnableRateLimiting(RateLimitingOptions.PolicyOAuthCallback)]
    public async Task<IActionResult> CompleteAuthorization(
        string provider,
        [FromQuery] string code,
        [FromQuery] string state)
    {
        if (!GitHubProviders.IsSupported(provider))
        {
            return NotFound();
        }

        try
        {
            var redirectUrl = await gitHubConnectionService.CompleteAuthorizationAsync(
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
        var deleted = await gitHubConnectionService.DeleteConnectionAsync(user, id);
        return deleted ? NoContent() : NotFound();
    }
}
