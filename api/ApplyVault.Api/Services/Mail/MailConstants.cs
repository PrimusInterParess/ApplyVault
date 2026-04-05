using ApplyVault.Api.Data;

namespace ApplyVault.Api.Services;

public static class MailProviders
{
    public const string Gmail = "gmail";

    public static bool IsSupported(string provider) =>
        string.Equals(provider, Gmail, StringComparison.OrdinalIgnoreCase);
}

public static class MailConnectionSyncStatuses
{
    public const string Connected = "connected";
    public const string Syncing = "syncing";
    public const string Error = "error";
    public const string NeedsReconnect = "needs_reconnect";
}

public static class JobStatusSources
{
    public const string Manual = "manual";
    public const string Gmail = "gmail";
}

public static class JobStatusKinds
{
    public const string Rejection = "rejection";
    public const string Interview = "interview";
}

internal static class ScrapeResultStatusUpdater
{
    public static void ApplyStatusSyncMetadata(
        ScrapeResultEntity entity,
        GmailMessage message,
        string kind,
        string source)
    {
        entity.LastStatusKind = kind;
        entity.LastStatusSource = source;
        entity.LastStatusUpdatedAt = DateTimeOffset.UtcNow;
        entity.LastStatusEmailReceivedAt = message.ReceivedAt;
        entity.LastStatusEmailFrom = MailTextNormalizer.Truncate(message.From, 320);
        entity.LastStatusEmailSubject = MailTextNormalizer.Truncate(message.Subject, 512);
    }
}
