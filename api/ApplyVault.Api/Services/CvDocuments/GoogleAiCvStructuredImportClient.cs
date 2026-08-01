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

    public Task<CvStructuredImportResult> ParseAsync(
        string extractedFullText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(extractedFullText))
        {
            throw new InvalidOperationException(
                "Extracted CV text is empty. PDF import must hard-fail empty extraction before calling AI.");
        }

        return ParseCoreAsync(extractedFullText, cancellationToken);
    }

    private async Task<CvStructuredImportResult> ParseCoreAsync(
        string extractedFullText,
        CancellationToken cancellationToken)
    {
        var options = googleAiOptions.Value;

        if (!options.Enabled)
        {
            throw new InvalidOperationException(
                "Google AI is disabled. PDF import uses heuristic structuring when GoogleAi:Enabled is false.");
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
            BuildRequest(extractedFullText),
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

    private object BuildRequest(string extractedFullText)
    {
        var prompts = importAiOptions.Value;
        // Catalog owns ADR-0001 section/field rules; preface frames AI-first fill + Contact/Custom/link integrity.
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
                            text = ApplyUserPayload(prompts.UserPromptTemplate, extractedFullText)
                        }
                    }
                }
            },
            generationConfig = GoogleAiCvSectionsResponseSchema.Create(sectionCatalog)
        };
    }

    /// <summary>
    /// Substitutes <c>{{payload}}</c> (preferred) or legacy <c>{{payloadJson}}</c> with full extracted text.
    /// </summary>
    internal static string ApplyUserPayload(string template, string extractedFullText)
    {
        var text = template ?? string.Empty;
        if (text.Contains("{{payload}}", StringComparison.Ordinal))
        {
            return text.Replace("{{payload}}", extractedFullText, StringComparison.Ordinal);
        }

        return text.Replace("{{payloadJson}}", extractedFullText, StringComparison.Ordinal);
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
