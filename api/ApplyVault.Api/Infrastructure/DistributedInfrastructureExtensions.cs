using ApplyVault.Api.Services;
using ApplyVault.Api.Services.Eures;
using ApplyVault.Api.Services.Jobnet;
using StackExchange.Redis;

namespace ApplyVault.Api.Infrastructure;

public static class DistributedInfrastructureExtensions
{
    public const string RedisConnectionStringName = "Redis";

    public static IServiceCollection AddApplyVaultDistributedInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString(RedisConnectionStringName);

        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddStackExchangeRedisCache((options) =>
            {
                options.Configuration = redisConnectionString;
            });
            services.AddSingleton<IConnectionMultiplexer>((_) =>
                ConnectionMultiplexer.Connect(redisConnectionString));
            services.AddSingleton<IDistributedLockProvider, RedisDistributedLockProvider>();
        }
        else
        {
            services.AddDistributedMemoryCache();
            services.AddSingleton<IDistributedLockProvider, InProcessDistributedLockProvider>();
        }

        services.AddScoped<EuresRankedResultsCache>();
        services.AddScoped<JobnetRankedResultsCache>();
        services.AddScoped<JobnetClassificationCache>();
        services.AddScoped<JobnetSearchPayloadCache>();
        services.AddSingleton<GmailMailSyncWorker>();

        return services;
    }

    public static bool UsesRedisDistributedCache(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration.GetConnectionString(RedisConnectionStringName));
}
