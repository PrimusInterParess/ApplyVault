namespace ApplyVault.Api.Options;

public sealed class GitHubIntegrationOptions
{
    public const string SectionName = "GitHubIntegration";

    public bool Enabled { get; set; } = false;

    public string PostConnectRedirectUrl { get; set; } = "http://localhost:4200/integrations/github/callback";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string RedirectUri { get; set; } = "http://localhost:5173/api/github-connections/github/callback";

    public string Scopes { get; set; } = "read:user repo";
}
