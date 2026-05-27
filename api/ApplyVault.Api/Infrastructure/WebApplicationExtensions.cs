using ApplyVault.Api.Data;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace ApplyVault.Api.Infrastructure;

public static class WebApplicationExtensions
{
    public static WebApplication UseApplyVaultPipeline(this WebApplication app)
    {
        app.InitializeApplyVaultDatabase();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        // TLS termination and HSTS are handled at the edge (Caddy in deploy/). Do not call
        // UseHttpsRedirection here — the API listens on HTTP behind the reverse proxy.
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseCors();
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapControllers();
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = (check) => check.Tags.Contains(HealthCheckTags.Ready),
            ResponseWriter = HealthCheckResponseWriter.WriteReadinessResponse
        });
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = (check) => check.Tags.Contains(HealthCheckTags.Live),
            ResponseWriter = HealthCheckResponseWriter.WriteLivenessResponse
        });

        return app;
    }
}
