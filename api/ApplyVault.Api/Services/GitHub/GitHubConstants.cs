namespace ApplyVault.Api.Services;

public static class GitHubProviders
{
    public const string GitHub = "github";

    public static bool IsSupported(string provider) =>
        string.Equals(provider, GitHub, StringComparison.OrdinalIgnoreCase);
}

public static class GitHubConnectionSyncStatuses
{
    public const string Connected = "connected";
}
