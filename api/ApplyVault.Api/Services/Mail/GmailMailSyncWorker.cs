using ApplyVault.Api.Infrastructure;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public sealed class GmailMailSyncWorker(
    IServiceScopeFactory serviceScopeFactory,
    IDistributedLockProvider lockProvider,
    IOptions<MailIntegrationOptions> options,
    ILogger<GmailMailSyncWorker> logger)
{
    private const string LockResourceName = "applyvault:gmail-sync";

    public async Task<bool> TryRunOnceAsync(CancellationToken cancellationToken = default)
    {
        var lockTtl = ComputeLockTtl(options.Value);
        await using var handle = await lockProvider
            .TryAcquireAsync(LockResourceName, lockTtl, cancellationToken)
            .ConfigureAwait(false);

        if (handle is null)
        {
            logger.LogDebug(
                "Gmail sync skipped; another API instance holds the distributed lock.");
            return false;
        }

        using var scope = serviceScopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<IMailSyncProcessor>();
        await processor.SyncAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    internal static TimeSpan ComputeLockTtl(MailIntegrationOptions mailOptions)
    {
        var pollIntervalSeconds = Math.Max(30, mailOptions.PollIntervalSeconds);
        return TimeSpan.FromSeconds(pollIntervalSeconds + 30);
    }
}
