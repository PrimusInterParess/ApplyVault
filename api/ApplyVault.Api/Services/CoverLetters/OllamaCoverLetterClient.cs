using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.CoverLetters;

public sealed class OllamaCoverLetterClient(
    HttpClient httpClient,
    IOptions<CoverLetterAiOptions> coverLetterOptions) : ICoverLetterAiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<string> WriteAsync(CoverLetterAiInput input, CancellationToken cancellationToken = default)
    {
        var options = coverLetterOptions.Value;

        if (!options.Enabled)
        {
            throw new CoverLetterValidationException(
                "Cover letter generation is disabled. Enable CoverLetter:Enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.BaseUrl) || string.IsNullOrWhiteSpace(options.Model))
        {
            throw new CoverLetterValidationException("Cover letter model is not configured.");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));

        var endpoint = $"{options.BaseUrl.TrimEnd('/')}/api/chat";

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsJsonAsync(
                endpoint,
                BuildRequest(input, options.Model),
                SerializerOptions,
                timeoutCts.Token);
        }
        catch (HttpRequestException)
        {
            throw new CoverLetterValidationException(
                $"Could not reach Ollama at {options.BaseUrl}. Start Ollama with {options.Model}.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CoverLetterValidationException(
                $"Ollama did not finish the cover letter within {options.TimeoutSeconds} seconds.");
        }

        using (response)
        {
            var responsePayload = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw new CoverLetterValidationException(ReadOllamaError(responsePayload, options.Model));
            }

            var chat = JsonSerializer.Deserialize<OllamaChatResponse>(responsePayload, SerializerOptions)
                ?? throw new CoverLetterValidationException("Ollama returned an unreadable response.");

            var letter = ReadLetter(chat.Message?.Content);
            if (letter.Length == 0)
            {
                throw new CoverLetterValidationException("Ollama returned an empty cover letter.");
            }

            return letter;
        }
    }

    private static object BuildRequest(CoverLetterAiInput input, string model)
    {
        var userPrompt = CoverLetterPrompt.UserPromptTemplate
            .Replace("{{cvJson}}", input.CvJson, StringComparison.Ordinal)
            .Replace("{{jobJson}}", input.JobJson, StringComparison.Ordinal)
            .Replace("{{note}}", string.IsNullOrWhiteSpace(input.Note) ? "(none)" : input.Note, StringComparison.Ordinal);

        return new
        {
            model,
            stream = false,
            think = false,
            format = new
            {
                type = "object",
                required = new[] { "letter" },
                properties = new
                {
                    letter = new { type = "string" }
                }
            },
            messages = new object[]
            {
                new { role = "system", content = CoverLetterPrompt.SystemInstruction },
                new { role = "user", content = userPrompt }
            },
            options = new { temperature = 0.4 }
        };
    }

    private static string ReadLetter(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var normalized = content.Trim();
        if (normalized.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = normalized.IndexOf('\n');
            var lastFence = normalized.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewLine >= 0 && lastFence > firstNewLine)
            {
                normalized = normalized[(firstNewLine + 1)..lastFence].Trim();
            }
        }

        try
        {
            var result = JsonSerializer.Deserialize<CoverLetterAiResponse>(normalized, SerializerOptions);
            var letter = result?.Letter?.Trim();
            if (!string.IsNullOrWhiteSpace(letter))
            {
                return letter;
            }
        }
        catch (JsonException)
        {
            // Plain prose is still a letter when the model skips the schema.
        }

        return normalized.StartsWith('{') ? string.Empty : normalized;
    }

    private static string ReadOllamaError(string responsePayload, string model)
    {
        try
        {
            var error = JsonSerializer.Deserialize<OllamaErrorResponse>(responsePayload, SerializerOptions);
            if (!string.IsNullOrWhiteSpace(error?.Error))
            {
                return error.Error.Contains("not found", StringComparison.OrdinalIgnoreCase)
                    ? $"Ollama does not have {model}. Pull it and try again."
                    : error.Error.Trim();
            }
        }
        catch (JsonException)
        {
            // Fall through to the generic message.
        }

        return "The local model could not write a cover letter. Try again.";
    }

    private sealed record CoverLetterAiResponse(string? Letter);

    private sealed record OllamaChatResponse(
        [property: JsonPropertyName("message")] OllamaChatMessage? Message);

    private sealed record OllamaChatMessage(
        [property: JsonPropertyName("content")] string? Content);

    private sealed record OllamaErrorResponse(
        [property: JsonPropertyName("error")] string? Error);
}
