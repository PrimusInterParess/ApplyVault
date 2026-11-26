using ApplyVault.Api.Models.InterviewPrep;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services;
using ApplyVault.Api.Services.InterviewPrep;
using ApplyVault.Api.Services.InterviewPrep.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Controllers;

[ApiController]
[Route("api/interview-prep")]
[Authorize]
public sealed class InterviewPrepController(IAppUserService users, InterviewPreparationPackService packs) : ControllerBase
{
    [HttpGet("context")]
    public async Task<ActionResult<PreparationContextDto>> Context(CancellationToken ct) =>
        Ok(await packs.ContextAsync(await users.GetRequiredUserAsync(), ct));

    [HttpGet("packs")]
    public async Task<ActionResult<IReadOnlyList<PreparationPackDto>>> List(CancellationToken ct) =>
        Ok(await packs.ListAsync(await users.GetRequiredUserAsync(), ct));

    [HttpGet("packs/{id:guid}")]
    public async Task<ActionResult<PreparationPackDto>> Get(Guid id, CancellationToken ct)
    {
        var user = await users.GetRequiredUserAsync();
        return await Execute(() => packs.GetAsync(user, id, ct));
    }

    [HttpPost("packs")]
    [EnableRateLimiting(RateLimitingOptions.PolicyInterviewPrep)]
    public async Task<ActionResult<PreparationPackDto>> Generate([FromBody] PreparationPackInput input, CancellationToken ct)
    {
        var user = await users.GetRequiredUserAsync();
        return await Execute(async () => await packs.GenerateAsync(user, input, ct), created: true);
    }

    [HttpPost("packs/{id:guid}/regenerate")]
    [EnableRateLimiting(RateLimitingOptions.PolicyInterviewPrep)]
    public async Task<ActionResult<PreparationPackDto>> Regenerate(Guid id, [FromBody] RegeneratePreparationPackRequest request, CancellationToken ct)
    {
        var user = await users.GetRequiredUserAsync();
        return await Execute(() => packs.RegenerateAsync(user, id, request, ct));
    }

    [HttpPut("packs/{id:guid}/progress")]
    public async Task<ActionResult<PreparationPackDto>> Progress(Guid id, [FromBody] PreparationProgressRequest request, CancellationToken ct)
    {
        var user = await users.GetRequiredUserAsync();
        return await Execute(() => packs.SaveProgressAsync(user, id, request, ct));
    }

    [HttpPost("packs/{id:guid}/documents/{documentId}/discovery")]
    [EnableRateLimiting(RateLimitingOptions.PolicyInterviewPrep)]
    public async Task<ActionResult<PreparationPackDto>> PrepareDocument(Guid id, string documentId,
        [FromBody] PreparationRevisionRequest request, CancellationToken ct)
    {
        var user = await users.GetRequiredUserAsync();
        return await Execute(() => packs.PrepareDocumentAsync(user, id, documentId, request, ct));
    }

    [HttpPut("packs/{id:guid}/documents/{documentId}/answers")]
    public async Task<ActionResult<PreparationPackDto>> SaveAnswers(Guid id, string documentId,
        [FromBody] PreparationAnswersRequest request, CancellationToken ct)
    {
        var user = await users.GetRequiredUserAsync();
        return await Execute(() => packs.SaveAnswersAsync(user, id, documentId, request, ct));
    }

    [HttpPost("packs/{id:guid}/documents/{documentId}/generate")]
    [EnableRateLimiting(RateLimitingOptions.PolicyInterviewPrep)]
    public async Task<ActionResult<PreparationPackDto>> WriteDocument(Guid id, string documentId,
        [FromBody] PreparationRevisionRequest request, CancellationToken ct)
    {
        var user = await users.GetRequiredUserAsync();
        return await Execute(() => packs.WriteDocumentAsync(user, id, documentId, request, ct));
    }

    [HttpPost("packs/{id:guid}/documents/{documentId}/questions/{questionId}/help")]
    [EnableRateLimiting(RateLimitingOptions.PolicyInterviewPrep)]
    public async Task<ActionResult<PreparationHelpResponse>> Help(Guid id, string documentId, string questionId,
        [FromBody] PreparationHelpRequest request, CancellationToken ct)
    {
        var user = await users.GetRequiredUserAsync();
        return await Execute(() => packs.HelpAsync(user, id, documentId, questionId, request, ct));
    }

    [HttpDelete("packs/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        await packs.DeleteAsync(await users.GetRequiredUserAsync(), id, ct) ? NoContent() : NotFound();

    private async Task<ActionResult<T>> Execute<T>(Func<Task<T>> action, bool created = false)
    {
        try
        {
            var result = await action();
            return created && result is PreparationPackDto pack
                ? CreatedAtAction(nameof(Get), new { id = pack.Id }, result) : Ok(result);
        }
        catch (InterviewPrepNotFoundException) { return NotFound(); }
        catch (InterviewPrepValidationException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InterviewPrepConflictException ex) { return Conflict(new { message = ex.Message, existingPackId = ex.ExistingPackId }); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "This pack changed in another tab. Reload it before saving." }); }
        catch (InterviewPrepAiUnavailableException ex) { return StatusCode(503, new { message = ex.Message, code = ex.FailureCode }); }
    }
}
