using System.Text.Json;
using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public interface IGitHubProjectSummaryService
{
    Task<IReadOnlyList<GitHubRepositoryListItemDto>> ListRepositoriesAsync(
        AppUserEntity user,
        int page,
        int perPage,
        CancellationToken cancellationToken = default);

    Task<GitHubRepositoryReadmeDto> GetRepositoryReadmeAsync(
        AppUserEntity user,
        string fullName,
        CancellationToken cancellationToken = default);

    Task<CvProjectSummaryDto> GenerateAsync(
        AppUserEntity user,
        string fullName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CvProjectSummaryDto>> ListSummariesAsync(
        AppUserEntity user,
        int page,
        int perPage,
        CancellationToken cancellationToken = default);

    Task<CvProjectSummaryDto?> GetSummaryAsync(
        AppUserEntity user,
        Guid summaryId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteSummaryAsync(
        AppUserEntity user,
        Guid summaryId,
        CancellationToken cancellationToken = default);
}

public sealed class GitHubProjectSummaryService(
    ApplyVaultDbContext dbContext,
    IGitHubAccountResolver gitHubAccountResolver,
    IGitHubApiClient gitHubApiClient,
    IGitHubProjectAiClient gitHubProjectAiClient,
    IOptions<GoogleAiOptions> googleAiOptions) : IGitHubProjectSummaryService
{
    public async Task<IReadOnlyList<GitHubRepositoryListItemDto>> ListRepositoriesAsync(
        AppUserEntity user,
        int page,
        int perPage,
        CancellationToken cancellationToken = default)
    {
        var (_, accessToken) = await gitHubAccountResolver.ResolveAsync(user, cancellationToken);
        var repositories = await gitHubApiClient.ListRepositoriesAsync(
            accessToken,
            page,
            perPage,
            cancellationToken);

        return repositories.Select(MapRepositoryListItem).ToArray();
    }

    public async Task<GitHubRepositoryReadmeDto> GetRepositoryReadmeAsync(
        AppUserEntity user,
        string fullName,
        CancellationToken cancellationToken = default)
    {
        var (owner, repo) = ParseFullName(fullName);
        var (_, accessToken) = await gitHubAccountResolver.ResolveAsync(user, cancellationToken);
        var readmeText = await gitHubApiClient.GetReadmeTextAsync(accessToken, owner, repo, cancellationToken);

        return new GitHubRepositoryReadmeDto(readmeText);
    }

    public async Task<CvProjectSummaryDto> GenerateAsync(
        AppUserEntity user,
        string fullName,
        CancellationToken cancellationToken = default)
    {
        if (!googleAiOptions.Value.Enabled)
        {
            throw new InvalidOperationException("Google AI is disabled. Enable GoogleAi:Enabled to generate project summaries.");
        }

        var (owner, repo) = ParseFullName(fullName);
        var (_, accessToken) = await gitHubAccountResolver.ResolveAsync(user, cancellationToken);

        var repository = await gitHubApiClient.GetRepositoryAsync(accessToken, owner, repo, cancellationToken);
        var readmeText = await gitHubApiClient.GetReadmeTextAsync(accessToken, owner, repo, cancellationToken);

        if (!GitHubProjectSummaryEligibility.HasSufficientSummaryData(
                readmeText,
                repository.Description,
                repository.PrimaryLanguage,
                repository.Topics))
        {
            throw new InvalidOperationException(GitHubProjectSummaryEligibility.InsufficientDataMessage);
        }

        var aiResult = await gitHubProjectAiClient.GenerateAsync(
            new GitHubProjectAiInput(
                repository.FullName,
                repository.Name,
                repository.Description,
                repository.PrimaryLanguage,
                repository.Topics,
                repository.StarCount,
                repository.PushedAt,
                readmeText),
            cancellationToken);

        var utcNow = DateTimeOffset.UtcNow;
        var bulletsJson = JsonSerializer.Serialize(aiResult.Bullets);
        var topicsJson = JsonSerializer.Serialize(repository.Topics);

        var entity = await dbContext.UserCvProjectSummaries.SingleOrDefaultAsync(
            (candidate) =>
                candidate.UserId == user.Id &&
                candidate.ExternalRepoId == repository.ExternalRepoId,
            cancellationToken);

        if (entity is null)
        {
            entity = new UserCvProjectSummaryEntity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                ExternalRepoId = repository.ExternalRepoId,
                FullName = repository.FullName,
                HtmlUrl = repository.HtmlUrl,
                PrimaryLanguage = repository.PrimaryLanguage,
                Topics = topicsJson,
                CvTitle = aiResult.Title.Trim(),
                CvSummary = aiResult.Summary.Trim(),
                CvBullets = bulletsJson,
                TechStack = aiResult.TechStack.Trim(),
                GeneratedAt = utcNow,
                UpdatedAt = utcNow
            };

            await dbContext.UserCvProjectSummaries.AddAsync(entity, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return MapSummary(entity);
        }

        entity.FullName = repository.FullName;
        entity.HtmlUrl = repository.HtmlUrl;
        entity.PrimaryLanguage = repository.PrimaryLanguage;
        entity.Topics = topicsJson;
        entity.CvTitle = aiResult.Title.Trim();
        entity.CvSummary = aiResult.Summary.Trim();
        entity.CvBullets = bulletsJson;
        entity.TechStack = aiResult.TechStack.Trim();
        entity.UpdatedAt = utcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return MapSummary(entity);
    }

    public async Task<IReadOnlyList<CvProjectSummaryDto>> ListSummariesAsync(
        AppUserEntity user,
        int page,
        int perPage,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = Math.Max(page, 1);
        var normalizedPerPage = Math.Clamp(perPage, 1, 100);

        var summaries = await dbContext.UserCvProjectSummaries
            .AsNoTracking()
            .Where((summary) => summary.UserId == user.Id)
            .OrderByDescending((summary) => summary.UpdatedAt)
            .Skip((normalizedPage - 1) * normalizedPerPage)
            .Take(normalizedPerPage)
            .ToArrayAsync(cancellationToken);

        return summaries.Select(MapSummary).ToArray();
    }

    public async Task<CvProjectSummaryDto?> GetSummaryAsync(
        AppUserEntity user,
        Guid summaryId,
        CancellationToken cancellationToken = default)
    {
        var summary = await dbContext.UserCvProjectSummaries
            .AsNoTracking()
            .SingleOrDefaultAsync(
                (candidate) => candidate.UserId == user.Id && candidate.Id == summaryId,
                cancellationToken);

        return summary is null ? null : MapSummary(summary);
    }

    public async Task<bool> DeleteSummaryAsync(
        AppUserEntity user,
        Guid summaryId,
        CancellationToken cancellationToken = default)
    {
        var summary = await dbContext.UserCvProjectSummaries.SingleOrDefaultAsync(
            (candidate) => candidate.UserId == user.Id && candidate.Id == summaryId,
            cancellationToken);

        if (summary is null)
        {
            return false;
        }

        dbContext.UserCvProjectSummaries.Remove(summary);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static (string Owner, string Repo) ParseFullName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new InvalidOperationException("Repository full name is required.");
        }

        var parts = fullName.Trim().Split('/', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2)
        {
            throw new InvalidOperationException("Repository full name must be in owner/repo format.");
        }

        return (parts[0], parts[1]);
    }

    private static GitHubRepositoryListItemDto MapRepositoryListItem(GitHubRepositoryListItem repository) =>
        new(
            repository.ExternalRepoId,
            repository.FullName,
            repository.Name,
            repository.Description,
            repository.HtmlUrl,
            repository.PrimaryLanguage,
            repository.Topics,
            repository.IsFork,
            repository.IsArchived,
            repository.IsPrivate,
            repository.StarCount,
            repository.PushedAt);

    private static CvProjectSummaryDto MapSummary(UserCvProjectSummaryEntity entity)
    {
        var topics = DeserializeStringArray(entity.Topics);
        var bullets = DeserializeStringArray(entity.CvBullets);

        return new CvProjectSummaryDto(
            entity.Id,
            entity.ExternalRepoId,
            entity.FullName,
            entity.HtmlUrl,
            entity.PrimaryLanguage,
            topics,
            entity.CvTitle,
            entity.CvSummary,
            bullets,
            entity.TechStack,
            entity.GeneratedAt,
            entity.UpdatedAt);
    }

    private static IReadOnlyList<string> DeserializeStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<string>>(json) ?? [];
    }
}
