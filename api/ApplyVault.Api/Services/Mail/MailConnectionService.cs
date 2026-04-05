using System.Text.Json;
using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public sealed class MailConnectionService(
    ApplyVaultDbContext dbContext,
    IGmailMailClient gmailMailClient,
    IOptions<MailIntegrationOptions> integrationOptions,
    IDataProtectionProvider dataProtectionProvider) : IMailConnectionService
{
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector("ApplyVault.MailOAuthState");

    public async Task<IReadOnlyList<ConnectedMailAccountDto>> GetConnectionsAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default)
    {
        var accounts = await dbContext.ConnectedAccounts
            .AsNoTracking()
            .Where((account) => account.UserId == user.Id && account.Provider == MailProviders.Gmail)
            .OrderBy((account) => account.Email)
            .ThenBy((account) => account.CreatedAt)
            .ToArrayAsync(cancellationToken);

        return accounts.Select(MapToDto).ToArray();
    }

    public string BuildAuthorizationUrl(AppUserEntity user, string provider, string? returnUrl = null)
    {
        EnsureConfigured();

        if (!MailProviders.IsSupported(provider))
        {
            throw new InvalidOperationException($"The mail provider '{provider}' is not supported.");
        }

        var payload = JsonSerializer.Serialize(new MailAuthorizationState(user.Id, provider, returnUrl));
        var protectedState = protector.Protect(payload);
        return gmailMailClient.BuildAuthorizationUrl(protectedState);
    }

    public async Task<string> CompleteAuthorizationAsync(
        string provider,
        string code,
        string state,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        if (!MailProviders.IsSupported(provider))
        {
            throw new InvalidOperationException($"The mail provider '{provider}' is not supported.");
        }

        var authorizationState = JsonSerializer.Deserialize<MailAuthorizationState>(protector.Unprotect(state))
            ?? throw new InvalidOperationException("The mail authorization state is invalid.");

        if (!string.Equals(authorizationState.Provider, provider, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The mail authorization provider does not match the original request.");
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(
            (candidate) => candidate.Id == authorizationState.UserId,
            cancellationToken)
            ?? throw new InvalidOperationException("The user that started the mail authorization flow no longer exists.");

        var connectedIdentity = await gmailMailClient.ExchangeCodeAsync(code, cancellationToken);
        var utcNow = DateTimeOffset.UtcNow;
        var account = await dbContext.ConnectedAccounts.SingleOrDefaultAsync(
            (candidate) =>
                candidate.UserId == user.Id &&
                candidate.Provider == provider &&
                candidate.ProviderUserId == connectedIdentity.ProviderUserId,
            cancellationToken);

        if (account is null)
        {
            account = new ConnectedAccountEntity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Provider = provider,
                ProviderUserId = connectedIdentity.ProviderUserId,
                AccessToken = connectedIdentity.AccessToken,
                CreatedAt = utcNow
            };

            await dbContext.ConnectedAccounts.AddAsync(account, cancellationToken);
        }

        account.Email = connectedIdentity.Email;
        account.DisplayName = connectedIdentity.DisplayName;
        account.AccessToken = connectedIdentity.AccessToken;
        account.RefreshToken = connectedIdentity.RefreshToken ?? account.RefreshToken;
        account.ExpiresAt = connectedIdentity.ExpiresAt;
        account.SyncStatus = MailConnectionSyncStatuses.Connected;
        account.LastSyncError = null;
        account.UpdatedAt = utcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return AppendQueryString(
            string.IsNullOrWhiteSpace(authorizationState.ReturnUrl)
                ? integrationOptions.Value.PostConnectRedirectUrl
                : authorizationState.ReturnUrl!,
            new Dictionary<string, string?>
            {
                ["provider"] = provider,
                ["success"] = "true"
            });
    }

    public async Task<bool> DeleteConnectionAsync(
        AppUserEntity user,
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        var account = await dbContext.ConnectedAccounts.SingleOrDefaultAsync(
            (candidate) => candidate.UserId == user.Id && candidate.Id == connectionId && candidate.Provider == MailProviders.Gmail,
            cancellationToken);

        if (account is null)
        {
            return false;
        }

        dbContext.ConnectedAccounts.Remove(account);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private void EnsureConfigured()
    {
        var options = integrationOptions.Value;

        if (!options.Enabled)
        {
            throw new InvalidOperationException("Mail integration is not enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.Gmail.ClientId) ||
            string.IsNullOrWhiteSpace(options.Gmail.ClientSecret) ||
            string.IsNullOrWhiteSpace(options.Gmail.RedirectUri))
        {
            throw new InvalidOperationException("Gmail OAuth is not configured yet.");
        }
    }

    private static ConnectedMailAccountDto MapToDto(ConnectedAccountEntity account) =>
        new(
            account.Id,
            account.Provider,
            account.ProviderUserId,
            account.Email,
            account.DisplayName,
            account.ExpiresAt,
            string.IsNullOrWhiteSpace(account.SyncStatus) ? MailConnectionSyncStatuses.Connected : account.SyncStatus!,
            account.LastSyncedAt,
            account.LastSyncError,
            account.LastHistoryId,
            account.CreatedAt,
            account.UpdatedAt);

    private static string AppendQueryString(string baseUrl, IReadOnlyDictionary<string, string?> values)
    {
        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var query = string.Join(
            "&",
            values
                .Where((pair) => !string.IsNullOrWhiteSpace(pair.Value))
                .Select((pair) => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));

        return string.IsNullOrWhiteSpace(query) ? baseUrl : $"{baseUrl}{separator}{query}";
    }
}
