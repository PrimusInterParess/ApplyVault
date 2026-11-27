using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApplyVault.Api.Services.JobRequirements;

internal static class JobRequirementsServiceCollectionExtensions
{
    public static IServiceCollection AddJobRequirements(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JobRequirementsOptions>()
            .Bind(configuration.GetSection(JobRequirementsOptions.SectionName))
            .ValidateDataAnnotations().ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient<IJobRequirementsAiClient, GoogleAiJobRequirementsClient>(http =>
            http.Timeout = Timeout.InfiniteTimeSpan);
        services.AddScoped<JobRequirementsWorkStore>();
        services.AddScoped<JobRequirementsProcessor>();
        services.AddScoped<IJobRequirementsReader, JobRequirementsReader>();
        services.AddExceptionHandler<JobRequirementsConcurrencyExceptionHandler>();
        services.AddHostedService<JobRequirementsBackgroundService>();
        return services;
    }
}
