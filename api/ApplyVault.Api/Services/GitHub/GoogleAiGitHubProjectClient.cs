using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public sealed class GoogleAiGitHubProjectClient(
    HttpClient httpClient,
    IOptions<GoogleAiOptions> googleAiOptions,
    IOptions<GitHubProjectAiOptions> projectAiOptions) : IGitHubProjectAiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<CvProjectSummaryResult> GenerateAsync(
        GitHubProjectAiInput input,
        CancellationToken cancellationToken = default)
    {
        var options = googleAiOptions.Value;

        if (!options.Enabled)
        {
            throw new InvalidOperationException("Google AI is disabled. Enable GoogleAi:Enabled to generate project summaries.");
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
            BuildRequest(input),
            SerializerOptions,
            timeoutCts.Token);

        var responsePayload = await response.Content.ReadAsStringAsync(timeoutCts.Token);
        response.EnsureSuccessStatusCode();

        var generatedJson = ExtractGeneratedJson(responsePayload);
        var result = JsonSerializer.Deserialize<CvProjectSummaryResult>(generatedJson, SerializerOptions)
            ?? throw new InvalidOperationException("Google AI returned an empty project summary payload.");

        if (!result.SufficientContext)
        {
            var refusalReason = result.RefusalReason?.Trim();

            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(refusalReason)
                    ? GitHubProjectSummaryEligibility.InsufficientDataMessage
                    : refusalReason);
        }

        if (!GitHubProjectSummaryEligibility.HasSufficientSummaryData(
                input.ReadmeText,
                input.Description,
                input.PrimaryLanguage,
                input.Topics))
        {
            throw new InvalidOperationException(GitHubProjectSummaryEligibility.InsufficientDataMessage);
        }

        if (GitHubProjectSummaryEligibility.IsWeakGeneratedSummary(result, input))
        {
            throw new InvalidOperationException(GitHubProjectSummaryEligibility.InsufficientDataMessage);
        }

        if (string.IsNullOrWhiteSpace(result.Title) || string.IsNullOrWhiteSpace(result.Summary))
        {
            throw new InvalidOperationException("Google AI returned an incomplete project summary.");
        }

        return result with
        {
            Bullets = result.Bullets?.Where((bullet) => !string.IsNullOrWhiteSpace(bullet)).ToArray() ?? [],
            TechStack = result.TechStack?.Trim() ?? string.Empty,
            RefusalReason = result.RefusalReason?.Trim() ?? string.Empty
        };
    }

    private object BuildRequest(GitHubProjectAiInput input)
    {
        var prompts = projectAiOptions.Value;
        var payloadJson = JsonSerializer.Serialize(input, SerializerOptions);

        return new
        {
            systemInstruction = new
            {
                parts = new[]
                {
                    new { text = prompts.SystemPrompt }
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
                    required = new[] { "sufficientContext", "title", "summary", "bullets", "techStack", "refusalReason" },
                    properties = new
                    {
                        sufficientContext = new
                        {
                            type = "BOOLEAN",
                            description = "True only when the repository has enough factual detail for a credible CV entry."
                        },
                        title = new
                        {
                            type = "STRING",
                            description = "Human-readable CV project title; reflect what the project does when supported by the source."
                        },
                        summary = new
                        {
                            type = "STRING",
                            description = "One or two sentences on purpose, what was built, and main technical or domain context."
                        },
                        bullets = new
                        {
                            type = "ARRAY",
                            description = "2-4 distinct, source-supported capability or technical bullets that do not repeat the summary.",
                            items = new { type = "STRING" }
                        },
                        techStack = new
                        {
                            type = "STRING",
                            description = "Comma-separated technologies explicitly evidenced in the README, description, topics, or primaryLanguage."
                        },
                        refusalReason = new
                        {
                            type = "STRING",
                            description = "When sufficientContext is false, briefly explain what factual detail is missing."
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
