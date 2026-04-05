using ApplyVault.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Services;

public sealed class EmailDrivenInterviewCalendarSyncService(
    ApplyVaultDbContext dbContext,
    ICalendarEventService calendarEventService,
    ILogger<EmailDrivenInterviewCalendarSyncService> logger) : IEmailDrivenInterviewCalendarSyncService
{
    public async Task SyncAsync(
        AppUserEntity user,
        Guid scrapeResultId,
        CancellationToken cancellationToken = default)
    {
        var connectionIds = await dbContext.ConnectedAccounts
            .AsNoTracking()
            .Where((account) =>
                account.UserId == user.Id &&
                (account.Provider == CalendarProviders.Google ||
                 account.Provider == CalendarProviders.Microsoft))
            .OrderBy((account) => account.Provider)
            .ThenBy((account) => account.Email)
            .Select((account) => account.Id)
            .ToArrayAsync(cancellationToken);

        foreach (var connectionId in connectionIds)
        {
            try
            {
                await calendarEventService.SyncEventAsync(user, scrapeResultId, connectionId, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Automatic calendar sync failed for scrape result {ScrapeResultId} and calendar connection {ConnectedAccountId}.",
                    scrapeResultId,
                    connectionId);
            }
        }
    }
}
