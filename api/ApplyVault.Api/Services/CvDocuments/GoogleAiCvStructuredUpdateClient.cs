using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public sealed class GoogleAiCvStructuredUpdateClient(
    HttpClient httpClient,
    IOptions<GoogleAiOptions> googleAiOptions,
    IOptions<CvUpdateAiOptions> updateAiOptions) : ICvStructuredUpdateAiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<SaveCvStructuredDocumentRequest> UpdateAsync(
        CvStructuredDocumentDto current,
        string instructions,
        CancellationToken cancellationToken = default)
    {
        var options = googleAiOptions.Value;

        if (!options.Enabled)
        {
            throw new InvalidOperationException("Google AI is disabled. Enable GoogleAi:Enabled to update CV structure.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("Google AI ApiKey is not configured.");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));

        var endpoint =
            $"https://generativelanguage.googleapis.com/v1beta/models/{options.Model}:generateContent?key={Uri.EscapeDataString(options.ApiKey)}";

        using var response = await httpClient.PostAsJsonAsync(
            endpoint,
            BuildRequest(current, instructions),
            SerializerOptions,
            timeoutCts.Token);

        var responsePayload = await response.Content.ReadAsStringAsync(timeoutCts.Token);
        response.EnsureSuccessStatusCode();

        var generatedJson = ExtractGeneratedJson(responsePayload);
        var result = JsonSerializer.Deserialize<CvStructuredUpdateAiResponse>(generatedJson, SerializerOptions)
            ?? throw new InvalidOperationException("Google AI returned an empty CV update payload.");

        return CvStructuredUpdateNormalizer.Normalize(result);
    }

    private object BuildRequest(CvStructuredDocumentDto current, string instructions)
    {
        var prompts = updateAiOptions.Value;
        var payloadJson = JsonSerializer.Serialize(current, SerializerOptions);

        return new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = prompts.SystemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new
                        {
                            text = prompts.UserPromptTemplate
                                .Replace("{{instructions}}", instructions, StringComparison.Ordinal)
                                .Replace("{{payloadJson}}", payloadJson, StringComparison.Ordinal)
                        }
                    }
                }
            },
            generationConfig = GoogleAiCvStructuredUpdateResponseSchema.Create()
        };
    }

    private static string ExtractGeneratedJson(string responsePayload)
    {
        var response = JsonSerializer.Deserialize<GenerateContentResponse>(responsePayload, SerializerOptions)
            ?? throw new InvalidOperationException("Google AI returned an unreadable response.");

        var text = response.Candidates?
            .SelectMany((candidate) => candidate.Content?.Parts ?? [])
            .Select((part) => part.Text)
            .FirstOrDefault((value) => !string.IsNullOrWhiteSpace(value));

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Google AI did not return JSON content.");
        }

        var normalized = text.Trim();

        if (normalized.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = normalized.IndexOf('\n');
            var lastFence = normalized.LastIndexOf("```", StringComparison.Ordinal);

            if (firstNewLine >= 0 && lastFence > firstNewLine)
            {
                normalized = normalized[(firstNewLine + 1)..lastFence].Trim();
            }
        }

        return normalized;
    }

    private sealed record GenerateContentResponse(
        [property: JsonPropertyName("candidates")] IReadOnlyList<GenerateContentCandidate>? Candidates);

    private sealed record GenerateContentCandidate(
        [property: JsonPropertyName("content")] GenerateContentContent? Content);

    private sealed record GenerateContentContent(
        [property: JsonPropertyName("parts")] IReadOnlyList<GenerateContentPart>? Parts);

    private sealed record GenerateContentPart(
        [property: JsonPropertyName("text")] string? Text);
}
