using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApplyVault.Api.Tests;

public sealed class EmailDrivenInterviewCalendarSyncServiceTests
{
    [Fact]
    public async Task SyncAsync_OnlyUsesCalendarConnections()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var googleAccount = CreateConnectedAccount(user.Id, CalendarProviders.Google, "google-user", "google@example.com");
        var microsoftAccount = CreateConnectedAccount(user.Id, CalendarProviders.Microsoft, "microsoft-user", "microsoft@example.com");
        var gmailAccount = CreateConnectedAccount(user.Id, MailProviders.Gmail, "gmail-user", "gmail@example.com");

        dbContext.Users.Add(user);
        dbContext.ConnectedAccounts.AddRange(googleAccount, microsoftAccount, gmailAccount);
        await dbContext.SaveChangesAsync();

        var calendarEventService = new SpyCalendarEventService();
        var service = new EmailDrivenInterviewCalendarSyncService(
            dbContext,
            calendarEventService,
            NullLogger<EmailDrivenInterviewCalendarSyncService>.Instance);

        var scrapeResultId = Guid.NewGuid();
        await service.SyncAsync(user, scrapeResultId);

        Assert.Equal(2, calendarEventService.Requests.Count);
        Assert.Contains(calendarEventService.Requests, (request) => request.ConnectedAccountId == googleAccount.Id);
        Assert.Contains(calendarEventService.Requests, (request) => request.ConnectedAccountId == microsoftAccount.Id);
        Assert.DoesNotContain(calendarEventService.Requests, (request) => request.ConnectedAccountId == gmailAccount.Id);
        Assert.All(calendarEventService.Requests, (request) => Assert.Equal(scrapeResultId, request.ScrapeResultId));
    }

    [Fact]
    public async Task SyncAsync_WithSqliteProvider_UsesOnlyTranslatableCalendarFilter()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplyVaultDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new ApplyVaultDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var user = CreateUser();
        var googleAccount = CreateConnectedAccount(user.Id, CalendarProviders.Google, "google-user", "google@example.com");
        var microsoftAccount = CreateConnectedAccount(user.Id, CalendarProviders.Microsoft, "microsoft-user", "microsoft@example.com");
        var gmailAccount = CreateConnectedAccount(user.Id, MailProviders.Gmail, "gmail-user", "gmail@example.com");

        dbContext.Users.Add(user);
        dbContext.ConnectedAccounts.AddRange(googleAccount, microsoftAccount, gmailAccount);
        await dbContext.SaveChangesAsync();

        var calendarEventService = new SpyCalendarEventService();
        var service = new EmailDrivenInterviewCalendarSyncService(
            dbContext,
            calendarEventService,
            NullLogger<EmailDrivenInterviewCalendarSyncService>.Instance);

        await service.SyncAsync(user, Guid.NewGuid());

        Assert.Equal(2, calendarEventService.Requests.Count);
        Assert.Contains(calendarEventService.Requests, (request) => request.ConnectedAccountId == googleAccount.Id);
        Assert.Contains(calendarEventService.Requests, (request) => request.ConnectedAccountId == microsoftAccount.Id);
        Assert.DoesNotContain(calendarEventService.Requests, (request) => request.ConnectedAccountId == gmailAccount.Id);
    }

    [Fact]
    public async Task SyncAsync_WhenOneConnectionFails_ContinuesWithRemainingConnections()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var failingAccount = CreateConnectedAccount(user.Id, CalendarProviders.Google, "google-user", "google@example.com");
        var succeedingAccount = CreateConnectedAccount(user.Id, CalendarProviders.Microsoft, "microsoft-user", "microsoft@example.com");

        dbContext.Users.Add(user);
        dbContext.ConnectedAccounts.AddRange(failingAccount, succeedingAccount);
        await dbContext.SaveChangesAsync();

        var calendarEventService = new SpyCalendarEventService
        {
            FailingConnectionIds = [failingAccount.Id]
        };
        var service = new EmailDrivenInterviewCalendarSyncService(
            dbContext,
            calendarEventService,
            NullLogger<EmailDrivenInterviewCalendarSyncService>.Instance);

        var scrapeResultId = Guid.NewGuid();
        await service.SyncAsync(user, scrapeResultId);

        Assert.Equal(2, calendarEventService.Requests.Count);
        Assert.Contains(calendarEventService.Requests, (request) => request.ConnectedAccountId == failingAccount.Id);
        Assert.Contains(calendarEventService.Requests, (request) => request.ConnectedAccountId == succeedingAccount.Id);
    }

    private static ApplyVaultDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplyVaultDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplyVaultDbContext(options);
    }

    private static AppUserEntity CreateUser()
    {
        var utcNow = DateTimeOffset.UtcNow;

        return new AppUserEntity
        {
            Id = Guid.NewGuid(),
            SupabaseUserId = Guid.NewGuid().ToString("N"),
            Email = "user@example.com",
            DisplayName = "Calendar Sync Test User",
            CreatedAt = utcNow,
            LastSeenAt = utcNow
        };
    }

    private static ConnectedAccountEntity CreateConnectedAccount(
        Guid userId,
        string provider,
        string providerUserId,
        string email)
    {
        var utcNow = DateTimeOffset.UtcNow;

        return new ConnectedAccountEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Provider = provider,
            ProviderUserId = providerUserId,
            Email = email,
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = utcNow.AddHours(1),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    private sealed class SpyCalendarEventService : ICalendarEventService
    {
        public List<(Guid UserId, Guid ScrapeResultId, Guid ConnectedAccountId)> Requests { get; } = [];

        public HashSet<Guid> FailingConnectionIds { get; init; } = [];

        public Task<CalendarEventLinkDto> SyncEventAsync(
            AppUserEntity user,
            Guid scrapeResultId,
            Guid connectedAccountId,
            CancellationToken cancellationToken = default)
        {
            Requests.Add((user.Id, scrapeResultId, connectedAccountId));

            if (FailingConnectionIds.Contains(connectedAccountId))
            {
                throw new InvalidOperationException("Calendar sync failed.");
            }

            var utcNow = DateTimeOffset.UtcNow;
            return Task.FromResult(new CalendarEventLinkDto(
                Guid.NewGuid(),
                connectedAccountId,
                "provider",
                Guid.NewGuid().ToString("N"),
                null,
                utcNow,
                utcNow));
        }
    }
}
