using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public sealed class GoogleAiCvStructuredEvaluationClient(
    HttpClient httpClient,
    IOptions<GoogleAiOptions> googleAiOptions,
    IOptions<CvEvaluationAiOptions> evaluationAiOptions) : ICvStructuredEvaluationAiClient
{
    private static readonly HashSet<string> KnownDimensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "content",
        "structure",
        "format"
    };

    private static readonly HashSet<string> KnownSeverities = new(StringComparer.OrdinalIgnoreCase)
    {
        "info",
        "warning",
        "critical"
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<CvQualityEvaluationDto> EvaluateAsync(
        CvStructuredDocumentDto current,
        int maxFindings = 8,
        CancellationToken cancellationToken = default)
    {
        var options = googleAiOptions.Value;

        if (!options.Enabled)
        {
            throw new InvalidOperationException("Google AI is disabled. Enable GoogleAi:Enabled to evaluate CV quality.");
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
            BuildRequest(current, maxFindings),
            SerializerOptions,
            timeoutCts.Token);

        var responsePayload = await response.Content.ReadAsStringAsync(timeoutCts.Token);
        response.EnsureSuccessStatusCode();

        var generatedJson = ExtractGeneratedJson(responsePayload);
        var result = JsonSerializer.Deserialize<CvStructuredEvaluationAiResponse>(generatedJson, SerializerOptions)
            ?? throw new InvalidOperationException("Google AI returned an empty CV evaluation payload.");

        return Normalize(current, result, maxFindings);
    }

    private object BuildRequest(CvStructuredDocumentDto current, int maxFindings)
    {
        var prompts = evaluationAiOptions.Value;
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
                                .Replace("{{maxFindings}}", maxFindings.ToString(), StringComparison.Ordinal)
                                .Replace("{{payloadJson}}", payloadJson, StringComparison.Ordinal)
                        }
                    }
                }
            },
            generationConfig = GoogleAiCvStructuredEvaluationResponseSchema.Create()
        };
    }

    private static CvQualityEvaluationDto Normalize(
        CvStructuredDocumentDto current,
        CvStructuredEvaluationAiResponse response,
        int maxFindings)
    {
        var summary = CvAiUserFacingText.StripIds(response.Summary?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new InvalidOperationException("Google AI returned a CV evaluation without a summary.");
        }

        var knownSectionIds = current.Sections.Select((section) => section.Id).ToHashSet();
        var knownEntryIds = current.Sections.SelectMany((section) => section.Entries).Select((entry) => entry.Id).ToHashSet();

        var dimensionsById = response.Dimensions
            .Where((dimension) =>
                !string.IsNullOrWhiteSpace(dimension.Id) &&
                KnownDimensions.Contains(dimension.Id) &&
                !string.IsNullOrWhiteSpace(dimension.Summary))
            .GroupBy((dimension) => dimension.Id.Trim().ToLowerInvariant())
            .ToDictionary(
                (group) => group.Key,
                (group) =>
                {
                    var dimension = group.First();
                    var dimensionSummary = CvAiUserFacingText.StripIds(dimension.Summary.Trim());
                    return new CvQualityEvaluationDimensionDto(
                        group.Key,
                        ClampScore(dimension.Score),
                        string.IsNullOrWhiteSpace(dimensionSummary)
                            ? "No dimension summary returned."
                            : dimensionSummary);
                });

        var dimensions = new[] { "content", "structure", "format" }
            .Select((id) =>
                dimensionsById.TryGetValue(id, out var dimension)
                    ? dimension
                    : new CvQualityEvaluationDimensionDto(id, ClampScore(response.OverallScore), "No dimension summary returned."))
            .ToArray();

        var findings = response.Findings
            .Where((finding) =>
                !string.IsNullOrWhiteSpace(finding.Title) &&
                !string.IsNullOrWhiteSpace(finding.Detail) &&
                !string.IsNullOrWhiteSpace(finding.Dimension) &&
                KnownDimensions.Contains(finding.Dimension) &&
                !string.IsNullOrWhiteSpace(finding.Severity) &&
                KnownSeverities.Contains(finding.Severity))
            .Take(maxFindings)
            .Select((finding, index) => new CvQualityEvaluationFindingDto(
                string.IsNullOrWhiteSpace(finding.Id) ? $"finding-{index + 1}" : finding.Id.Trim(),
                finding.Dimension.Trim().ToLowerInvariant(),
                finding.Severity.Trim().ToLowerInvariant(),
                CvAiUserFacingText.StripIds(finding.Title.Trim()),
                CvAiUserFacingText.StripIds(finding.Detail.Trim()),
                ParseKnownGuid(finding.SectionId, knownSectionIds),
                ParseKnownGuid(finding.EntryId, knownEntryIds)))
            .Where((finding) =>
                !string.IsNullOrWhiteSpace(finding.Title) &&
                !string.IsNullOrWhiteSpace(finding.Detail))
            .ToArray();

        var selfCheckQuestions = (response.SelfCheckQuestions ?? [])
            .Where((question) => !string.IsNullOrWhiteSpace(question))
            .Select((question) => CvAiUserFacingText.StripIds(question.Trim()))
            .Where((question) => !string.IsNullOrWhiteSpace(question))
            .Take(3)
            .ToArray();

        return new CvQualityEvaluationDto(
            current.DocumentId,
            ClampScore(response.OverallScore),
            summary,
            dimensions,
            findings,
            selfCheckQuestions);
    }

    private static int ClampScore(int score) => Math.Clamp(score, 0, 100);

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
