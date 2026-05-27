using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApplyVault.Api.Services;

public sealed class GitHubApiClient(HttpClient httpClient) : IGitHubApiClient
{
    private const int ReadmeMaxChars = 12_000;

    public async Task<IReadOnlyList<GitHubRepositoryListItem>> ListRepositoriesAsync(
        string accessToken,
        int page,
        int perPage = 100,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(1, page);
        var safePerPage = Math.Clamp(perPage, 1, 100);
        var url =
            $"https://api.github.com/user/repos?sort=updated&affiliation=owner&per_page={safePerPage}&page={safePage}";

        using var request = CreateAuthorizedRequest(HttpMethod.Get, url, accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var payload = JsonSerializer.Deserialize<List<GitHubRepositoryResponse>>(
            await response.Content.ReadAsStringAsync(cancellationToken),
            GitHubJsonSerializerOptions.Default)
            ?? [];

        return payload.Select(MapListItem).ToArray();
    }

    public async Task<GitHubRepositoryDetail> GetRepositoryAsync(
        string accessToken,
        string owner,
        string repo,
        CancellationToken cancellationToken = default)
    {
        var url = $"https://api.github.com/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}";

        using var request = CreateAuthorizedRequest(HttpMethod.Get, url, accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var payload = JsonSerializer.Deserialize<GitHubRepositoryResponse>(
            await response.Content.ReadAsStringAsync(cancellationToken),
            GitHubJsonSerializerOptions.Default)
            ?? throw new InvalidOperationException("GitHub did not return repository details.");

        return MapDetail(payload);
    }

    public async Task<string?> GetReadmeTextAsync(
        string accessToken,
        string owner,
        string repo,
        CancellationToken cancellationToken = default)
    {
        var url = $"https://api.github.com/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}/readme";

        using var request = CreateAuthorizedRequest(HttpMethod.Get, url, accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.raw+json"));

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);

        var readmeText = await response.Content.ReadAsStringAsync(cancellationToken);
        return TruncateReadme(readmeText);
    }

    private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized =>
                new InvalidOperationException("GitHub authorization expired. Reconnect GitHub in Settings."),
            HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests =>
                new InvalidOperationException(
                    $"GitHub rate limit reached. Try again later. ({(int)response.StatusCode})"),
            _ => new InvalidOperationException(
                $"GitHub request failed with status {(int)response.StatusCode}: {body}")
        };
    }

    private static string? TruncateReadme(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        return trimmed.Length <= ReadmeMaxChars ? trimmed : trimmed[..ReadmeMaxChars];
    }

    private static GitHubRepositoryListItem MapListItem(GitHubRepositoryResponse response) =>
        new(
            response.Id,
            response.FullName ?? string.Empty,
            response.Name ?? string.Empty,
            response.Description,
            response.HtmlUrl ?? string.Empty,
            response.Language,
            response.Topics ?? [],
            response.Fork,
            response.Archived,
            response.Private,
            response.StargazersCount,
            response.PushedAt);

    private static GitHubRepositoryDetail MapDetail(GitHubRepositoryResponse response) =>
        new(
            response.Id,
            response.FullName ?? string.Empty,
            response.Name ?? string.Empty,
            response.Description,
            response.HtmlUrl ?? string.Empty,
            response.Language,
            response.Topics ?? [],
            response.Fork,
            response.Archived,
            response.Private,
            response.StargazersCount,
            response.PushedAt,
            response.CreatedAt);

    private sealed class GitHubRepositoryResponse
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("full_name")]
        public string? FullName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("topics")]
        public List<string>? Topics { get; set; }

        [JsonPropertyName("fork")]
        public bool Fork { get; set; }

        [JsonPropertyName("archived")]
        public bool Archived { get; set; }

        [JsonPropertyName("private")]
        public bool Private { get; set; }

        [JsonPropertyName("stargazers_count")]
        public int StargazersCount { get; set; }

        [JsonPropertyName("pushed_at")]
        public DateTimeOffset? PushedAt { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset CreatedAt { get; set; }
    }

}
