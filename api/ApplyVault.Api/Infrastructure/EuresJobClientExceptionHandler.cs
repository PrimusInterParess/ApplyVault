using System.Diagnostics;
using ApplyVault.Api.Services.Eures;
using Microsoft.AspNetCore.Diagnostics;

namespace ApplyVault.Api.Infrastructure;

public sealed class EuresJobClientExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not EuresJobClientException euresException)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status502BadGateway;
        await httpContext.Response.WriteAsJsonAsync(
            new
            {
                message = euresException.Message,
                status = StatusCodes.Status502BadGateway,
                title = "EURES integration error",
                detail = euresException.Message,
                traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier
            },
            cancellationToken);

        return true;
    }
}
