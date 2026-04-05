using ApplyVault.Api.Data;
using ApplyVault.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public sealed class MailSyncProcessor(
    ApplyVaultDbContext dbContext,
    IGmailMailClient gmailMailClient,
    IEmailDrivenJobUpdateService emailDrivenJobUpdateService,
    IOptions<MailIntegrationOptions> options,
    ILogger<MailSyncProcessor> logger) : IMailSyncProcessor
{
    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        var integrationOptions = options.Value;

        if (!integrationOptions.Enabled)
        {
            return;
        }

        var accounts = await dbContext.ConnectedAccounts
            .Include((account) => account.User)
            .Where((account) => account.Provider == MailProviders.Gmail)
            .OrderBy((account) => account.CreatedAt)
            .ToArrayAsync(cancellationToken);

        foreach (var account in accounts)
        {
            if (account.User is null)
            {
                continue;
            }

            try
            {
                await SyncAccountAsync(account, integrationOptions, cancellationToken);
            }
            catch (Exception exception)
            {
                account.SyncStatus = MailConnectionSyncStatuses.Error;
                account.LastSyncError = exception.Message;
                account.UpdatedAt = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogWarning(exception, "Mail sync failed for account {AccountId}.", account.Id);
            }
        }
    }

    private async Task SyncAccountAsync(
        ConnectedAccountEntity account,
        MailIntegrationOptions integrationOptions,
        CancellationToken cancellationToken)
    {
        account.SyncStatus = MailConnectionSyncStatuses.Syncing;
        account.LastSyncError = null;
        account.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        account = await EnsureFreshAccessTokenAsync(account, cancellationToken);

        if (string.Equals(account.SyncStatus, MailConnectionSyncStatuses.NeedsReconnect, StringComparison.Ordinal))
        {
            return;
        }

        var sinceUtc = account.LastSyncedAt ?? DateTimeOffset.UtcNow.AddHours(-Math.Max(1, integrationOptions.InitialLookbackHours));
        var messages = await gmailMailClient.GetRecentMessagesAsync(
            account.AccessToken,
            sinceUtc,
            integrationOptions.MaxMessagesPerSync,
            cancellationToken);

        foreach (var message in messages)
        {
            await emailDrivenJobUpdateService.TryApplyAsync(account.User!, message, cancellationToken);
        }

        var latestMessage = messages.LastOrDefault();
        account.LastSyncedAt = DateTimeOffset.UtcNow;
        account.LastHistoryId = latestMessage?.HistoryId ?? account.LastHistoryId;
        account.SyncStatus = MailConnectionSyncStatuses.Connected;
        account.LastSyncError = null;
        account.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<ConnectedAccountEntity> EnsureFreshAccessTokenAsync(
        ConnectedAccountEntity account,
        CancellationToken cancellationToken)
    {
        if (account.ExpiresAt is null || account.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return account;
        }

        if (string.IsNullOrWhiteSpace(account.RefreshToken))
        {
            account.SyncStatus = MailConnectionSyncStatuses.NeedsReconnect;
            account.LastSyncError = "The Gmail connection needs to be reconnected.";
            account.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return account;
        }

        try
        {
            var refreshed = await gmailMailClient.RefreshAsync(
                new MailRefreshRequest(
                    account.RefreshToken,
                    account.ProviderUserId,
                    account.Email,
                    account.DisplayName),
                cancellationToken);
            account.AccessToken = refreshed.AccessToken;
            account.RefreshToken = refreshed.RefreshToken ?? account.RefreshToken;
            account.ExpiresAt = refreshed.ExpiresAt;
            account.Email = refreshed.Email ?? account.Email;
            account.DisplayName = refreshed.DisplayName ?? account.DisplayName;
            account.SyncStatus = MailConnectionSyncStatuses.Connected;
            account.LastSyncError = null;
            account.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return account;
        }
        catch (Exception exception)
        {
            account.SyncStatus = MailConnectionSyncStatuses.NeedsReconnect;
            account.LastSyncError = exception.Message;
            account.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }
    }
}
