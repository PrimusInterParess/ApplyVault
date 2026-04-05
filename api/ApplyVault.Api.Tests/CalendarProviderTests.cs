using System.Net;
using System.Text;
using System.Text.Json;
using ApplyVault.Api.Data;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Tests;

public sealed class CalendarProviderTests
{
    [Fact]
    public async Task GoogleCalendarProvider_CreateEventAsync_UsesIanaTimeZoneAndLocalClockTime()
    {
        var capturedRequestBody = string.Empty;
        var handler = new StubHttpMessageHandler((request) =>
        {
            capturedRequestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();

            return JsonResponse("""
                {
                  "id": "google-event-id",
                  "htmlLink": "https://calendar.google.com/event"
                }
                """);
        });

        using var httpClient = new HttpClient(handler);
        var provider = new GoogleCalendarProvider(httpClient, CreateOptions());

        await provider.CreateEventAsync(CreateAccount(), CreateDraft("W. Europe Standard Time"));

        using var document = JsonDocument.Parse(capturedRequestBody);
        var start = document.RootElement.GetProperty("start");
        var end = document.RootElement.GetProperty("end");
        Assert.Equal("2026-04-06T10:00:00", start.GetProperty("dateTime").GetString());
        Assert.Equal("2026-04-06T11:00:00", end.GetProperty("dateTime").GetString());

        Assert.True(TimeZoneInfo.TryConvertWindowsIdToIanaId("W. Europe Standard Time", out var expectedIanaTimeZone));
        Assert.Equal(expectedIanaTimeZone, start.GetProperty("timeZone").GetString());
        Assert.Equal(expectedIanaTimeZone, end.GetProperty("timeZone").GetString());
    }

    [Fact]
    public async Task MicrosoftCalendarProvider_CreateEventAsync_UsesWindowsTimeZoneAndLocalClockTime()
    {
        var capturedRequestBody = string.Empty;
        var handler = new StubHttpMessageHandler((request) =>
        {
            capturedRequestBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();

            return JsonResponse("""
                {
                  "id": "microsoft-event-id",
                  "webLink": "https://outlook.office.com/calendar/item"
                }
                """);
        });

        using var httpClient = new HttpClient(handler);
        var provider = new MicrosoftCalendarProvider(httpClient, CreateOptions());

        await provider.CreateEventAsync(CreateAccount(), CreateDraft("Europe/Copenhagen"));

        using var document = JsonDocument.Parse(capturedRequestBody);
        var start = document.RootElement.GetProperty("start");
        var end = document.RootElement.GetProperty("end");
        Assert.Equal("2026-04-06T10:00:00", start.GetProperty("dateTime").GetString());
        Assert.Equal("2026-04-06T11:00:00", end.GetProperty("dateTime").GetString());

        Assert.True(TimeZoneInfo.TryConvertIanaIdToWindowsId("Europe/Copenhagen", out var expectedWindowsTimeZone));
        Assert.Equal(expectedWindowsTimeZone, start.GetProperty("timeZone").GetString());
        Assert.Equal(expectedWindowsTimeZone, end.GetProperty("timeZone").GetString());
    }

    private static IOptions<CalendarIntegrationOptions> CreateOptions() =>
        Microsoft.Extensions.Options.Options.Create(new CalendarIntegrationOptions
        {
            Google = new GoogleCalendarOptions
            {
                ClientId = "google-client-id",
                ClientSecret = "google-client-secret",
                RedirectUri = "https://example.com/google/callback"
            },
            Microsoft = new MicrosoftCalendarOptions
            {
                ClientId = "microsoft-client-id",
                ClientSecret = "microsoft-client-secret",
                RedirectUri = "https://example.com/microsoft/callback",
                TenantId = "common"
            },
            PostConnectRedirectUrl = "https://example.com/settings"
        });

    private static ConnectedAccountEntity CreateAccount()
    {
        var utcNow = DateTimeOffset.UtcNow;

        return new ConnectedAccountEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Provider = CalendarProviders.Google,
            ProviderUserId = "provider-user-id",
            Email = "user@example.com",
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = utcNow.AddHours(1),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };
    }

    private static CalendarEventDraft CreateDraft(string timeZone) =>
        new(
            Title: "Interview",
            Description: "Interview details",
            Location: "Virtual",
            StartUtc: new DateTimeOffset(2026, 4, 6, 8, 0, 0, TimeSpan.Zero),
            EndUtc: new DateTimeOffset(2026, 4, 6, 9, 0, 0, TimeSpan.Zero),
            TimeZone: timeZone);

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

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
