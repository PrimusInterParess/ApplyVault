using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.JobRequirements;

internal sealed class JobRequirementsBackgroundService(
    IServiceScopeFactory scopes,
    IOptions<JobRequirementsOptions> options,
    IOptions<GoogleAiOptions> googleOptions,
    ILogger<JobRequirementsBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled || !googleOptions.Value.Enabled)
        {
            logger.LogInformation("Job requirements worker is idle because extraction or Google AI is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<JobRequirementsProcessor>().RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError("Job requirements worker iteration failed with error type {ErrorType}.", exception.GetType().Name);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
