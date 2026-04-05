using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Tests;

public sealed class GmailMailClientTests
{
    [Fact]
    public async Task GetRecentMessagesAsync_ParsesMessagesAndReturnsSortedResults()
    {
        var handler = new StubHttpMessageHandler((request) =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("access-token", request.Headers.Authorization?.Parameter);

            if (request.RequestUri!.AbsoluteUri.Contains("/messages?", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "messages": [
                        { "id": "message-2" },
                        { "id": "message-1" }
                      ]
                    }
                    """);
            }

            if (request.RequestUri.AbsoluteUri.Contains("/messages/message-1", StringComparison.Ordinal))
            {
                return JsonResponse(BuildMessageJson(
                    id: "message-1",
                    historyId: "history-1",
                    subject: "Earlier message",
                    from: "jobs@example.com",
                    snippet: "First snippet",
                    bodyText: "First body",
                    receivedAt: new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero)));
            }

            if (request.RequestUri.AbsoluteUri.Contains("/messages/message-2", StringComparison.Ordinal))
            {
                return JsonResponse(BuildMessageJson(
                    id: "message-2",
                    historyId: "history-2",
                    subject: "Later message",
                    from: "jobs@example.com",
                    snippet: "Second snippet",
                    bodyText: "Second body",
                    receivedAt: new DateTimeOffset(2026, 4, 2, 10, 0, 0, TimeSpan.Zero)));
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);

        var messages = await client.GetRecentMessagesAsync(
            "access-token",
            new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero),
            10);

        Assert.Equal(2, messages.Count);
        Assert.Equal("message-1", messages[0].Id);
        Assert.Equal("First body", messages[0].BodyText);
        Assert.Equal("message-2", messages[1].Id);
        Assert.Equal("Second body", messages[1].BodyText);
    }

    [Fact]
    public async Task RefreshAsync_UsesFallbackIdentityFieldsWhenUserInfoIsPartial()
    {
        var handler = new StubHttpMessageHandler((request) =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("/token", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "access_token": "new-access-token",
                      "expires_in": 3600
                    }
                    """);
            }

            if (request.RequestUri.AbsoluteUri.Contains("/userinfo", StringComparison.Ordinal))
            {
                return JsonResponse("""
                    {
                      "sub": "provider-user-id"
                    }
                    """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient);

        var identity = await client.RefreshAsync(
            new MailRefreshRequest(
                "refresh-token",
                "provider-user-id",
                "fallback@example.com",
                "Fallback User"));

        Assert.Equal("provider-user-id", identity.ProviderUserId);
        Assert.Equal("fallback@example.com", identity.Email);
        Assert.Equal("Fallback User", identity.DisplayName);
        Assert.Equal("new-access-token", identity.AccessToken);
        Assert.NotNull(identity.ExpiresAt);
    }

    private static GmailMailClient CreateClient(HttpClient httpClient)
    {
        return new GmailMailClient(
            httpClient,
            Microsoft.Extensions.Options.Options.Create(new MailIntegrationOptions
            {
                Gmail = new GmailMailOptions
                {
                    ClientId = "client-id",
                    ClientSecret = "client-secret",
                    RedirectUri = "https://example.com/callback"
                }
            }));
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static string BuildMessageJson(
        string id,
        string historyId,
        string subject,
        string from,
        string snippet,
        string bodyText,
        DateTimeOffset receivedAt)
    {
        var encodedBody = Convert.ToBase64String(Encoding.UTF8.GetBytes(bodyText))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return $$"""
            {
              "id": "{{id}}",
              "historyId": "{{historyId}}",
              "internalDate": "{{receivedAt.ToUnixTimeMilliseconds()}}",
              "snippet": "{{snippet}}",
              "payload": {
                "mimeType": "multipart/alternative",
                "headers": [
                  { "name": "Subject", "value": "{{subject}}" },
                  { "name": "From", "value": "{{from}}" }
                ],
                "parts": [
                  {
                    "mimeType": "text/plain",
                    "body": {
                      "data": "{{encodedBody}}"
                    }
                  }
                ]
              }
            }
            """;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = responder(request);
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }
}
