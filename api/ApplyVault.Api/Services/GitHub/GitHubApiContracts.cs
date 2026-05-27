using ApplyVault.Api.Data;

namespace ApplyVault.Api.Services;

public sealed record GitHubRepositoryListItem(
    long ExternalRepoId,
    string FullName,
    string Name,
    string? Description,
    string HtmlUrl,
    string? PrimaryLanguage,
    IReadOnlyList<string> Topics,
    bool IsFork,
    bool IsArchived,
    bool IsPrivate,
    int StarCount,
    DateTimeOffset? PushedAt);

public sealed record GitHubRepositoryDetail(
    long ExternalRepoId,
    string FullName,
    string Name,
    string? Description,
    string HtmlUrl,
    string? PrimaryLanguage,
    IReadOnlyList<string> Topics,
    bool IsFork,
    bool IsArchived,
    bool IsPrivate,
    int StarCount,
    DateTimeOffset? PushedAt,
    DateTimeOffset RepoCreatedAt);

public interface IGitHubApiClient
{
    Task<IReadOnlyList<GitHubRepositoryListItem>> ListRepositoriesAsync(
        string accessToken,
        int page,
        int perPage = 100,
        CancellationToken cancellationToken = default);

    Task<GitHubRepositoryDetail> GetRepositoryAsync(
        string accessToken,
        string owner,
        string repo,
        CancellationToken cancellationToken = default);

    Task<string?> GetReadmeTextAsync(
        string accessToken,
        string owner,
        string repo,
        CancellationToken cancellationToken = default);
}

public interface IGitHubAccountResolver
{
    Task<(ConnectedAccountEntity Account, string AccessToken)> ResolveAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default);
}
