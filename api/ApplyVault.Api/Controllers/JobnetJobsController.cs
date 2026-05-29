using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services;
using ApplyVault.Api.Services.Jobnet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ApplyVault.Api.Controllers;

[ApiController]
[Route("api/jobnet/jobs")]
[Authorize]
public sealed class JobnetJobsController(
    IJobnetJobClient jobnetJobClient,
    IJobnetJobSearchRequestNormalizer requestNormalizer,
    IJobnetJobSaveService jobnetJobSaveService,
    IAppUserService appUserService) : ControllerBase
{
    [HttpPost("search")]
    [EnableRateLimiting(RateLimitingOptions.PolicyJobnetSearch)]
    public async Task<ActionResult<JobnetJobSearchResponse>> Search([FromBody] JobnetJobSearchRequest request)
    {
        if (!requestNormalizer.TryNormalizeSearchRequest(request, out var normalizedRequest, out var validationMessage))
        {
            ModelState.AddModelError(nameof(request.Keywords), validationMessage);
            return ValidationProblem(ModelState);
        }

        return Ok(await jobnetJobClient.SearchJobsAsync(normalizedRequest));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<JobnetJobDetailResponse>> GetById(string id, [FromQuery] string? requestLanguage)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(new { message = "Job id is required." });
        }

        var language = requestNormalizer.NormalizeRequestLanguage(requestLanguage);
        var detail = await jobnetJobClient.GetJobByIdAsync(id, language);

        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpPost("{id}/save")]
    public async Task<ActionResult<SaveJobnetJobResponse>> Save(string id, [FromQuery] string? requestLanguage)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest(new { message = "Job id is required." });
        }

        var user = await appUserService.GetRequiredUserAsync();
        var language = requestNormalizer.NormalizeRequestLanguage(requestLanguage);
        var response = await jobnetJobSaveService.SaveAsync(id, language, user.Id);

        if (response is null)
        {
            return NotFound();
        }

        if (response.AlreadyExists)
        {
            return Ok(response);
        }

        return Created($"/api/scrape-results/{response.Id}", response);
    }
}
