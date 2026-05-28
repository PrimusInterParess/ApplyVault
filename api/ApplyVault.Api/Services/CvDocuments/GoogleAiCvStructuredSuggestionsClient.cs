using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public sealed class GoogleAiCvStructuredSuggestionsClient(
    HttpClient httpClient,
    IOptions<GoogleAiOptions> googleAiOptions,
    IOptions<CvSuggestionsAiOptions> suggestionsAiOptions) : ICvStructuredSuggestionsAiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<CvImprovementSuggestionsDto> GenerateAsync(
        CvStructuredDocumentDto current,
        IReadOnlyList<Guid>? focusSectionIds = null,
        int maxSuggestions = 6,
        CancellationToken cancellationToken = default)
    {
        var options = googleAiOptions.Value;

        if (!options.Enabled)
        {
            throw new InvalidOperationException("Google AI is disabled. Enable GoogleAi:Enabled to generate CV suggestions.");
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
            BuildRequest(current, focusSectionIds, maxSuggestions),
            SerializerOptions,
            timeoutCts.Token);

        var responsePayload = await response.Content.ReadAsStringAsync(timeoutCts.Token);
        response.EnsureSuccessStatusCode();

        var generatedJson = ExtractGeneratedJson(responsePayload);
        var result = JsonSerializer.Deserialize<CvStructuredSuggestionsAiResponse>(generatedJson, SerializerOptions)
            ?? throw new InvalidOperationException("Google AI returned an empty CV suggestions payload.");

        return Normalize(current, result, maxSuggestions);
    }

    private object BuildRequest(
        CvStructuredDocumentDto current,
        IReadOnlyList<Guid>? focusSectionIds,
        int maxSuggestions)
    {
        var prompts = suggestionsAiOptions.Value;
        var payloadJson = JsonSerializer.Serialize(current, SerializerOptions);
        var focusSections = BuildFocusSectionsText(current, focusSectionIds);

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
                                .Replace("{{maxSuggestions}}", maxSuggestions.ToString(), StringComparison.Ordinal)
                                .Replace("{{focusSections}}", focusSections, StringComparison.Ordinal)
                                .Replace("{{payloadJson}}", payloadJson, StringComparison.Ordinal)
                        }
                    }
                }
            },
            generationConfig = GoogleAiCvStructuredSuggestionsResponseSchema.Create()
        };
    }

    private static string BuildFocusSectionsText(
        CvStructuredDocumentDto current,
        IReadOnlyList<Guid>? focusSectionIds)
    {
        if (focusSectionIds is null || focusSectionIds.Count == 0)
        {
            return "Review the full CV and suggest the highest-value improvements.";
        }

        var sectionsById = current.Sections.ToDictionary((section) => section.Id);
        var lines = new List<string>
        {
            "Focus sections (suggest improvements primarily for these sections):"
        };

        foreach (var sectionId in focusSectionIds)
        {
            if (sectionsById.TryGetValue(sectionId, out var section))
            {
                lines.Add($"- {section.Heading} (id: {section.Id})");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static CvImprovementSuggestionsDto Normalize(
        CvStructuredDocumentDto current,
        CvStructuredSuggestionsAiResponse response,
        int maxSuggestions)
    {
        var knownSectionIds = current.Sections.Select((section) => section.Id).ToHashSet();
        var knownEntryIds = current.Sections.SelectMany((section) => section.Entries).Select((entry) => entry.Id).ToHashSet();
        var suggestions = response.Suggestions
            .Where((suggestion) =>
                !string.IsNullOrWhiteSpace(suggestion.Title) &&
                !string.IsNullOrWhiteSpace(suggestion.Rationale) &&
                !string.IsNullOrWhiteSpace(suggestion.SuggestedInstruction))
            .Take(maxSuggestions)
            .Select((suggestion, index) =>
            {
                var sectionId = ParseKnownGuid(suggestion.SectionId, knownSectionIds);
                var entryId = ParseKnownGuid(suggestion.EntryId, knownEntryIds);

                return new CvImprovementSuggestionDto(
                    string.IsNullOrWhiteSpace(suggestion.Id) ? $"suggestion-{index + 1}" : suggestion.Id.Trim(),
                    suggestion.Title.Trim(),
                    suggestion.Rationale.Trim(),
                    suggestion.SuggestedInstruction.Trim(),
                    sectionId,
                    entryId,
                    string.IsNullOrWhiteSpace(suggestion.Category) ? "Content" : suggestion.Category.Trim(),
                    string.IsNullOrWhiteSpace(suggestion.Impact) ? "Medium" : suggestion.Impact.Trim());
            })
            .ToArray();

        return new CvImprovementSuggestionsDto(current.DocumentId, current.StructuredImportedAt, suggestions);
    }

    private static Guid? ParseKnownGuid(string? value, HashSet<Guid> knownIds) =>
        Guid.TryParse(value, out var parsed) && knownIds.Contains(parsed)
            ? parsed
            : null;

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
