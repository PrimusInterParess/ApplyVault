namespace ApplyVault.Api.Services;

public sealed record GitHubProjectAiInput(
    string FullName,
    string Name,
    string? Description,
    string? PrimaryLanguage,
    IReadOnlyList<string> Topics,
    int StarCount,
    DateTimeOffset? PushedAt,
    string? ReadmeText);

public sealed record CvProjectSummaryResult(
    string Title,
    string Summary,
    IReadOnlyList<string> Bullets,
    string TechStack);

public interface IGitHubProjectAiClient
{
    Task<CvProjectSummaryResult> GenerateAsync(
        GitHubProjectAiInput input,
        CancellationToken cancellationToken = default);
}
