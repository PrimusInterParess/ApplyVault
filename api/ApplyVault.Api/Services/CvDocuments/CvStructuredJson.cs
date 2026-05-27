using System.Text.Json;

namespace ApplyVault.Api.Services;

internal static class CvStructuredJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string SerializeBullets(IReadOnlyList<string> bullets) =>
        JsonSerializer.Serialize(bullets ?? [], SerializerOptions);

    public static IReadOnlyList<string> DeserializeBullets(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<string>>(json, SerializerOptions) ?? [];
    }
}
