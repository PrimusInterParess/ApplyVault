using System.Diagnostics;
using ApplyVault.Api.Services.Jobnet;
using Microsoft.AspNetCore.Diagnostics;

namespace ApplyVault.Api.Infrastructure;

public sealed class JobnetJobClientExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not JobnetJobClientException jobnetException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
        await httpContext.Response.WriteAsJsonAsync(
            new
            {
                message = jobnetException.Message,
                status = StatusCodes.Status502BadGateway,
                title = "Jobnet integration error",
                detail = jobnetException.Message,
                traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier
            },
            cancellationToken);

        return true;
    }
}
