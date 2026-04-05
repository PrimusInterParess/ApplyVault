using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApplyVault.Api.Services;

internal static class GmailHttpResponse
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
                ? $"Gmail request failed with {(int)response.StatusCode} {response.ReasonPhrase}."
                : $"Gmail request failed with {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
    }
}

internal static class GmailJsonSerializerOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web);
}

[JsonSerializable(typeof(GmailMessageResponse))]
[JsonSerializable(typeof(GmailListMessagesResponse))]
internal sealed partial class GmailJsonSerializerContext : JsonSerializerContext;

internal sealed class GmailListMessagesResponse
{
    [JsonPropertyName("messages")]
    public List<GmailMessageListItem> Messages { get; set; } = [];
}

internal sealed class GmailMessageListItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

internal sealed class GmailMessageResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("historyId")]
    public string? HistoryId { get; set; }

    [JsonPropertyName("internalDate")]
    public string? InternalDate { get; set; }

    [JsonPropertyName("snippet")]
    public string? Snippet { get; set; }

    [JsonPropertyName("payload")]
    public GmailMessagePayloadResponse? Payload { get; set; }
}

internal sealed class GmailMessagePayloadResponse
{
    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    [JsonPropertyName("headers")]
    public List<GmailHeaderResponse> Headers { get; set; } = [];

    [JsonPropertyName("parts")]
    public List<GmailMessagePayloadResponse> Parts { get; set; } = [];

    [JsonPropertyName("body")]
    public GmailBodyResponse? Body { get; set; }
}

internal sealed class GmailHeaderResponse
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }
}

internal sealed class GmailBodyResponse
{
    [JsonPropertyName("data")]
    public string? Data { get; set; }
}
