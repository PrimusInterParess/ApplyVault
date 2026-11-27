using ApplyVault.Api.Data;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Services.JobRequirements;

internal sealed class JobRequirementsConcurrencyExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateConcurrencyException conflict || conflict.Entries.Count == 0 ||
            conflict.Entries.Any(entry => entry.Entity is not ScrapeResultEntity))
        {
            return false;
        }

        await Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Saved job changed",
            detail: "The saved job changed while this request was processing. Reload the job and try again.")
            .ExecuteAsync(context);
        return true;
    }
}
