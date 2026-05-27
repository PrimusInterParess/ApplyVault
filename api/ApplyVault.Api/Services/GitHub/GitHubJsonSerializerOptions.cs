using System.Text.Json;

namespace ApplyVault.Api.Services;

internal static class GitHubJsonSerializerOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
