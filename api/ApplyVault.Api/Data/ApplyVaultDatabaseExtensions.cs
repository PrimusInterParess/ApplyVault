using ApplyVault.Api.Options;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Data;

public static class ApplyVaultDatabaseExtensions
{
    public static IServiceCollection AddApplyVaultDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<TestingOptions>()
            .Bind(configuration.GetSection(TestingOptions.SectionName));

        var testingOptions = configuration.GetSection(TestingOptions.SectionName).Get<TestingOptions>()
            ?? new TestingOptions();

        services.AddDbContext<ApplyVaultDbContext>((options) =>
        {
            if (testingOptions.UseInMemoryDatabase)
            {
                options.UseInMemoryDatabase(testingOptions.InMemoryDatabaseName);
                return;
            }

            var connectionString = configuration.GetConnectionString("ApplyVault")
                ?? throw new InvalidOperationException("Connection string 'ApplyVault' is not configured.");

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
