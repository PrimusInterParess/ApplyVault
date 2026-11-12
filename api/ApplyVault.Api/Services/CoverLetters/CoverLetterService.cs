using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services;
using ApplyVault.Api.Services.InterviewPrep.Adapters;
using ApplyVault.Api.Services.InterviewPrep.Domain;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.CoverLetters;

public interface ICoverLetterService
{
    Task<CoverLetterDto> CreateAsync(
        AppUserEntity user,
        Guid scrapeResultId,
        CreateCoverLetterRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CoverLetterValidationException(string message) : Exception(message);

public sealed class CoverLetterService(
    IInterviewPrepCandidateContextAdapter candidateContext,
    IInterviewPrepJobContextAdapter jobContext,
    ICoverLetterAiClient aiClient,
    IScrapeResultStore scrapeResultStore) : ICoverLetterService
{
    public const int MaxNoteLength = 800;

    public async Task<CoverLetterDto> CreateAsync(
        AppUserEntity user,
        Guid scrapeResultId,
        CreateCoverLetterRequest request,
        CancellationToken cancellationToken = default)
    {
        var note = request.Note?.Trim() ?? string.Empty;
        if (note.Length > MaxNoteLength)
        {
            throw new CoverLetterValidationException($"Note must be {MaxNoteLength} characters or fewer.");
        }

        InterviewPrepCandidateSnapshot candidate;
        InterviewPrepJobSnapshot? job;
        try
        {
            candidate = await candidateContext.CaptureAsync(user, cancellationToken);
            job = await jobContext.CaptureAsync(user, scrapeResultId, cancellationToken);
        }
        catch (InterviewPrepValidationException ex)
        {
            throw new CoverLetterValidationException(MapContextError(ex.Message));
        }

        if (job is null || string.IsNullOrWhiteSpace(job.JobDescription))
        {
            throw new CoverLetterValidationException(
                "This saved job needs a description before a cover letter can be written.");
        }

        var letter = await aiClient.WriteAsync(
            new CoverLetterAiInput(candidate.SnapshotJson, job.SnapshotJson, note),
            cancellationToken);

        var updatedAt = await scrapeResultStore.SaveCoverLetterAsync(
            scrapeResultId,
            user.Id,
            letter,
            cancellationToken);

        if (updatedAt is null)
        {
            throw new CoverLetterValidationException("Saved job was not found.");
        }

        return new CoverLetterDto(scrapeResultId, letter, updatedAt.Value);
    }

    private static string MapContextError(string message)
    {
        if (message.Contains("Structured CV", StringComparison.Ordinal))
        {
            return "Create a Structured CV before writing a cover letter.";
        }

        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return "Saved job was not found.";
        }

        return message;
    }
}

public sealed record CoverLetterAiInput(string CvJson, string JobJson, string Note);

public interface ICoverLetterAiClient
{
    Task<string> WriteAsync(CoverLetterAiInput input, CancellationToken cancellationToken = default);
}

public sealed class GoogleAiCoverLetterClient(
    HttpClient httpClient,
    IOptions<GoogleAiOptions> googleAiOptions) : ICoverLetterAiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<string> WriteAsync(CoverLetterAiInput input, CancellationToken cancellationToken = default)
    {
        var options = googleAiOptions.Value;

        if (!options.Enabled)
        {
            throw new CoverLetterValidationException(
                "Google AI is disabled. Enable GoogleAi:Enabled to write a cover letter.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new CoverLetterValidationException("Google AI ApiKey is not configured.");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(options.TimeoutSeconds, 60)));

        var endpoint =
            $"https://generativelanguage.googleapis.com/v1beta/models/{options.Model}:generateContent?key={Uri.EscapeDataString(options.ApiKey)}";

        using var response = await httpClient.PostAsJsonAsync(
            endpoint,
            BuildRequest(input),
            SerializerOptions,
            timeoutCts.Token);

        var responsePayload = await response.Content.ReadAsStringAsync(timeoutCts.Token);
        if (!response.IsSuccessStatusCode)
        {
            throw new CoverLetterValidationException("The model could not write a cover letter. Try again.");
        }

        var generatedJson = ExtractGeneratedJson(responsePayload);
        var result = JsonSerializer.Deserialize<CoverLetterAiResponse>(generatedJson, SerializerOptions)
            ?? throw new CoverLetterValidationException("The model returned an empty cover letter.");

        var letter = result.Letter?.Trim() ?? string.Empty;
        if (letter.Length == 0)
        {
            throw new CoverLetterValidationException("The model returned an empty cover letter.");
        }

        return letter;
    }

    private static object BuildRequest(CoverLetterAiInput input)
    {
        var userPrompt = CoverLetterPrompt.UserPromptTemplate
            .Replace("{{cvJson}}", input.CvJson, StringComparison.Ordinal)
            .Replace("{{jobJson}}", input.JobJson, StringComparison.Ordinal)
            .Replace("{{note}}", string.IsNullOrWhiteSpace(input.Note) ? "(none)" : input.Note, StringComparison.Ordinal);

        return new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = CoverLetterPrompt.SystemInstruction } }
            },
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = userPrompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.4,
                responseMimeType = "application/json",
                responseSchema = new
                {
                    type = "OBJECT",
                    required = new[] { "letter" },
                    properties = new
                    {
                        letter = new { type = "STRING" }
                    }
                }
            }
        };
    }

    private static string ExtractGeneratedJson(string responsePayload)
    {
        var response = JsonSerializer.Deserialize<GenerateContentResponse>(responsePayload, SerializerOptions)
            ?? throw new CoverLetterValidationException("The model returned an unreadable response.");

        var text = response.Candidates?
            .SelectMany((candidate) => candidate.Content?.Parts ?? [])
            .Select((part) => part.Text)
            .FirstOrDefault((value) => !string.IsNullOrWhiteSpace(value));

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new CoverLetterValidationException("The model did not return a cover letter.");
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

    private sealed record CoverLetterAiResponse(string? Letter);

    private sealed record GenerateContentResponse(
        [property: JsonPropertyName("candidates")] IReadOnlyList<GenerateContentCandidate>? Candidates);

    private sealed record GenerateContentCandidate(
        [property: JsonPropertyName("content")] GenerateContentContent? Content);

    private sealed record GenerateContentContent(
        [property: JsonPropertyName("parts")] IReadOnlyList<GenerateContentPart>? Parts);

    private sealed record GenerateContentPart(
        [property: JsonPropertyName("text")] string? Text);
}
