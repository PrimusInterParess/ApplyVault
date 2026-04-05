namespace ApplyVault.Api.Options;

public sealed class MailIntegrationOptions
{
    public const string SectionName = "MailIntegration";

    public bool Enabled { get; set; } = false;

    public string PostConnectRedirectUrl { get; set; } = "http://localhost:4200/integrations/mail/callback";

    public int PollIntervalSeconds { get; set; } = 300;

    public int InitialLookbackHours { get; set; } = 168;

    public int MaxMessagesPerSync { get; set; } = 20;

    public GmailMailOptions Gmail { get; set; } = new();
}

public sealed class GmailMailOptions
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string RedirectUri { get; set; } = "http://localhost:5173/api/mail-connections/gmail/callback";
}
