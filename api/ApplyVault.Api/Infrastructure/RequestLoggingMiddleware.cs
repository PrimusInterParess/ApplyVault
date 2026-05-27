namespace ApplyVault.Api.Infrastructure;

/// <summary>
/// Logs completed requests that return 4xx/5xx with the ASP.NET trace id for correlation.
/// Does not log Authorization headers or request bodies.
/// </summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    private static readonly PathString[] ExcludedPaths =
    [
        "/health",
        "/health/live",
        "/api/health"
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        var statusCode = context.Response.StatusCode;
        if (statusCode < StatusCodes.Status400BadRequest)
        {
            return;
        }

        if (IsExcludedPath(context.Request.Path))
        {
            return;
        }

        var logLevel = statusCode >= StatusCodes.Status500InternalServerError
            ? LogLevel.Error
            : LogLevel.Warning;

        logger.Log(
            logLevel,
            "HTTP {StatusCode} {Method} {Path} TraceId={TraceId}",
            statusCode,
            context.Request.Method,
            context.Request.Path.Value ?? "/",
            context.TraceIdentifier);
    }

    private static bool IsExcludedPath(PathString path)
    {
        foreach (var excluded in ExcludedPaths)
        {
            if (path.StartsWithSegments(excluded, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
