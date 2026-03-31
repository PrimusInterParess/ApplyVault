using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApplyVault.Api.Controllers;

[ApiController]
[Route("api/scrape-results")]
public sealed class ScrapeResultsController(IScrapeResultStore store) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyCollection<SavedScrapeResult>> GetAll()
    {
        return Ok(store.GetAll());
    }

    [HttpGet("{id:guid}")]
    public ActionResult<SavedScrapeResult> GetById(Guid id)
    {
        var result = store.GetById(id);

        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPost]
    public ActionResult<SaveScrapeResultResponse> Create([FromBody] ScrapeResultDto request)
    {
        ValidateRequest(request);

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var savedResult = store.Save(request);
        var response = new SaveScrapeResultResponse(savedResult.Id, savedResult.SavedAt);

        return CreatedAtAction(nameof(GetById), new { id = savedResult.Id }, response);
    }

    private void ValidateRequest(ScrapeResultDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            ModelState.AddModelError(nameof(request.Title), "Title is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Url))
        {
            ModelState.AddModelError(nameof(request.Url), "Url is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            ModelState.AddModelError(nameof(request.Text), "Text is required.");
        }

        if (request.JobDetails is null)
        {
            ModelState.AddModelError(nameof(request.JobDetails), "JobDetails is required.");
        }
    }
}
