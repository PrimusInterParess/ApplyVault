using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public sealed class GmailMailClient(
    HttpClient httpClient,
    IOptions<MailIntegrationOptions> options) : IGmailMailClient
{
    private const string GmailScope = "https://www.googleapis.com/auth/gmail.readonly";
    private readonly GmailMailOptions providerOptions = options.Value.Gmail;

    public string BuildAuthorizationUrl(string state)
    {
        var scopes = Uri.EscapeDataString($"{GmailScope} openid email profile");
        return "https://accounts.google.com/o/oauth2/v2/auth"
            + $"?client_id={Uri.EscapeDataString(providerOptions.ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(providerOptions.RedirectUri)}"
            + "&response_type=code"
            + $"&scope={scopes}"
            + "&access_type=offline"
            + "&prompt=consent"
            + "&include_granted_scopes=true"
            + $"&state={Uri.EscapeDataString(state)}";
    }

    public async Task<MailConnectedIdentity> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = providerOptions.ClientId,
                ["client_secret"] = providerOptions.ClientSecret,
                ["code"] = code,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = providerOptions.RedirectUri
            }),
            cancellationToken);

        return await BuildIdentityAsync(response, cancellationToken);
    }

    public async Task<MailConnectedIdentity> RefreshAsync(
        MailRefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = providerOptions.ClientId,
                ["client_secret"] = providerOptions.ClientSecret,
                ["refresh_token"] = request.RefreshToken,
                ["grant_type"] = "refresh_token"
            }),
            cancellationToken);

        return await BuildIdentityAsync(
            response,
            cancellationToken,
            request.ProviderUserId,
            request.Email,
            request.DisplayName);
    }

    public async Task<IReadOnlyList<GmailMessage>> GetRecentMessagesAsync(
        string accessToken,
        DateTimeOffset sinceUtc,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        var query = $"after:{sinceUtc.ToUnixTimeSeconds()}";
        var url = $"https://gmail.googleapis.com/gmail/v1/users/me/messages?maxResults={Math.Clamp(maxResults, 1, 100)}&q={Uri.EscapeDataString(query)}";
        using var request = CreateAuthenticatedRequest(HttpMethod.Get, url, accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await GmailHttpResponse.EnsureSuccessAsync(response);

        var listResponse = JsonSerializer.Deserialize<GmailListMessagesResponse>(
            await response.Content.ReadAsStringAsync(cancellationToken),
            GmailJsonSerializerOptions.Default)
            ?? new GmailListMessagesResponse();

        if (listResponse.Messages is null || listResponse.Messages.Count == 0)
        {
            return [];
        }

        var messages = new List<GmailMessage>(listResponse.Messages.Count);

        foreach (var summary in listResponse.Messages)
        {
            var message = await GetMessageAsync(accessToken, summary.Id, cancellationToken);

            if (message is not null && message.ReceivedAt >= sinceUtc)
            {
                messages.Add(message);
            }
        }

        return messages
            .OrderBy((message) => message.ReceivedAt)
            .ToArray();
    }

    private async Task<GmailMessage?> GetMessageAsync(
        string accessToken,
        string messageId,
        CancellationToken cancellationToken)
    {
        using var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"https://gmail.googleapis.com/gmail/v1/users/me/messages/{Uri.EscapeDataString(messageId)}?format=full",
            accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await GmailHttpResponse.EnsureSuccessAsync(response);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = JsonSerializer.Deserialize(body, GmailJsonSerializerContext.Default.GmailMessageResponse);

        if (message is null)
        {
            return null;
        }

        var headers = message.Payload?.Headers ?? [];
        var subject = headers.FirstOrDefault((header) => string.Equals(header.Name, "Subject", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
        var from = headers.FirstOrDefault((header) => string.Equals(header.Name, "From", StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;
        var textBody = ExtractTextBody(message.Payload);
        var receivedAt = ParseReceivedAt(message.InternalDate);

        return new GmailMessage(
            message.Id ?? messageId,
            message.HistoryId,
            subject.Trim(),
            from.Trim(),
            (message.Snippet ?? string.Empty).Trim(),
            textBody.Trim(),
            receivedAt);
    }

    private async Task<MailConnectedIdentity> BuildIdentityAsync(
        HttpResponseMessage tokenResponse,
        CancellationToken cancellationToken,
        string? fallbackProviderUserId = null,
        string? fallbackEmail = null,
        string? fallbackDisplayName = null)
    {
        await GmailHttpResponse.EnsureSuccessAsync(tokenResponse);
        using var tokenDocument = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(cancellationToken));
        var accessToken = tokenDocument.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Google did not return an access token.");
        var refreshToken = tokenDocument.RootElement.TryGetProperty("refresh_token", out var refreshTokenElement)
            ? refreshTokenElement.GetString()
            : null;
        DateTimeOffset? expiresAt = tokenDocument.RootElement.TryGetProperty("expires_in", out var expiresInElement)
            ? DateTimeOffset.UtcNow.AddSeconds(expiresInElement.GetInt32())
            : null;

        using var profileRequest = CreateAuthenticatedRequest(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo", accessToken);
        using var profileResponse = await httpClient.SendAsync(profileRequest, cancellationToken);
        await GmailHttpResponse.EnsureSuccessAsync(profileResponse);

        using var profileDocument = JsonDocument.Parse(await profileResponse.Content.ReadAsStringAsync(cancellationToken));
        var providerUserId = profileDocument.RootElement.TryGetProperty("sub", out var subElement)
            ? subElement.GetString()
            : fallbackProviderUserId;
        var email = profileDocument.RootElement.TryGetProperty("email", out var emailElement)
            ? emailElement.GetString()
            : fallbackEmail;
        var displayName = profileDocument.RootElement.TryGetProperty("name", out var nameElement)
            ? nameElement.GetString()
            : fallbackDisplayName;

        return new MailConnectedIdentity(
            providerUserId ?? throw new InvalidOperationException("Google did not return the connected account id."),
            email,
            displayName,
            accessToken,
            refreshToken,
            expiresAt);
    }

    private static HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static DateTimeOffset ParseReceivedAt(string? internalDate)
    {
        if (long.TryParse(internalDate, out var milliseconds))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        }

        return DateTimeOffset.UtcNow;
    }

    private static string ExtractTextBody(GmailMessagePayloadResponse? payload)
    {
        if (payload is null)
        {
            return string.Empty;
        }

        var preferredText = ExtractMimeBody(payload, "text/plain");

        if (!string.IsNullOrWhiteSpace(preferredText))
        {
            return preferredText;
        }

        var htmlBody = ExtractMimeBody(payload, "text/html");

        if (string.IsNullOrWhiteSpace(htmlBody))
        {
            return string.Empty;
        }

        return Regex.Replace(WebUtility.HtmlDecode(htmlBody), "<[^>]+>", " ");
    }

    private static string ExtractMimeBody(GmailMessagePayloadResponse payload, string mimeType)
    {
        if (string.Equals(payload.MimeType, mimeType, StringComparison.OrdinalIgnoreCase))
        {
            return DecodeBase64Url(payload.Body?.Data);
        }

        if (payload.Parts is null)
        {
            return string.Empty;
        }

        foreach (var part in payload.Parts)
        {
            var value = ExtractMimeBody(part, mimeType);

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string DecodeBase64Url(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        var bytes = Convert.FromBase64String(padded);
        return Encoding.UTF8.GetString(bytes);
    }
}
