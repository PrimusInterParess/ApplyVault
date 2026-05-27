using System.Text.Json;
using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public sealed class GitHubConnectionService(
    ApplyVaultDbContext dbContext,
    IGitHubOAuthClient gitHubOAuthClient,
    IOptions<GitHubIntegrationOptions> integrationOptions,
    IDataProtectionProvider dataProtectionProvider) : IGitHubConnectionService
{
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector("ApplyVault.GitHubOAuthState");

    public async Task<IReadOnlyList<ConnectedGitHubAccountDto>> GetConnectionsAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default)
    {
        var accounts = await dbContext.ConnectedAccounts
            .AsNoTracking()
            .Where((account) => account.UserId == user.Id && account.Provider == GitHubProviders.GitHub)
            .OrderBy((account) => account.DisplayName)
            .ThenBy((account) => account.CreatedAt)
            .ToArrayAsync(cancellationToken);

        return accounts.Select(MapToDto).ToArray();
    }

    public string BuildAuthorizationUrl(AppUserEntity user, string provider, string? returnUrl = null)
    {
        EnsureConfigured();

        if (!GitHubProviders.IsSupported(provider))
        {
            throw new InvalidOperationException($"The GitHub provider '{provider}' is not supported.");
        }

        var payload = JsonSerializer.Serialize(new GitHubAuthorizationState(user.Id, provider, returnUrl));
        var protectedState = protector.Protect(payload);
        return gitHubOAuthClient.BuildAuthorizationUrl(protectedState);
    }

    public async Task<string> CompleteAuthorizationAsync(
        string provider,
        string code,
        string state,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        if (!GitHubProviders.IsSupported(provider))
        {
            throw new InvalidOperationException($"The GitHub provider '{provider}' is not supported.");
        }

        var authorizationState = JsonSerializer.Deserialize<GitHubAuthorizationState>(protector.Unprotect(state))
            ?? throw new InvalidOperationException("The GitHub authorization state is invalid.");

        if (!string.Equals(authorizationState.Provider, provider, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The GitHub authorization provider does not match the original request.");
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(
            (candidate) => candidate.Id == authorizationState.UserId,
            cancellationToken)
            ?? throw new InvalidOperationException("The user that started the GitHub authorization flow no longer exists.");

        var connectedIdentity = await gitHubOAuthClient.ExchangeCodeAsync(code, cancellationToken);
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
        account.SyncStatus = GitHubConnectionSyncStatuses.Connected;
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
            (candidate) =>
                candidate.UserId == user.Id &&
                candidate.Id == connectionId &&
                candidate.Provider == GitHubProviders.GitHub,
            cancellationToken);

        if (account is null)
        {
            return false;
        }

        var summaries = await dbContext.UserCvProjectSummaries
            .Where((summary) => summary.UserId == user.Id)
            .ToArrayAsync(cancellationToken);

        if (summaries.Length > 0)
        {
            dbContext.UserCvProjectSummaries.RemoveRange(summaries);
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
            throw new InvalidOperationException("GitHub integration is not enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.ClientId) ||
            string.IsNullOrWhiteSpace(options.ClientSecret) ||
            string.IsNullOrWhiteSpace(options.RedirectUri))
        {
            throw new InvalidOperationException("GitHub OAuth is not configured yet.");
        }
    }

    private static ConnectedGitHubAccountDto MapToDto(ConnectedAccountEntity account) =>
        new(
            account.Id,
            account.Provider,
            account.ProviderUserId,
            account.Email,
            account.DisplayName,
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
