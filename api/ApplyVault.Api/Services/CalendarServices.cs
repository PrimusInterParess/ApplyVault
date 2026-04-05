using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public static class CalendarProviders
{
    public const string Google = "google";
    public const string Microsoft = "microsoft";

    public static bool IsSupported(string provider) =>
        provider is Google or Microsoft;
}

public sealed record CalendarAuthorizationState(
    Guid UserId,
    string Provider,
    string? ReturnUrl
);

public sealed record CalendarConnectedIdentity(
    string ProviderUserId,
    string? Email,
    string? DisplayName,
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset? ExpiresAt
);

public sealed record CalendarEventDraft(
    string Title,
    string Description,
    string? Location,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string TimeZone
);

public sealed record CalendarEventOperationResult(
    string ExternalEventId,
    string? ExternalEventUrl
);

public interface ICalendarProvider
{
    string Provider { get; }

    string BuildAuthorizationUrl(string state);

    Task<CalendarConnectedIdentity> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<CalendarConnectedIdentity> RefreshAsync(
        ConnectedAccountEntity account,
        CancellationToken cancellationToken = default);

    Task<CalendarEventOperationResult> CreateEventAsync(
        ConnectedAccountEntity account,
        CalendarEventDraft draft,
        CancellationToken cancellationToken = default);

    Task<CalendarEventOperationResult> UpdateEventAsync(
        ConnectedAccountEntity account,
        string externalEventId,
        CalendarEventDraft draft,
        CancellationToken cancellationToken = default);
}

public interface ICalendarProviderFactory
{
    ICalendarProvider GetRequired(string provider);
}

