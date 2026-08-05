using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services.InterviewPrep.Ai.Prompts;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.InterviewPrep.Ai;

/// <summary>
/// Gemini raw HTTP transport (ADR-0008). Live JSON generation for planning and in-session
/// operations listed in <c>SupportedOperations</c>; others return
/// <see cref="InterviewPrepAiErrorCodes.OperationNotImplemented"/>.
/// </summary>
public sealed class GoogleAiInterviewPrepTransport(
    HttpClient httpClient,
    IOptions<GoogleAiOptions> googleAiOptions,
    IOptions<InterviewPrepAiOptions> interviewPrepAiOptions) : IInterviewPrepAiProvider
{
    public const string Name = "google-ai-http";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly HashSet<InterviewPrepAiOperation> SupportedOperations =
    [
        InterviewPrepAiOperation.CreateInterviewBrief,
        InterviewPrepAiOperation.PlanInterview,
        InterviewPrepAiOperation.GenerateOpening,
        InterviewPrepAiOperation.AssessAnswer,
        InterviewPrepAiOperation.SelectNextAction,
        InterviewPrepAiOperation.GenerateInterviewerMessage,
        InterviewPrepAiOperation.PlanFullLoop
    ];

    public string ProviderName => Name;

    public async Task<InterviewPrepAiProviderResult> CompleteAsync(
        InterviewPrepPromptDefinition prompt,
        object request,
        CancellationToken cancellationToken = default)
    {
        var google = googleAiOptions.Value;
        var ai = interviewPrepAiOptions.Value;

        if (!ai.Enabled || !google.Enabled)
        {
            return Fail(
                InterviewPrepAiErrorCodes.Disabled,
                "Interview Prep AI / GoogleAi is disabled. Enable InterviewPrep:Ai:Enabled and GoogleAi:Enabled for live Gemini.");
        }

        if (string.IsNullOrWhiteSpace(google.ApiKey))
        {
            return Fail(
                InterviewPrepAiErrorCodes.ProviderUnavailable,
                "GoogleAi:ApiKey is not configured.");
        }

        if (!SupportedOperations.Contains(prompt.Operation))
        {
            return Fail(
                InterviewPrepAiErrorCodes.OperationNotImplemented,
                $"Live Gemini transport does not implement {prompt.Operation}. Use UseFakeProvider for stubbed operations.");
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var timeoutSeconds = ai.TimeoutSeconds ?? google.TimeoutSeconds;
            if (timeoutSeconds > 0)
            {
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            }

            var endpoint =
                $"https://generativelanguage.googleapis.com/v1beta/models/{google.Model}:generateContent?key={Uri.EscapeDataString(google.ApiKey)}";

            using var response = await httpClient.PostAsJsonAsync(
                endpoint,
                BuildRequest(prompt, request),
                SerializerOptions,
                timeoutCts.Token);

            var responsePayload = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                return Fail(
                    InterviewPrepAiErrorCodes.TransportFailed,
                    $"Gemini HTTP {(int)response.StatusCode}.");
            }

            var generatedJson = ExtractGeneratedJson(responsePayload);
            return new InterviewPrepAiProviderResult(true, generatedJson, null, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail(InterviewPrepAiErrorCodes.Timeout, "Gemini Interview Prep call timed out.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Fail(InterviewPrepAiErrorCodes.TransportFailed, ex.Message);
        }
    }

    private static object BuildRequest(InterviewPrepPromptDefinition prompt, object request)
    {
        var payloadJson = JsonSerializer.Serialize(request, SerializerOptions);
        var userText = prompt.UserPromptTemplate.Replace("{{payloadJson}}", payloadJson, StringComparison.Ordinal);

        return new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = prompt.SystemInstruction } }
            },
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = userText } }
                }
            },
            generationConfig = GoogleAiInterviewPrepResponseSchemas.For(prompt.Operation)
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

    private static InterviewPrepAiProviderResult Fail(string code, string message) =>
        new(false, null, code, message);

    private sealed record GenerateContentResponse(
        [property: JsonPropertyName("candidates")] IReadOnlyList<GenerateContentCandidate>? Candidates);

    private sealed record GenerateContentCandidate(
        [property: JsonPropertyName("content")] GenerateContentContent? Content);

    private sealed record GenerateContentContent(
        [property: JsonPropertyName("parts")] IReadOnlyList<GenerateContentPart>? Parts);

    private sealed record GenerateContentPart(
        [property: JsonPropertyName("text")] string? Text);
}
