using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public sealed class GoogleAiScrapeResultClient(
    HttpClient httpClient,
    IOptions<GoogleAiOptions> googleAiOptions) : IScrapeResultAiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<ScrapeResultDto> EnrichAsync(
        ScrapeResultDto request,
        CancellationToken cancellationToken = default)
    {
        var options = googleAiOptions.Value;

        if (!options.Enabled)
        {
            throw new InvalidOperationException("Google AI enrichment is disabled.");
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
            BuildRequest(request),
            SerializerOptions,
            timeoutCts.Token);

        var responsePayload = await response.Content.ReadAsStringAsync(timeoutCts.Token);
        response.EnsureSuccessStatusCode();

        var generatedJson = ExtractGeneratedJson(responsePayload);
        var result = JsonSerializer.Deserialize<ScrapeResultDto>(generatedJson, SerializerOptions);

        return result ?? throw new InvalidOperationException("Google AI returned an empty scrape result payload.");
    }

    private object BuildRequest(ScrapeResultDto request)
    {
        var payloadJson = JsonSerializer.Serialize(request, SerializerOptions);
        var options = googleAiOptions.Value;

        return new
        {
            systemInstruction = new
            {
                parts = new[]
                {
                    new
                    {
                        text = options.SystemPrompt
                    }
                }
            },
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new
                        {
                            text = RenderUserPrompt(options.UserPromptTemplate, payloadJson)
                        }
                    }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = new
                {
                    type = "OBJECT",
                    required = new[] { "title", "url", "text", "textLength", "extractedAt", "jobDetails" },
                    properties = new
                    {
                        title = new { type = "STRING" },
                        url = new { type = "STRING" },
                        text = new { type = "STRING" },
                        textLength = new { type = "INTEGER" },
                        extractedAt = new { type = "STRING" },
                        jobDetails = new
                        {
                            type = "OBJECT",
                            required = new[]
                            {
                                "sourceHostname",
                                "detectedPageType",
                                "hiringManagerContacts"
                            },
                            properties = new
                            {
                                sourceHostname = new { type = "STRING" },
                                detectedPageType = new { type = "STRING" },
                                jobTitle = new { type = "STRING", nullable = true },
                                companyName = new { type = "STRING", nullable = true },
                                location = new { type = "STRING", nullable = true },
                                jobDescription = new { type = "STRING", nullable = true },
                                positionSummary = new { type = "STRING", nullable = true },
                                hiringManagerName = new { type = "STRING", nullable = true },
                                hiringManagerContacts = new
                                {
                                    type = "ARRAY",
                                    items = new
                                    {
                                        type = "OBJECT",
                                        required = new[] { "type", "value" },
                                        properties = new
                                        {
                                            type = new { type = "STRING" },
                                            value = new { type = "STRING" },
                                            label = new { type = "STRING", nullable = true }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    private static string RenderUserPrompt(string template, string payloadJson)
    {
        return template.Replace("{{payloadJson}}", payloadJson, StringComparison.Ordinal);
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
