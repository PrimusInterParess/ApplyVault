using ApplyVault.Api.Data;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Tests;

public sealed class MailSyncProcessorTests
{
    [Fact]
    public async Task SyncAsync_ExpiredAccessToken_RefreshesAndProcessesMessages()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var account = new ConnectedAccountEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            Provider = MailProviders.Gmail,
            ProviderUserId = "gmail-user-1",
            Email = "before@example.com",
            DisplayName = "Before User",
            AccessToken = "expired-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            SyncStatus = MailConnectionSyncStatuses.Connected,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-30)
        };

        dbContext.Users.Add(user);
        dbContext.ConnectedAccounts.Add(account);
        await dbContext.SaveChangesAsync();

        var gmailClient = new FakeGmailMailClient
        {
            RefreshResult = new MailConnectedIdentity(
                "gmail-user-1",
                "after@example.com",
                "After User",
                "fresh-token",
                "fresh-refresh-token",
                DateTimeOffset.UtcNow.AddHours(1)),
            Messages =
            [
                new GmailMessage(
                    "message-1",
                    "history-1",
                    "Subject",
                    "jobs@example.com",
                    "Snippet",
                    "Body",
                    DateTimeOffset.UtcNow.AddMinutes(-5))
            ]
        };
        var updater = new SpyEmailDrivenJobUpdateService();
        var processor = new MailSyncProcessor(
            dbContext,
            gmailClient,
            updater,
            Microsoft.Extensions.Options.Options.Create(new MailIntegrationOptions
            {
                Enabled = true,
                InitialLookbackHours = 24,
                MaxMessagesPerSync = 10
            }),
            NullLogger<MailSyncProcessor>.Instance);

        await processor.SyncAsync();

        Assert.Single(gmailClient.RefreshRequests);
        Assert.Equal("refresh-token", gmailClient.RefreshRequests[0].RefreshToken);
        Assert.Equal("fresh-token", gmailClient.LastAccessTokenUsedForFetch);
        Assert.Single(updater.AppliedMessages);

        var updatedAccount = await dbContext.ConnectedAccounts.SingleAsync();
        Assert.Equal("fresh-token", updatedAccount.AccessToken);
        Assert.Equal("fresh-refresh-token", updatedAccount.RefreshToken);
        Assert.Equal(MailConnectionSyncStatuses.Connected, updatedAccount.SyncStatus);
        Assert.Equal("history-1", updatedAccount.LastHistoryId);
        Assert.NotNull(updatedAccount.LastSyncedAt);
    }

    [Fact]
    public async Task SyncAsync_ExpiredAccessTokenWithoutRefreshToken_MarksAccountForReconnect()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var account = new ConnectedAccountEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            Provider = MailProviders.Gmail,
            ProviderUserId = "gmail-user-2",
            AccessToken = "expired-token",
            RefreshToken = null,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            SyncStatus = MailConnectionSyncStatuses.Connected,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-30),
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-30)
        };

        dbContext.Users.Add(user);
        dbContext.ConnectedAccounts.Add(account);
        await dbContext.SaveChangesAsync();

        var gmailClient = new FakeGmailMailClient();
        var updater = new SpyEmailDrivenJobUpdateService();
        var processor = new MailSyncProcessor(
            dbContext,
            gmailClient,
            updater,
            Microsoft.Extensions.Options.Options.Create(new MailIntegrationOptions
            {
                Enabled = true,
                InitialLookbackHours = 24,
                MaxMessagesPerSync = 10
            }),
            NullLogger<MailSyncProcessor>.Instance);

        await processor.SyncAsync();

        var updatedAccount = await dbContext.ConnectedAccounts.SingleAsync();
        Assert.Equal(MailConnectionSyncStatuses.NeedsReconnect, updatedAccount.SyncStatus);
        Assert.Equal("The Gmail connection needs to be reconnected.", updatedAccount.LastSyncError);
        Assert.Empty(gmailClient.RefreshRequests);
        Assert.Empty(updater.AppliedMessages);
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
            DisplayName = "Processor Test User",
            CreatedAt = utcNow,
            LastSeenAt = utcNow
        };
    }

    private sealed class FakeGmailMailClient : IGmailMailClient
    {
        public List<MailRefreshRequest> RefreshRequests { get; } = [];

        public MailConnectedIdentity RefreshResult { get; set; } = new(
            "provider-user-id",
            "user@example.com",
            "Test User",
            "access-token",
            "refresh-token",
            DateTimeOffset.UtcNow.AddHours(1));

        public IReadOnlyList<GmailMessage> Messages { get; set; } = [];

        public string? LastAccessTokenUsedForFetch { get; private set; }

        public string BuildAuthorizationUrl(string state) => state;

        public Task<MailConnectedIdentity> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult(RefreshResult);

        public Task<MailConnectedIdentity> RefreshAsync(MailRefreshRequest request, CancellationToken cancellationToken = default)
        {
            RefreshRequests.Add(request);
            return Task.FromResult(RefreshResult);
        }

        public Task<IReadOnlyList<GmailMessage>> GetRecentMessagesAsync(
            string accessToken,
            DateTimeOffset sinceUtc,
            int maxResults,
            CancellationToken cancellationToken = default)
        {
            LastAccessTokenUsedForFetch = accessToken;
            return Task.FromResult(Messages);
        }
    }

    private sealed class SpyEmailDrivenJobUpdateService : IEmailDrivenJobUpdateService
    {
        public List<GmailMessage> AppliedMessages { get; } = [];

        public Task<bool> TryApplyAsync(
            AppUserEntity user,
            GmailMessage message,
            CancellationToken cancellationToken = default)
        {
            AppliedMessages.Add(message);
            return Task.FromResult(true);
        }
    }
}
