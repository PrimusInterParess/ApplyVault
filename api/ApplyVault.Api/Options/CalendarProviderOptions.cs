namespace ApplyVault.Api.Options;

public sealed class CalendarIntegrationOptions
{
    public const string SectionName = "CalendarIntegration";

    public string PostConnectRedirectUrl { get; set; } = "http://localhost:4200/integrations/calendar/callback";

    public GoogleCalendarOptions Google { get; set; } = new();

    public MicrosoftCalendarOptions Microsoft { get; set; } = new();
}

public sealed class GoogleCalendarOptions
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string RedirectUri { get; set; } = "http://localhost:5173/api/calendar-connections/google/callback";
}

public sealed class MicrosoftCalendarOptions
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string TenantId { get; set; } = "common";

    public string RedirectUri { get; set; } = "http://localhost:5173/api/calendar-connections/microsoft/callback";
}
