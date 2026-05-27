using ApplyVault.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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

        services.AddDbContext<ApplyVaultDbContext>((serviceProvider, options) =>
        {
            var runtimeConfiguration = serviceProvider.GetRequiredService<IConfiguration>();
            var testingOptions = runtimeConfiguration.GetSection(TestingOptions.SectionName).Get<TestingOptions>()
                ?? new TestingOptions();

            if (testingOptions.UseInMemoryDatabase)
            {
                options.UseInMemoryDatabase(testingOptions.InMemoryDatabaseName);
                return;
            }

            var connectionString = runtimeConfiguration.GetConnectionString("ApplyVault");
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

        services.AddSingleton<IDatabaseInitializer>((serviceProvider) =>
        {
            var testingOptions = serviceProvider.GetRequiredService<IConfiguration>()
                .GetSection(TestingOptions.SectionName)
                .Get<TestingOptions>()
                ?? new TestingOptions();

            return testingOptions.UseInMemoryDatabase
                ? new InMemoryDatabaseInitializer()
                : new RelationalDatabaseInitializer(serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>());
        });

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
