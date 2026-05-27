using ApplyVault.Api.Data;
using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public sealed record GitHubAuthorizationState(
    Guid UserId,
    string Provider,
    string? ReturnUrl
);

public sealed record GitHubConnectedIdentity(
    string ProviderUserId,
    string? Email,
    string? DisplayName,
    string AccessToken
);

public interface IGitHubOAuthClient
{
    string BuildAuthorizationUrl(string state);

    Task<GitHubConnectedIdentity> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default);
}

public interface IGitHubConnectionService
{
    Task<IReadOnlyList<ConnectedGitHubAccountDto>> GetConnectionsAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default);

    string BuildAuthorizationUrl(AppUserEntity user, string provider, string? returnUrl = null);

    Task<string> CompleteAuthorizationAsync(
        string provider,
        string code,
        string state,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteConnectionAsync(
        AppUserEntity user,
        Guid connectionId,
        CancellationToken cancellationToken = default);
}
