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

        app.UseCors();
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHealthChecks("/health");

        return app;
    }
}
