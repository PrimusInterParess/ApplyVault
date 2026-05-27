using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApplyVault.Api.Controllers;

[ApiController]
[Route("api/cv-projects")]
[Authorize]
public sealed class CvProjectsController(
    IAppUserService appUserService,
    IGitHubProjectSummaryService gitHubProjectSummaryService) : ControllerBase
{
    [HttpPost("generate")]
    public async Task<ActionResult<CvProjectSummaryDto>> Generate(
        [FromBody] GenerateCvProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);

        try
        {
            return Ok(await gitHubProjectSummaryService.GenerateAsync(user, request.FullName, cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CvProjectSummaryDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int perPage = 5,
        CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        return Ok(await gitHubProjectSummaryService.ListSummariesAsync(user, page, perPage, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CvProjectSummaryDto>> Get(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var summary = await gitHubProjectSummaryService.GetSummaryAsync(user, id, cancellationToken);
        return summary is null ? NotFound() : Ok(summary);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await appUserService.GetRequiredUserAsync(cancellationToken);
        var deleted = await gitHubProjectSummaryService.DeleteSummaryAsync(user, id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
