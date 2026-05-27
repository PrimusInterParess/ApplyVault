using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public sealed class GoogleAiCvExportClient(
    HttpClient httpClient,
    IOptions<GoogleAiOptions> googleAiOptions,
    IOptions<CvExportAiOptions> exportAiOptions) : ICvExportAiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<CvStructuredImportResult> PolishAsync(
        IReadOnlyList<CvExportSectionInput> sections,
        CancellationToken cancellationToken = default)
    {
        var options = googleAiOptions.Value;

        if (!options.Enabled)
        {
            throw new InvalidOperationException("Google AI is disabled.");
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
            ?? throw new InvalidOperationException("Google AI returned an empty CV export payload.");

        return result with
        {
            Sections = result.Sections?
                .Where((section) => !string.IsNullOrWhiteSpace(section.Heading))
                .Select((section) => section with
                {
                    SectionType = CvSectionTypes.Normalize(section.SectionType),
                    Entries = section.Entries?
                        .Where(EntryHasContent)
                        .Select((entry) => entry with
                        {
                            Title = entry.Title?.Trim() ?? string.Empty,
                            Subtitle = NullIfEmpty(entry.Subtitle),
                            DateRange = NullIfEmpty(entry.DateRange),
                            Summary = entry.Summary?.Trim() ?? string.Empty,
                            Bullets = entry.Bullets?
                                .Where((bullet) => !string.IsNullOrWhiteSpace(bullet))
                                .Select((bullet) => bullet.Trim())
                                .ToArray() ?? [],
                            TechStack = entry.TechStack?.Trim() ?? string.Empty
                        })
                        .ToArray() ?? []
                })
                .ToArray() ?? []
        };
    }

    private object BuildRequest(IReadOnlyList<CvExportSectionInput> sections)
    {
        var prompts = exportAiOptions.Value;
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
            generationConfig = GoogleAiCvSectionsResponseSchema.Create()
        };
    }

    private static bool EntryHasContent(CvStructuredImportEntryResult entry) =>
        !string.IsNullOrWhiteSpace(entry.Title)
        || !string.IsNullOrWhiteSpace(entry.Subtitle)
        || !string.IsNullOrWhiteSpace(entry.DateRange)
        || !string.IsNullOrWhiteSpace(entry.Summary)
        || entry.Bullets?.Count > 0
        || !string.IsNullOrWhiteSpace(entry.TechStack);

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
