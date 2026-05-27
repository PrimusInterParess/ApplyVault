using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public sealed class GoogleAiCvStructuredImportClient(
    HttpClient httpClient,
    IOptions<GoogleAiOptions> googleAiOptions,
    IOptions<CvImportAiOptions> importAiOptions) : ICvStructuredImportAiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<CvStructuredImportResult> ParseAsync(
        IReadOnlyList<CvImportSectionInput> sections,
        CancellationToken cancellationToken = default)
    {
        var options = googleAiOptions.Value;

        if (!options.Enabled)
        {
            throw new InvalidOperationException("Google AI is disabled. Enable GoogleAi:Enabled to import CV structure.");
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
            BuildRequest(sections),
            SerializerOptions,
            timeoutCts.Token);

        var responsePayload = await response.Content.ReadAsStringAsync(timeoutCts.Token);
        response.EnsureSuccessStatusCode();

        var generatedJson = ExtractGeneratedJson(responsePayload);
        var result = JsonSerializer.Deserialize<CvStructuredImportResult>(generatedJson, SerializerOptions)
            ?? throw new InvalidOperationException("Google AI returned an empty CV import payload.");

        return result with
        {
            Sections = result.Sections?
                .Where((section) => !string.IsNullOrWhiteSpace(section.Heading))
                .Select((section) => section with
                {
                    SectionType = CvSectionTypes.Normalize(section.SectionType),
                    Entries = section.Entries?
                        .Where((entry) => !string.IsNullOrWhiteSpace(entry.Title)
                            || !string.IsNullOrWhiteSpace(entry.Summary)
                            || entry.Bullets?.Count > 0)
                        .Select((entry) => entry with
                        {
                            Bullets = entry.Bullets?.Where((bullet) => !string.IsNullOrWhiteSpace(bullet)).ToArray() ?? [],
                            TechStack = entry.TechStack?.Trim() ?? string.Empty
                        })
                        .ToArray() ?? []
                })
                .ToArray() ?? []
        };
    }

    private object BuildRequest(IReadOnlyList<CvImportSectionInput> sections)
    {
        var prompts = importAiOptions.Value;
        var payloadJson = JsonSerializer.Serialize(sections, SerializerOptions);

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
                            text = prompts.UserPromptTemplate.Replace(
                                "{{payloadJson}}",
                                payloadJson,
                                StringComparison.Ordinal)
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
                    required = new[] { "sections" },
                    properties = new
                    {
                        sections = new
                        {
                            type = "ARRAY",
                            items = new
                            {
                                type = "OBJECT",
                                required = new[] { "heading", "sectionType", "entries" },
                                properties = new
                                {
                                    heading = new { type = "STRING" },
                                    sectionType = new { type = "STRING" },
                                    entries = new
                                    {
                                        type = "ARRAY",
                                        items = new
                                        {
                                            type = "OBJECT",
                                            required = new[] { "title", "summary", "bullets", "techStack" },
                                            properties = new
                                            {
                                                title = new { type = "STRING" },
                                                subtitle = new { type = "STRING" },
                                                dateRange = new { type = "STRING" },
                                                summary = new { type = "STRING" },
                                                bullets = new
                                                {
                                                    type = "ARRAY",
                                                    items = new { type = "STRING" }
                                                },
                                                techStack = new { type = "STRING" }
                                            }
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
