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

    public const int MaxChangeBullets = 5;
    public const int MaxBulletLength = 200;

    public async Task<CvStructuredUpdateAiResult> UpdateAsync(
        CvStructuredDocumentDto modelInput,
        string instructions,
        IReadOnlyList<Guid>? focusSectionIds = null,
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
            BuildRequest(modelInput, instructions, focusSectionIds),
            SerializerOptions,
            timeoutCts.Token);

        var responsePayload = await response.Content.ReadAsStringAsync(timeoutCts.Token);
        response.EnsureSuccessStatusCode();

        var generatedJson = ExtractGeneratedJson(responsePayload);
        var result = JsonSerializer.Deserialize<CvStructuredUpdateAiResponse>(generatedJson, SerializerOptions)
            ?? throw new InvalidOperationException("Google AI returned an empty CV update payload.");

        return new CvStructuredUpdateAiResult(
            CvStructuredUpdateNormalizer.Normalize(modelInput, result),
            NormalizeChangeBullets(result.ChangeBullets));
    }

    internal static IReadOnlyList<string> NormalizeChangeBullets(IReadOnlyList<string>? changeBullets) =>
        (changeBullets ?? [])
            .Where((bullet) => !string.IsNullOrWhiteSpace(bullet))
            .Select((bullet) => Truncate(CvAiUserFacingText.StripIds(bullet.Trim()), MaxBulletLength))
            .Where((bullet) => !string.IsNullOrWhiteSpace(bullet))
            .Take(MaxChangeBullets)
            .ToArray();

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private object BuildRequest(
        CvStructuredDocumentDto modelInput,
        string instructions,
        IReadOnlyList<Guid>? focusSectionIds)
    {
        var prompts = updateAiOptions.Value;
        // Callers pass an already-scoped document when chips are selected.
        var payloadForModel = BuildPayloadForModel(modelInput, focusSectionIds);
        var payloadJson = JsonSerializer.Serialize(payloadForModel, SerializerOptions);
        var focusSections = BuildFocusSectionsText(modelInput, focusSectionIds);

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
                                .Replace("{{focusSections}}", focusSections, StringComparison.Ordinal)
                                .Replace("{{payloadJson}}", payloadJson, StringComparison.Ordinal)
                        }
                    }
                }
            },
            generationConfig = GoogleAiCvStructuredUpdateResponseSchema.Create()
        };
    }

    internal static CvStructuredDocumentDto BuildPayloadForModel(
        CvStructuredDocumentDto current,
        IReadOnlyList<Guid>? focusSectionIds)
    {
        if (focusSectionIds is null || focusSectionIds.Count == 0)
        {
            return current;
        }

        var byId = current.Sections.ToDictionary((section) => section.Id);
        var focused = focusSectionIds
            .Where(byId.ContainsKey)
            .Select((id) => byId[id])
            .ToArray();

        if (focused.Length == 0)
        {
            throw new InvalidOperationException("One or more selected CV sections were not found.");
        }

        return current with { Sections = focused };
    }

    private static string BuildFocusSectionsText(
        CvStructuredDocumentDto current,
        IReadOnlyList<Guid>? focusSectionIds)
    {
        if (focusSectionIds is null || focusSectionIds.Count == 0)
        {
            return "Apply the instructions across the full CV as appropriate.";
        }

        var sectionsById = current.Sections.ToDictionary((section) => section.Id);
        var lines = new List<string>
        {
            "Scope: focus sections only. The payload contains ONLY these sections.",
            "Return ONLY these sections. Preserve sectionId/entry ids.",
            "Do not blank dateRange or employer subtitle.",
            "changeBullets must describe only real text edits you made (or [] if unchanged)."
        };

        foreach (var sectionId in focusSectionIds)
        {
            if (!sectionsById.TryGetValue(sectionId, out var section))
            {
                continue;
            }

            lines.Add($"- {section.Heading} — sectionId={section.Id}");
        }

        return string.Join(Environment.NewLine, lines);
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
