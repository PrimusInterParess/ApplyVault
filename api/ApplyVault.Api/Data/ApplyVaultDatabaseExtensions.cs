using ApplyVault.Api.Options;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Data;

public static class ApplyVaultDatabaseExtensions
{
    public static IServiceCollection AddApplyVaultDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services
            .AddOptions<TestingOptions>()
            .Bind(configuration.GetSection(TestingOptions.SectionName));

        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName));

        var testingOptions = configuration.GetSection(TestingOptions.SectionName).Get<TestingOptions>()
            ?? new TestingOptions();

        services.AddDbContext<ApplyVaultDbContext>((options) =>
        {
            if (testingOptions.UseInMemoryDatabase)
            {
                options.UseInMemoryDatabase(testingOptions.InMemoryDatabaseName);
                return;
            }

            var connectionString = configuration.GetConnectionString("ApplyVault");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Connection string 'ApplyVault' is not configured. Set ConnectionStrings__ApplyVault or appsettings.{Environment}.json.");
            }

            if (!environment.IsDevelopment() && connectionString.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Connection string 'ApplyVault' must not use LocalDB when ASPNETCORE_ENVIRONMENT is not Development.");
            }

            options.UseSqlServer(connectionString);
        });

        if (testingOptions.UseInMemoryDatabase)
        {
            services.AddSingleton<IDatabaseInitializer, InMemoryDatabaseInitializer>();
        }
        else
        {
            services.AddSingleton<IDatabaseInitializer, RelationalDatabaseInitializer>();
        }

        return services;
    }

    public static void InitializeApplyVaultDatabase(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplyVaultDbContext>();
        var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
        initializer.Initialize(dbContext);
    }
}
