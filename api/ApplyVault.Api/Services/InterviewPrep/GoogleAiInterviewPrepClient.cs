using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public sealed class GoogleAiInterviewPrepClient(
    HttpClient httpClient,
    IOptions<GoogleAiOptions> googleAiOptions,
    IOptions<InterviewPrepAiOptions> interviewPrepAiOptions) : IInterviewPrepAiClient
{
    internal static readonly string[] ScorecardDimensionIds =
    [
        "clarity",
        "evidence",
        "structure",
        "role_fit",
        "language"
    ];

    private static readonly HashSet<string> KnownPhases = new(StringComparer.OrdinalIgnoreCase)
    {
        "interview",
        "debrief"
    };

    private static readonly HashSet<string> KnownLanguageMixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "en",
        "da",
        "mixed"
    };

    private static readonly HashSet<string> KnownHiringMarkets = new(StringComparer.OrdinalIgnoreCase)
    {
        "general",
        "dk"
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<InterviewPrepAiTurnResult> GenerateTurnAsync(
        InterviewPrepAiTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.StructuredCv);

        var options = googleAiOptions.Value;
        var prepOptions = interviewPrepAiOptions.Value;

        if (!options.Enabled)
        {
            throw new InvalidOperationException(
                "Google AI is disabled. Enable GoogleAi:Enabled to run Interview Prep turns.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException("Google AI ApiKey is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            throw new InvalidOperationException("Google AI Model is not configured.");
        }

        var timeoutSeconds = prepOptions.TimeoutSeconds ?? options.TimeoutSeconds;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var endpoint =
            $"https://generativelanguage.googleapis.com/v1beta/models/{options.Model}:generateContent?key={Uri.EscapeDataString(options.ApiKey)}";

        using var response = await httpClient.PostAsJsonAsync(
            endpoint,
            BuildRequest(request, prepOptions),
            SerializerOptions,
            timeoutCts.Token);

        var responsePayload = await response.Content.ReadAsStringAsync(timeoutCts.Token);
        response.EnsureSuccessStatusCode();

        var generatedJson = ExtractGeneratedJson(responsePayload);
        var parsed = JsonSerializer.Deserialize<InterviewPrepAiRawResponse>(generatedJson, SerializerOptions)
            ?? throw new InvalidOperationException("Google AI returned an empty Interview Prep payload.");

        return Normalize(parsed);
    }

    private static object BuildRequest(InterviewPrepAiTurnRequest request, InterviewPrepAiOptions prepOptions)
    {
        var mode = request.Mode?.Trim()
            ?? throw new InvalidOperationException("Interview Prep mode is required.");
        if (string.IsNullOrWhiteSpace(mode))
        {
            throw new InvalidOperationException("Interview Prep mode is required.");
        }

        var languageMix = ResolveLanguageMix(request.LanguageMix, prepOptions.DefaultLanguageMix);
        var hiringMarket = ResolveHiringMarket(request.HiringMarket, prepOptions.DefaultHiringMarket);
        var userMessage = Truncate(
            (request.UserMessage ?? string.Empty).Trim(),
            prepOptions.MaxUserMessageChars);

        if (string.IsNullOrWhiteSpace(userMessage))
        {
            throw new InvalidOperationException("Interview Prep userMessage must be non-empty.");
        }

        var priorTurns = NormalizePriorTurns(request.PriorTurns, prepOptions);
        var cvJson = JsonSerializer.Serialize(request.StructuredCv, SerializerOptions);
        var jobJson = request.Job is null
            ? "null"
            : JsonSerializer.Serialize(request.Job, SerializerOptions);
        var priorTurnsJson = JsonSerializer.Serialize(priorTurns, SerializerOptions);
        var alreadyAskedJson = JsonSerializer.Serialize(
            request.AlreadyAsked ?? Array.Empty<string>(),
            SerializerOptions);

        var userPrompt = prepOptions.UserPromptTemplate
            .Replace("{{mode}}", mode, StringComparison.Ordinal)
            .Replace("{{languageMix}}", languageMix, StringComparison.Ordinal)
            .Replace("{{hiringMarket}}", hiringMarket, StringComparison.Ordinal)
            .Replace("{{userMessage}}", userMessage, StringComparison.Ordinal)
            .Replace("{{priorTurnsJson}}", priorTurnsJson, StringComparison.Ordinal)
            .Replace("{{alreadyAskedJson}}", alreadyAskedJson, StringComparison.Ordinal)
            .Replace("{{jobJson}}", jobJson, StringComparison.Ordinal)
            .Replace("{{cvJson}}", cvJson, StringComparison.Ordinal);

        if (!string.IsNullOrWhiteSpace(request.CorrectiveNudge))
        {
            userPrompt = userPrompt + "\n\n" + request.CorrectiveNudge.Trim();
        }

        return new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = prepOptions.SystemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new
                        {
                            text = userPrompt
                        }
                    }
                }
            },
            generationConfig = GoogleAiInterviewPrepResponseSchema.Create()
        };
    }

    internal static InterviewPrepAiTurnResult Normalize(InterviewPrepAiRawResponse response)
    {
        if (string.IsNullOrWhiteSpace(response.CoachMessage))
        {
            throw new InvalidOperationException("Google AI returned an Interview Prep turn without coachMessage.");
        }

        if (response.Inference is null)
        {
            throw new InvalidOperationException("Google AI returned an Interview Prep turn without inference.");
        }

        var phase = NormalizePhase(response.Phase);
        var inference = NormalizeInference(response.Inference);
        var followUps = NormalizeStringList(response.FollowUps);
        var debriefBullets = NormalizeStringList(response.DebriefBullets);
        var scorecard = response.Scorecard is null
            ? null
            : NormalizeScorecard(response.Scorecard);
        var modelAnswer = NormalizeModelAnswer(response.ModelAnswer, phase);

        return new InterviewPrepAiTurnResult(
            phase,
            inference,
            response.CoachMessage.Trim(),
            scorecard,
            followUps,
            debriefBullets,
            modelAnswer);
    }

    private static string? NormalizeModelAnswer(string? modelAnswer, string phase)
    {
        if (string.Equals(phase, "debrief", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(modelAnswer))
        {
            return null;
        }

        return Truncate(modelAnswer.Trim(), 4_000);
    }

    internal static InterviewPrepAiScorecard NormalizeScorecard(InterviewPrepAiRawScorecard scorecard)
    {
        var overall = ClampScore(scorecard.Overall);
        var summary = string.IsNullOrWhiteSpace(scorecard.Summary) ? null : scorecard.Summary.Trim();

        var byId = (scorecard.Dimensions ?? [])
            .Where((dimension) =>
                !string.IsNullOrWhiteSpace(dimension.Id) &&
                ScorecardDimensionIds.Contains(dimension.Id.Trim(), StringComparer.OrdinalIgnoreCase))
            .GroupBy((dimension) => dimension.Id!.Trim().ToLowerInvariant())
            .ToDictionary(
                (group) => group.Key,
                (group) =>
                {
                    var dimension = group.First();
                    var note = string.IsNullOrWhiteSpace(dimension.Note)
                        ? "No dimension note returned."
                        : dimension.Note.Trim();
                    return new InterviewPrepAiScorecardDimension(
                        group.Key,
                        ClampScore(dimension.Score),
                        note);
                });

        var dimensions = ScorecardDimensionIds
            .Select((id) =>
                byId.TryGetValue(id, out var dimension)
                    ? dimension
                    : new InterviewPrepAiScorecardDimension(
                        id,
                        overall,
                        "No dimension note returned."))
            .ToArray();

        return new InterviewPrepAiScorecard(overall, summary, dimensions);
    }

    private static InterviewPrepAiInference NormalizeInference(InterviewPrepAiRawInference inference) =>
        new(
            string.IsNullOrWhiteSpace(inference.Role) ? "unknown" : inference.Role.Trim(),
            string.IsNullOrWhiteSpace(inference.Seniority) ? "unknown" : inference.Seniority.Trim(),
            string.IsNullOrWhiteSpace(inference.InterviewStyle) ? "unknown" : inference.InterviewStyle.Trim(),
            inference.IsTechnicalContext ?? false);

    private static string NormalizePhase(string? phase)
    {
        if (!string.IsNullOrWhiteSpace(phase) && KnownPhases.Contains(phase))
        {
            return phase.Trim().ToLowerInvariant();
        }

        return "interview";
    }

    private static IReadOnlyList<string> NormalizeStringList(IReadOnlyList<string>? values) =>
        (values ?? [])
            .Where((value) => !string.IsNullOrWhiteSpace(value))
            .Select((value) => value.Trim())
            .ToArray();

    private static string ResolveLanguageMix(string? languageMix, string defaultLanguageMix)
    {
        var candidate = string.IsNullOrWhiteSpace(languageMix) ? defaultLanguageMix : languageMix.Trim();
        if (string.Equals(candidate, "en+da", StringComparison.OrdinalIgnoreCase))
        {
            return "mixed";
        }

        if (KnownLanguageMixes.Contains(candidate))
        {
            return candidate.ToLowerInvariant();
        }

        if (KnownLanguageMixes.Contains(defaultLanguageMix))
        {
            return defaultLanguageMix.Trim().ToLowerInvariant();
        }

        return "en";
    }

    private static string ResolveHiringMarket(string? hiringMarket, string defaultHiringMarket)
    {
        var candidate = string.IsNullOrWhiteSpace(hiringMarket)
            ? defaultHiringMarket
            : hiringMarket.Trim();

        if (KnownHiringMarkets.Contains(candidate))
        {
            return candidate.ToLowerInvariant();
        }

        if (KnownHiringMarkets.Contains(defaultHiringMarket))
        {
            return defaultHiringMarket.Trim().ToLowerInvariant();
        }

        return "general";
    }

    private static IReadOnlyList<object> NormalizePriorTurns(
        IReadOnlyList<InterviewPrepPriorTurn>? priorTurns,
        InterviewPrepAiOptions prepOptions)
    {
        if (priorTurns is null || priorTurns.Count == 0)
        {
            return [];
        }

        return priorTurns
            .Where((turn) =>
                !string.IsNullOrWhiteSpace(turn.Role) &&
                !string.IsNullOrWhiteSpace(turn.Text) &&
                (string.Equals(turn.Role, "user", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(turn.Role, "coach", StringComparison.OrdinalIgnoreCase)))
            .TakeLast(Math.Max(1, prepOptions.MaxPriorTurns))
            .Select((turn) => (object)new
            {
                role = turn.Role.Trim().ToLowerInvariant(),
                text = Truncate(turn.Text.Trim(), prepOptions.MaxPriorTurnChars),
                phase = NormalizePhase(turn.Phase)
            })
            .ToArray();
    }

    private static int ClampScore(int score) => Math.Clamp(score, 0, 100);

    private static string Truncate(string value, int maxChars)
    {
        if (maxChars <= 0 || value.Length <= maxChars)
        {
            return value;
        }

        return value[..maxChars];
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