public interface ICalendarConnectionService
{
    Task<IReadOnlyList<ConnectedCalendarAccountDto>> GetConnectionsAsync(
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

public interface ICalendarEventService
{
    Task<CalendarEventLinkDto> SyncEventAsync(
        AppUserEntity user,
        Guid scrapeResultId,
        Guid connectedAccountId,
        CancellationToken cancellationToken = default);
}

public sealed class CalendarProviderFactory(IEnumerable<ICalendarProvider> providers) : ICalendarProviderFactory
{
    private readonly Dictionary<string, ICalendarProvider> providerMap = providers.ToDictionary(
        (provider) => provider.Provider,
        StringComparer.OrdinalIgnoreCase);

    public ICalendarProvider GetRequired(string provider)
    {
        if (providerMap.TryGetValue(provider, out var calendarProvider))
        {
            return calendarProvider;
        }

        throw new InvalidOperationException($"The calendar provider '{provider}' is not supported.");
    }
}

public sealed class CalendarConnectionService(
    ApplyVaultDbContext dbContext,
    ICalendarProviderFactory calendarProviderFactory,
    IOptions<CalendarIntegrationOptions> integrationOptions,
    IDataProtectionProvider dataProtectionProvider) : ICalendarConnectionService
{
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector("ApplyVault.CalendarOAuthState");

    public async Task<IReadOnlyList<ConnectedCalendarAccountDto>> GetConnectionsAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default)
    {
        var accounts = await dbContext.ConnectedAccounts
            .AsNoTracking()
            .Where((account) => account.UserId == user.Id)
            .OrderBy((account) => account.Provider)
            .ThenBy((account) => account.Email)
            .ToArrayAsync(cancellationToken);

        return accounts.Select(MapToDto).ToArray();
    }

    public string BuildAuthorizationUrl(AppUserEntity user, string provider, string? returnUrl = null)
    {
        var calendarProvider = calendarProviderFactory.GetRequired(provider);
        var payload = JsonSerializer.Serialize(new CalendarAuthorizationState(user.Id, provider, returnUrl));
        var protectedState = protector.Protect(payload);
        return calendarProvider.BuildAuthorizationUrl(protectedState);
    }

    public async Task<string> CompleteAuthorizationAsync(
        string provider,
        string code,
        string state,
        CancellationToken cancellationToken = default)
    {
        var calendarProvider = calendarProviderFactory.GetRequired(provider);
        var authorizationState = JsonSerializer.Deserialize<CalendarAuthorizationState>(protector.Unprotect(state))
            ?? throw new InvalidOperationException("The calendar authorization state is invalid.");

        if (!string.Equals(authorizationState.Provider, provider, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The calendar authorization provider does not match the original request.");
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(
            (candidate) => candidate.Id == authorizationState.UserId,
            cancellationToken)
            ?? throw new InvalidOperationException("The user that started the calendar authorization flow no longer exists.");

        var connectedIdentity = await calendarProvider.ExchangeCodeAsync(code, cancellationToken);
        var utcNow = DateTimeOffset.UtcNow;
        var account = await dbContext.ConnectedAccounts.SingleOrDefaultAsync(
            (candidate) =>
                candidate.UserId == user.Id &&
                candidate.Provider == provider &&
                candidate.ProviderUserId == connectedIdentity.ProviderUserId,
            cancellationToken);

        if (account is null)
        {
            account = new ConnectedAccountEntity
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Provider = provider,
                ProviderUserId = connectedIdentity.ProviderUserId,
                AccessToken = connectedIdentity.AccessToken,
                CreatedAt = utcNow
            };

            await dbContext.ConnectedAccounts.AddAsync(account, cancellationToken);
        }

        account.Email = connectedIdentity.Email;
        account.DisplayName = connectedIdentity.DisplayName;
        account.AccessToken = connectedIdentity.AccessToken;
        account.RefreshToken = connectedIdentity.RefreshToken ?? account.RefreshToken;
        account.ExpiresAt = connectedIdentity.ExpiresAt;
        account.UpdatedAt = utcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return AppendQueryString(
            string.IsNullOrWhiteSpace(authorizationState.ReturnUrl)
                ? integrationOptions.Value.PostConnectRedirectUrl
                : authorizationState.ReturnUrl!,
            new Dictionary<string, string?>
            {
                ["provider"] = provider,
                ["success"] = "true"
            });
    }

    public async Task<bool> DeleteConnectionAsync(
        AppUserEntity user,
        Guid connectionId,
        CancellationToken cancellationToken = default)
    {
        var account = await dbContext.ConnectedAccounts.SingleOrDefaultAsync(
            (candidate) => candidate.UserId == user.Id && candidate.Id == connectionId,
            cancellationToken);

        if (account is null)
        {
            return false;
        }

        dbContext.ConnectedAccounts.Remove(account);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ConnectedCalendarAccountDto MapToDto(ConnectedAccountEntity account) =>
        new(
            account.Id,
            account.Provider,
            account.ProviderUserId,
            account.Email,
            account.DisplayName,
            account.ExpiresAt,
            account.CreatedAt,
            account.UpdatedAt);

    private static string AppendQueryString(string baseUrl, IReadOnlyDictionary<string, string?> values)
    {
        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var query = string.Join(
            "&",
            values
                .Where((pair) => !string.IsNullOrWhiteSpace(pair.Value))
                .Select((pair) => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}"));

        return string.IsNullOrWhiteSpace(query) ? baseUrl : $"{baseUrl}{separator}{query}";
    }
}

public sealed class CalendarEventService(
    ApplyVaultDbContext dbContext,
    ICalendarProviderFactory calendarProviderFactory) : ICalendarEventService
{
    public async Task<CalendarEventLinkDto> SyncEventAsync(
        AppUserEntity user,
        Guid scrapeResultId,
        Guid connectedAccountId,
        CancellationToken cancellationToken = default)
    {
        var scrapeResult = await dbContext.ScrapeResults
            .Include((result) => result.InterviewEvent)
            .Include((result) => result.CalendarEventLinks)
            .SingleOrDefaultAsync(
                (result) =>
                    result.Id == scrapeResultId &&
                    !result.IsDeleted &&
                    (result.UserId == user.Id || result.UserId == null),
                cancellationToken)
            ?? throw new InvalidOperationException("The requested scrape result does not exist.");

        var interviewEvent = scrapeResult.InterviewEvent
            ?? throw new InvalidOperationException("Save interview timing before creating a calendar event.");

        var account = await dbContext.ConnectedAccounts.SingleOrDefaultAsync(
            (candidate) => candidate.UserId == user.Id && candidate.Id == connectedAccountId,
            cancellationToken)
            ?? throw new InvalidOperationException("The requested calendar connection does not exist.");

        var provider = calendarProviderFactory.GetRequired(account.Provider);
        account = await EnsureFreshAccessTokenAsync(provider, account, cancellationToken);

        var draft = new CalendarEventDraft(
            Title: BuildEventTitle(scrapeResult),
            Description: BuildEventDescription(scrapeResult, interviewEvent),
            Location: interviewEvent.Location,
            StartUtc: interviewEvent.StartUtc,
            EndUtc: interviewEvent.EndUtc,
            TimeZone: interviewEvent.TimeZone);

        var existingLink = scrapeResult.CalendarEventLinks.SingleOrDefault(
            (link) => link.ConnectedAccountId == account.Id);
        var utcNow = DateTimeOffset.UtcNow;
        CalendarEventOperationResult result;

        if (existingLink is null)
        {
            result = await provider.CreateEventAsync(account, draft, cancellationToken);
            existingLink = new CalendarEventLinkEntity
            {
                Id = Guid.NewGuid(),
                ScrapeResultId = scrapeResult.Id,
                ConnectedAccountId = account.Id,
                Provider = account.Provider,
                ExternalEventId = result.ExternalEventId,
                ExternalEventUrl = result.ExternalEventUrl,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };

            await dbContext.CalendarEventLinks.AddAsync(existingLink, cancellationToken);
        }
        else
        {
            result = await provider.UpdateEventAsync(account, existingLink.ExternalEventId, draft, cancellationToken);
            existingLink.ExternalEventUrl = result.ExternalEventUrl;
            existingLink.UpdatedAt = utcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CalendarEventLinkDto(
            existingLink.Id,
            existingLink.ConnectedAccountId,
            existingLink.Provider,
            existingLink.ExternalEventId,
            existingLink.ExternalEventUrl,
            existingLink.CreatedAt,
            existingLink.UpdatedAt);
    }

    private async Task<ConnectedAccountEntity> EnsureFreshAccessTokenAsync(
        ICalendarProvider provider,
        ConnectedAccountEntity account,
        CancellationToken cancellationToken)
    {
        if (account.ExpiresAt is null || account.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return account;
        }

        if (string.IsNullOrWhiteSpace(account.RefreshToken))
        {
            return account;
        }

        var refreshed = await provider.RefreshAsync(account, cancellationToken);
        account.AccessToken = refreshed.AccessToken;
        account.RefreshToken = refreshed.RefreshToken ?? account.RefreshToken;
        account.ExpiresAt = refreshed.ExpiresAt;
        account.Email = refreshed.Email ?? account.Email;
        account.DisplayName = refreshed.DisplayName ?? account.DisplayName;
        account.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return account;
    }

    private static string BuildEventTitle(ScrapeResultEntity scrapeResult)
    {
        var company = string.IsNullOrWhiteSpace(scrapeResult.CompanyName) ? "Unknown company" : scrapeResult.CompanyName;
        return $"Interview: {scrapeResult.Title} at {company}";
    }

    private static string BuildEventDescription(ScrapeResultEntity scrapeResult, InterviewEventEntity interviewEvent)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Role: {scrapeResult.Title}");

        if (!string.IsNullOrWhiteSpace(scrapeResult.CompanyName))
        {
            builder.AppendLine($"Company: {scrapeResult.CompanyName}");
        }

        builder.AppendLine($"Listing: {scrapeResult.Url}");

        if (!string.IsNullOrWhiteSpace(scrapeResult.HiringManagerName))
        {
            builder.AppendLine($"Hiring manager: {scrapeResult.HiringManagerName}");
        }

        if (!string.IsNullOrWhiteSpace(interviewEvent.Notes))
        {
            builder.AppendLine();
            builder.AppendLine(interviewEvent.Notes.Trim());
        }

        return builder.ToString().Trim();
    }
}

public sealed class GoogleCalendarProvider(
    HttpClient httpClient,
    IOptions<CalendarIntegrationOptions> options) : ICalendarProvider
{
    private const string CalendarScope = "https://www.googleapis.com/auth/calendar.events";
    private readonly GoogleCalendarOptions providerOptions = options.Value.Google;

    public string Provider => CalendarProviders.Google;

    public string BuildAuthorizationUrl(string state)
    {
        var scopes = Uri.EscapeDataString($"{CalendarScope} openid email profile");
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

    public async Task<CalendarConnectedIdentity> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
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

        return await BuildGoogleIdentityAsync(response, cancellationToken);
    }

    public async Task<CalendarConnectedIdentity> RefreshAsync(
        ConnectedAccountEntity account,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = providerOptions.ClientId,
                ["client_secret"] = providerOptions.ClientSecret,
                ["refresh_token"] = account.RefreshToken ?? string.Empty,
                ["grant_type"] = "refresh_token"
            }),
            cancellationToken);

        return await BuildGoogleIdentityAsync(response, cancellationToken, account.ProviderUserId, account.Email, account.DisplayName);
    }

    public Task<CalendarEventOperationResult> CreateEventAsync(
        ConnectedAccountEntity account,
        CalendarEventDraft draft,
        CancellationToken cancellationToken = default) =>
        SendEventAsync(HttpMethod.Post, "https://www.googleapis.com/calendar/v3/calendars/primary/events", account, draft, null, cancellationToken);

    public Task<CalendarEventOperationResult> UpdateEventAsync(
        ConnectedAccountEntity account,
        string externalEventId,
        CalendarEventDraft draft,
        CancellationToken cancellationToken = default) =>
        SendEventAsync(
            HttpMethod.Put,
            $"https://www.googleapis.com/calendar/v3/calendars/primary/events/{Uri.EscapeDataString(externalEventId)}",
            account,
            draft,
            externalEventId,
            cancellationToken);

    private async Task<CalendarEventOperationResult> SendEventAsync(
        HttpMethod method,
        string url,
        ConnectedAccountEntity account,
        CalendarEventDraft draft,
        string? externalEventId,
        CancellationToken cancellationToken)
    {
        var googleTimeZone = CalendarTimeZoneResolver.NormalizeForGoogle(draft.TimeZone);
        var startLocal = CalendarTimeZoneResolver.ConvertUtcToLocalTime(draft.StartUtc, draft.TimeZone);
        var endLocal = CalendarTimeZoneResolver.ConvertUtcToLocalTime(draft.EndUtc, draft.TimeZone);

        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);
        request.Content = JsonContent.Create(new
        {
            id = externalEventId,
            summary = draft.Title,
            description = draft.Description,
            location = draft.Location,
            start = new
            {
                dateTime = CalendarTimeZoneResolver.FormatLocalDateTime(startLocal),
                timeZone = googleTimeZone
            },
            end = new
            {
                dateTime = CalendarTimeZoneResolver.FormatLocalDateTime(endLocal),
                timeZone = googleTimeZone
            }
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await CalendarHttpResponse.EnsureSuccessAsync(response);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return new CalendarEventOperationResult(
            document.RootElement.GetProperty("id").GetString() ?? throw new InvalidOperationException("Google Calendar did not return an event id."),
            document.RootElement.TryGetProperty("htmlLink", out var htmlLink) ? htmlLink.GetString() : null);
    }

    private async Task<CalendarConnectedIdentity> BuildGoogleIdentityAsync(
        HttpResponseMessage tokenResponse,
        CancellationToken cancellationToken,
        string? fallbackProviderUserId = null,
        string? fallbackEmail = null,
        string? fallbackDisplayName = null)
    {
        await CalendarHttpResponse.EnsureSuccessAsync(tokenResponse);
        using var tokenDocument = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(cancellationToken));
        var accessToken = tokenDocument.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Google did not return an access token.");
        var refreshToken = tokenDocument.RootElement.TryGetProperty("refresh_token", out var refreshTokenElement)
            ? refreshTokenElement.GetString()
            : null;
        DateTimeOffset? expiresAt = tokenDocument.RootElement.TryGetProperty("expires_in", out var expiresInElement)
            ? DateTimeOffset.UtcNow.AddSeconds(expiresInElement.GetInt32())
            : null;

        using var profileRequest = new HttpRequestMessage(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
        profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var profileResponse = await httpClient.SendAsync(profileRequest, cancellationToken);
        await CalendarHttpResponse.EnsureSuccessAsync(profileResponse);

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

        return new CalendarConnectedIdentity(
            providerUserId ?? throw new InvalidOperationException("Google did not return the connected account id."),
            email,
            displayName,
            accessToken,
            refreshToken,
            expiresAt);
    }
}

public sealed class MicrosoftCalendarProvider(
    HttpClient httpClient,
    IOptions<CalendarIntegrationOptions> options) : ICalendarProvider
{
    private readonly MicrosoftCalendarOptions providerOptions = options.Value.Microsoft;

    public string Provider => CalendarProviders.Microsoft;

    public string BuildAuthorizationUrl(string state)
    {
        const string scopes = "offline_access openid profile email User.Read Calendars.ReadWrite";
        return $"https://login.microsoftonline.com/{Uri.EscapeDataString(providerOptions.TenantId)}/oauth2/v2.0/authorize"
            + $"?client_id={Uri.EscapeDataString(providerOptions.ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(providerOptions.RedirectUri)}"
            + "&response_type=code"
            + $"&scope={Uri.EscapeDataString(scopes)}"
            + $"&state={Uri.EscapeDataString(state)}";
    }

    public async Task<CalendarConnectedIdentity> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync(
            $"https://login.microsoftonline.com/{providerOptions.TenantId}/oauth2/v2.0/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = providerOptions.ClientId,
                ["client_secret"] = providerOptions.ClientSecret,
                ["code"] = code,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = providerOptions.RedirectUri,
                ["scope"] = "offline_access openid profile email User.Read Calendars.ReadWrite"
            }),
            cancellationToken);

