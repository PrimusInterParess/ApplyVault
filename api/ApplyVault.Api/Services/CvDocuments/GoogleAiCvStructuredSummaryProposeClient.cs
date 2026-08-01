using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public sealed class GoogleAiCvStructuredSummaryProposeClient(
    HttpClient httpClient,
    IOptions<GoogleAiOptions> googleAiOptions,
    IOptions<CvSummaryProposeAiOptions> summaryProposeAiOptions) : ICvStructuredSummaryProposeAiClient
{
    internal const int MaxChangeBullets = 5;
    internal const int MaxBulletLength = 200;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<CvSummaryProposeAiResult> ProposeAsync(
        CvStructuredDocumentDto current,
        string? instructions,
        string? appUserDisplayName,
        string? appUserEmail,
        CancellationToken cancellationToken = default)
    {
        var options = googleAiOptions.Value;

        if (!options.Enabled)
        {
            throw new InvalidOperationException(
                "Google AI is disabled. Enable GoogleAi:Enabled to propose a CV summary.");
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
            BuildRequest(current, instructions, appUserDisplayName, appUserEmail),
            SerializerOptions,
            timeoutCts.Token);

        var responsePayload = await response.Content.ReadAsStringAsync(timeoutCts.Token);
        response.EnsureSuccessStatusCode();

        var generatedJson = ExtractGeneratedJson(responsePayload);
        var result = JsonSerializer.Deserialize<CvStructuredSummaryProposeAiResponse>(generatedJson, SerializerOptions)
            ?? throw new InvalidOperationException("Google AI returned an empty CV summary proposal payload.");

        return Normalize(result);
    }

    private object BuildRequest(
        CvStructuredDocumentDto current,
        string? instructions,
        string? appUserDisplayName,
        string? appUserEmail)
    {
        var prompts = summaryProposeAiOptions.Value;
        var payloadJson = JsonSerializer.Serialize(current, SerializerOptions);
        var currentSummary = ExtractCurrentSummaryText(current);
        var identityJson = JsonSerializer.Serialize(
            BuildIdentityContext(current, appUserDisplayName, appUserEmail),
            SerializerOptions);
        var instructionText = string.IsNullOrWhiteSpace(instructions)
            ? string.Empty
            : instructions.Trim();

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
                                .Replace("{{instructions}}", instructionText, StringComparison.Ordinal)
                                .Replace("{{currentSummary}}", currentSummary, StringComparison.Ordinal)
                                .Replace("{{identityJson}}", identityJson, StringComparison.Ordinal)
                                .Replace("{{payloadJson}}", payloadJson, StringComparison.Ordinal)
                        }
                    }
                }
            },
            generationConfig = GoogleAiCvStructuredSummaryProposeResponseSchema.Create()
        };
    }

    internal static CvSummaryProposeAiResult Normalize(CvStructuredSummaryProposeAiResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.ProposedSummaryText))
        {
            throw new InvalidOperationException("Google AI returned an empty proposed summary.");
        }

        var bullets = (response.ChangeBullets ?? [])
            .Where((bullet) => !string.IsNullOrWhiteSpace(bullet))
            .Select((bullet) => Truncate(bullet.Trim(), MaxBulletLength))
            .Take(MaxChangeBullets)
            .ToArray();

        if (bullets.Length == 0)
        {
            throw new InvalidOperationException("Google AI returned a summary proposal without change bullets.");
        }

        return new CvSummaryProposeAiResult(response.ProposedSummaryText.Trim(), bullets);
    }

    internal static string ExtractCurrentSummaryText(CvStructuredDocumentDto current)
    {
        var summarySection = current.Sections
            .Where((section) =>
                string.Equals(section.SectionType, CvSectionTypes.Summary, StringComparison.OrdinalIgnoreCase))
            .OrderBy((section) => section.SortOrder)
            .FirstOrDefault();

        if (summarySection is null)
        {
            return string.Empty;
        }

        var entry = summarySection.Entries
            .OrderBy((item) => item.SortOrder)
            .FirstOrDefault();

        return entry?.Summary?.Trim() ?? string.Empty;
    }

    internal static object BuildIdentityContext(
        CvStructuredDocumentDto current,
        string? appUserDisplayName,
        string? appUserEmail)
    {
        return new
        {
            appUser = new
            {
                displayName = NullIfWhiteSpace(appUserDisplayName),
                email = NullIfWhiteSpace(appUserEmail)
            },
            contact = BuildContactExcerpt(current),
            resolutionRule =
                "Prefer Contact name/email over AppUser when both present and they conflict; AppUser is fallback identity."
        };
    }

    private static object? BuildContactExcerpt(CvStructuredDocumentDto current)
    {
        var contactSection = current.Sections
            .Where((section) =>
                string.Equals(section.SectionType, CvSectionTypes.Contact, StringComparison.OrdinalIgnoreCase))
            .OrderBy((section) => section.SortOrder)
            .FirstOrDefault();

        if (contactSection is null)
        {
            return null;
        }

        var entry = contactSection.Entries
            .OrderBy((item) => item.SortOrder)
            .FirstOrDefault();

        if (entry is null)
        {
            return new
            {
                heading = contactSection.Heading,
                name = (string?)null,
                subtitle = (string?)null,
                lines = Array.Empty<string>()
            };
        }

        var lines = entry.Bullets
            .Where((line) => !string.IsNullOrWhiteSpace(line))
            .Select((line) => line.Trim())
            .ToArray();

        return new
        {
            heading = contactSection.Heading,
            name = NullIfWhiteSpace(entry.Title),
            subtitle = NullIfWhiteSpace(entry.Subtitle),
            lines
        };
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

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
