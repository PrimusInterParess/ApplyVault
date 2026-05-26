using ApplyVault.Api.Models;
using ApplyVault.Api.Services.Eures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApplyVault.Api.Controllers;

[ApiController]
[Route("api/eures/jobs")]
[Authorize]
public sealed class EuresJobsController(
    IEuresJobClient euresJobClient,
    IEuresJobSearchRequestNormalizer requestNormalizer) : ControllerBase
{
    [HttpPost("search")]
    public async Task<ActionResult<EuresJobSearchResponse>> Search(
        [FromBody] EuresJobSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (!requestNormalizer.TryNormalizeSearchRequest(request, out var normalizedRequest, out var validationMessage))
        {
            ModelState.AddModelError(nameof(request.Keywords), validationMessage);
            return ValidationProblem(ModelState);
        }

        return Ok(await euresJobClient.SearchJobsAsync(normalizedRequest, cancellationToken));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<EuresJobDetailResponse>> GetById(
        string id,
        [FromQuery] string? requestLanguage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(new { message = "Job id is required." });
        }

        var language = requestNormalizer.NormalizeRequestLanguage(requestLanguage);
        var detail = await euresJobClient.GetJobByIdAsync(id, language, cancellationToken);

        return detail is null ? NotFound() : Ok(detail);
    }
}