        return await BuildMicrosoftIdentityAsync(response, cancellationToken);
    }

    public async Task<CalendarConnectedIdentity> RefreshAsync(
        ConnectedAccountEntity account,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync(
            $"https://login.microsoftonline.com/{providerOptions.TenantId}/oauth2/v2.0/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = providerOptions.ClientId,
                ["client_secret"] = providerOptions.ClientSecret,
                ["refresh_token"] = account.RefreshToken ?? string.Empty,
                ["grant_type"] = "refresh_token",
                ["scope"] = "offline_access openid profile email User.Read Calendars.ReadWrite"
            }),
            cancellationToken);

        return await BuildMicrosoftIdentityAsync(response, cancellationToken, account.ProviderUserId, account.Email, account.DisplayName);
    }

    public Task<CalendarEventOperationResult> CreateEventAsync(
        ConnectedAccountEntity account,
        CalendarEventDraft draft,
        CancellationToken cancellationToken = default) =>
        SendEventAsync(HttpMethod.Post, "https://graph.microsoft.com/v1.0/me/events", account, draft, cancellationToken);

    public Task<CalendarEventOperationResult> UpdateEventAsync(
        ConnectedAccountEntity account,
        string externalEventId,
        CalendarEventDraft draft,
        CancellationToken cancellationToken = default) =>
        SendEventAsync(
            HttpMethod.Patch,
            $"https://graph.microsoft.com/v1.0/me/events/{Uri.EscapeDataString(externalEventId)}",
            account,
            draft,
            cancellationToken);

    private async Task<CalendarEventOperationResult> SendEventAsync(
        HttpMethod method,
        string url,
        ConnectedAccountEntity account,
        CalendarEventDraft draft,
        CancellationToken cancellationToken)
    {
        var microsoftTimeZone = CalendarTimeZoneResolver.NormalizeForMicrosoft(draft.TimeZone);
        var startLocal = CalendarTimeZoneResolver.ConvertUtcToLocalTime(draft.StartUtc, draft.TimeZone);
        var endLocal = CalendarTimeZoneResolver.ConvertUtcToLocalTime(draft.EndUtc, draft.TimeZone);

        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);
        request.Content = JsonContent.Create(new
        {
            subject = draft.Title,
            body = new
            {
                contentType = "text",
                content = draft.Description
            },
            location = string.IsNullOrWhiteSpace(draft.Location)
                ? null
                : new
                {
                    displayName = draft.Location
                },
            start = new
            {
                dateTime = CalendarTimeZoneResolver.FormatLocalDateTime(startLocal),
                timeZone = microsoftTimeZone
            },
            end = new
            {
                dateTime = CalendarTimeZoneResolver.FormatLocalDateTime(endLocal),
                timeZone = microsoftTimeZone
            }
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        await CalendarHttpResponse.EnsureSuccessAsync(response);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return new CalendarEventOperationResult(
            document.RootElement.GetProperty("id").GetString() ?? throw new InvalidOperationException("Microsoft Graph did not return an event id."),
            document.RootElement.TryGetProperty("webLink", out var webLink) ? webLink.GetString() : null);
    }

    private async Task<CalendarConnectedIdentity> BuildMicrosoftIdentityAsync(
        HttpResponseMessage tokenResponse,
        CancellationToken cancellationToken,
        string? fallbackProviderUserId = null,
        string? fallbackEmail = null,
        string? fallbackDisplayName = null)
    {
        await CalendarHttpResponse.EnsureSuccessAsync(tokenResponse);
        using var tokenDocument = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync(cancellationToken));
        var accessToken = tokenDocument.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Microsoft did not return an access token.");
        var refreshToken = tokenDocument.RootElement.TryGetProperty("refresh_token", out var refreshTokenElement)
            ? refreshTokenElement.GetString()
            : null;
        DateTimeOffset? expiresAt = tokenDocument.RootElement.TryGetProperty("expires_in", out var expiresInElement)
            ? DateTimeOffset.UtcNow.AddSeconds(expiresInElement.GetInt32())
            : null;

        using var profileRequest = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me?$select=id,displayName,mail,userPrincipalName");
        profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var profileResponse = await httpClient.SendAsync(profileRequest, cancellationToken);
        await CalendarHttpResponse.EnsureSuccessAsync(profileResponse);

        using var profileDocument = JsonDocument.Parse(await profileResponse.Content.ReadAsStringAsync(cancellationToken));
        var providerUserId = profileDocument.RootElement.TryGetProperty("id", out var idElement)
            ? idElement.GetString()
            : fallbackProviderUserId;
        var email = profileDocument.RootElement.TryGetProperty("mail", out var mailElement) && mailElement.ValueKind != JsonValueKind.Null
            ? mailElement.GetString()
            : profileDocument.RootElement.TryGetProperty("userPrincipalName", out var upnElement)
                ? upnElement.GetString()
                : fallbackEmail;
        var displayName = profileDocument.RootElement.TryGetProperty("displayName", out var nameElement)
            ? nameElement.GetString()
            : fallbackDisplayName;

        return new CalendarConnectedIdentity(
            providerUserId ?? throw new InvalidOperationException("Microsoft did not return the connected account id."),
            email,
            displayName,
            accessToken,
            refreshToken,
            expiresAt);
    }
}

