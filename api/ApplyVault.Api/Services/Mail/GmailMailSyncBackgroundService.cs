using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public sealed class GmailMailSyncBackgroundService(
    GmailMailSyncWorker syncWorker,
    IOptions<MailIntegrationOptions> options,
    ILogger<GmailMailSyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Mail integration is disabled. Gmail sync worker will stay idle.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await syncWorker.TryRunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Unhandled Gmail sync worker error.");
            }

            var delaySeconds = Math.Max(30, options.Value.PollIntervalSeconds);
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken).ConfigureAwait(false);
        }
    }
}
