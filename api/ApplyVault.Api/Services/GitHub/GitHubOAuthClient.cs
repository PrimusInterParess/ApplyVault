using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public sealed class GitHubOAuthClient(
    HttpClient httpClient,
    IOptions<GitHubIntegrationOptions> options) : IGitHubOAuthClient
{
    private readonly GitHubIntegrationOptions integrationOptions = options.Value;

    public string BuildAuthorizationUrl(string state)
    {
        var scopes = Uri.EscapeDataString(integrationOptions.Scopes.Trim());

        return "https://github.com/login/oauth/authorize"
            + $"?client_id={Uri.EscapeDataString(integrationOptions.ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(integrationOptions.RedirectUri)}"
            + "&response_type=code"
            + $"&scope={scopes}"
            + $"&state={Uri.EscapeDataString(state)}";
    }

    public async Task<GitHubConnectedIdentity> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = integrationOptions.ClientId,
                ["client_secret"] = integrationOptions.ClientSecret,
                ["code"] = code,
                ["redirect_uri"] = integrationOptions.RedirectUri
            })
        };

        tokenRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var tokenResponse = await httpClient.SendAsync(tokenRequest, cancellationToken);
        await EnsureSuccessAsync(tokenResponse, cancellationToken);

        var tokenPayload = JsonSerializer.Deserialize<GitHubTokenResponse>(
            await tokenResponse.Content.ReadAsStringAsync(cancellationToken),
            GitHubJsonSerializerOptions.Default)
            ?? throw new InvalidOperationException("GitHub did not return an OAuth token response.");

        if (string.IsNullOrWhiteSpace(tokenPayload.AccessToken))
        {
            throw new InvalidOperationException("GitHub did not return an access token.");
        }

        using var userRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.github.com/user");
        userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenPayload.AccessToken);
        userRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        userRequest.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using var userResponse = await httpClient.SendAsync(userRequest, cancellationToken);
        await EnsureSuccessAsync(userResponse, cancellationToken);

        var userPayload = JsonSerializer.Deserialize<GitHubUserResponse>(
            await userResponse.Content.ReadAsStringAsync(cancellationToken),
            GitHubJsonSerializerOptions.Default)
            ?? throw new InvalidOperationException("GitHub did not return a user profile.");

        var email = userPayload.Email;

        if (string.IsNullOrWhiteSpace(email))
        {
            email = await TryGetPrimaryEmailAsync(tokenPayload.AccessToken, cancellationToken);
        }

        return new GitHubConnectedIdentity(
            userPayload.Id.ToString(CultureInfo.InvariantCulture),
            email,
            string.IsNullOrWhiteSpace(userPayload.Name) ? userPayload.Login : userPayload.Name,
            tokenPayload.AccessToken);
    }

    private async Task<string?> TryGetPrimaryEmailAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var emailRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.github.com/user/emails");
        emailRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        emailRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        emailRequest.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using var emailResponse = await httpClient.SendAsync(emailRequest, cancellationToken);

        if (!emailResponse.IsSuccessStatusCode)
        {
            return null;
        }

        var emails = JsonSerializer.Deserialize<List<GitHubEmailResponse>>(
            await emailResponse.Content.ReadAsStringAsync(cancellationToken),
            GitHubJsonSerializerOptions.Default);

        return emails?
            .FirstOrDefault((entry) => entry.Primary)?.Email
            ?? emails?.FirstOrDefault()?.Email;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"GitHub request failed with status {(int)response.StatusCode}: {body}");
    }

    private sealed class GitHubTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }

    private sealed class GitHubUserResponse
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("login")]
        public string Login { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }
    }

    private sealed class GitHubEmailResponse
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("primary")]
        public bool Primary { get; set; }
    }

    private static class GitHubJsonSerializerOptions
    {
        public static readonly JsonSerializerOptions Default = new()
        {
            PropertyNameCaseInsensitive = true
        };
    }
}
