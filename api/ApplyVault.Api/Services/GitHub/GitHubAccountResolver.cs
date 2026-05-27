using ApplyVault.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Services;

public sealed class GitHubAccountResolver(ApplyVaultDbContext dbContext) : IGitHubAccountResolver
{
    public async Task<(ConnectedAccountEntity Account, string AccessToken)> ResolveAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default)
    {
        var account = await dbContext.ConnectedAccounts
            .SingleOrDefaultAsync(
                (candidate) =>
                    candidate.UserId == user.Id &&
                    candidate.Provider == GitHubProviders.GitHub,
                cancellationToken)
            ?? throw new InvalidOperationException("Connect GitHub in Settings before using CV projects.");

        if (string.IsNullOrWhiteSpace(account.AccessToken))
        {
            throw new InvalidOperationException("GitHub access token is missing. Reconnect GitHub in Settings.");
        }

        return (account, account.AccessToken);
    }
}
