using ApplyVault.Api.Data;

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
        app.UseCors();
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHealthChecks("/health");

        return app;
    }
}