internal static class CalendarTimeZoneResolver
{
    public static string NormalizeForGoogle(string timeZone)
    {
        var normalized = NormalizeInput(timeZone);

        if (IsUtc(normalized))
        {
            return "UTC";
        }

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(normalized, out _))
        {
            return normalized;
        }

        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(normalized, out var ianaId))
        {
            return ianaId;
        }

        var resolvedZone = ResolveTimeZoneInfo(normalized);
        return TimeZoneInfo.TryConvertWindowsIdToIanaId(resolvedZone.Id, out ianaId)
            ? ianaId
            : resolvedZone.Id;
    }

    public static string NormalizeForMicrosoft(string timeZone)
    {
        var normalized = NormalizeInput(timeZone);

        if (IsUtc(normalized))
        {
            return "UTC";
        }

        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(normalized, out _))
        {
            return normalized;
        }

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(normalized, out var windowsId))
        {
            return windowsId;
        }

        var resolvedZone = ResolveTimeZoneInfo(normalized);
        return TimeZoneInfo.TryConvertIanaIdToWindowsId(resolvedZone.Id, out windowsId)
            ? windowsId
            : resolvedZone.Id;
    }

    public static DateTimeOffset ConvertUtcToLocalTime(DateTimeOffset utcInstant, string timeZone) =>
        TimeZoneInfo.ConvertTime(utcInstant.ToUniversalTime(), ResolveTimeZoneInfo(timeZone));

    public static string FormatLocalDateTime(DateTimeOffset localTime) =>
        localTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    private static TimeZoneInfo ResolveTimeZoneInfo(string timeZone)
    {
        var normalized = NormalizeInput(timeZone);

        foreach (var candidate in GetCandidates(normalized))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(candidate);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        throw new InvalidOperationException($"The time zone '{timeZone}' is not supported.");
    }

    private static IEnumerable<string> GetCandidates(string timeZone)
    {
        yield return timeZone;

        if (TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZone, out var windowsId))
        {
            yield return windowsId;
        }

        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZone, out var ianaId))
        {
            yield return ianaId;
        }
    }

    private static string NormalizeInput(string timeZone) =>
        string.IsNullOrWhiteSpace(timeZone) ? "UTC" : timeZone.Trim();

    private static bool IsUtc(string timeZone) =>
        string.Equals(timeZone, "UTC", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(timeZone, "Etc/UTC", StringComparison.OrdinalIgnoreCase);
}

internal static class CalendarHttpResponse
{
    public static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = (await response.Content.ReadAsStringAsync()).Trim();
        throw new InvalidOperationException(
            string.IsNullOrWhiteSpace(body)
                ? $"Calendar provider request failed with {(int)response.StatusCode} {response.ReasonPhrase}."
                : $"Calendar provider request failed with {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
    }
}
