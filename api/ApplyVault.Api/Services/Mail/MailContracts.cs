using ApplyVault.Api.Data;
using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public sealed record MailAuthorizationState(
    Guid UserId,
    string Provider,
    string? ReturnUrl
);

public sealed record MailConnectedIdentity(
    string ProviderUserId,
    string? Email,
    string? DisplayName,
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset? ExpiresAt
);

public sealed record MailRefreshRequest(
    string RefreshToken,
    string ProviderUserId,
    string? Email,
    string? DisplayName
);

public sealed record GmailMessage(
    string Id,
    string? HistoryId,
    string Subject,
    string From,
    string Snippet,
    string BodyText,
    DateTimeOffset ReceivedAt
);

public sealed record EmailDrivenInterviewSchedule(
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string TimeZone,
    string? Location
);

public sealed record EmailClassification(
    string Kind,
    double Confidence,
    EmailDrivenInterviewSchedule? InterviewSchedule
);

public interface IMailConnectionService
{
    Task<IReadOnlyList<ConnectedMailAccountDto>> GetConnectionsAsync(
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

public interface IGmailMailClient
{
    string BuildAuthorizationUrl(string state);

    Task<MailConnectedIdentity> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<MailConnectedIdentity> RefreshAsync(
        MailRefreshRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GmailMessage>> GetRecentMessagesAsync(
        string accessToken,
        DateTimeOffset sinceUtc,
        int maxResults,
        CancellationToken cancellationToken = default);
}

public interface IMailSyncProcessor
{
    Task SyncAsync(CancellationToken cancellationToken = default);
}

public interface IEmailDrivenJobUpdateService
{
    Task<bool> TryApplyAsync(
        AppUserEntity user,
        GmailMessage message,
        CancellationToken cancellationToken = default);
}

public interface IEmailDrivenInterviewCalendarSyncService
{
    Task SyncAsync(
        AppUserEntity user,
        Guid scrapeResultId,
        CancellationToken cancellationToken = default);
}

public interface IEmailJobStatusClassifier
{
    EmailClassification? Classify(GmailMessage message);
}

public interface IInterviewScheduleExtractor
{
    bool TryExtractSchedule(GmailMessage message, out EmailDrivenInterviewSchedule? schedule);
}

public interface IScrapeResultEmailMatcher
{
    ScrapeResultEntity? FindBestMatch(
        IReadOnlyList<ScrapeResultEntity> candidates,
        GmailMessage message);
}
