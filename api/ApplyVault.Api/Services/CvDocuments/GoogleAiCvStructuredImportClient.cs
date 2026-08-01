using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services.CvSectionCatalog;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public sealed class GoogleAiCvStructuredImportClient(
    HttpClient httpClient,
    IOptions<GoogleAiOptions> googleAiOptions,
    IOptions<CvImportAiOptions> importAiOptions,
    ICvSectionCatalog sectionCatalog) : ICvStructuredImportAiClient
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
            // Import is heuristic-first; Gemini is optional and only called when the backend gate requests it.
            throw new InvalidOperationException(
                "Google AI is disabled. PDF import uses heuristic structuring unless GoogleAi:Enabled and the import gate requests AI.");
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
                    SectionType = sectionCatalog.Normalize(section.SectionType),
                    Entries = section.Entries?
                        .Where(CvStructuredImportEntrySupport.EntryHasContent)
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
        // Catalog owns ADR-0001 section/field rules; preface frames gated invocation + Contact/Custom/link integrity.
        var systemPrompt = ComposeSystemPrompt(prompts.SystemPromptPreface, sectionCatalog.BuildImportSystemPrompt());

        return new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = systemPrompt } }
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
            generationConfig = GoogleAiCvSectionsResponseSchema.Create(sectionCatalog)
        };
    }

    internal static string ComposeSystemPrompt(string preface, string catalogPrompt)
    {
        var trimmedPreface = preface?.Trim() ?? string.Empty;
        var trimmedCatalog = catalogPrompt?.Trim() ?? string.Empty;

        if (trimmedPreface.Length == 0)
        {
            return trimmedCatalog;
        }

        if (trimmedCatalog.Length == 0)
        {
            return trimmedPreface;
        }

        return $"{trimmedPreface}\n\n{trimmedCatalog}";
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
