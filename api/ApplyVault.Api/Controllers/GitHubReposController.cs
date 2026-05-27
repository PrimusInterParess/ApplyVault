using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApplyVault.Api.Controllers;

[ApiController]
[Route("api/github/repos")]
[Authorize]
public sealed class GitHubReposController(
    IAppUserService appUserService,
    IGitHubProjectSummaryService gitHubProjectSummaryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GitHubRepositoryListItemDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int perPage = 100,
        CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);

        try
        {
            return Ok(await gitHubProjectSummaryService.ListRepositoriesAsync(user, page, perPage, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}
