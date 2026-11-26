using System.Net.Http.Json;
using System.Text.Json;
using ApplyVault.Api.Models.InterviewPrep;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services.InterviewPrep.Domain;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.InterviewPrep.Ai;

public sealed record PreparationProjectSource(Guid Id, string Title, string Summary, string Bullets, string TechStack);
public sealed record PreparationPackSources(PreparationPackInput Input, string CvJson, string? JobJson,
    string? JobTitle, string? CompanyName, Guid CvDocumentId, IReadOnlyList<PreparationProjectSource> Projects);
public sealed record GeneratedPreparationPlan(PreparationDiscoveryPlan Plan, bool IsDemo);
public sealed record GeneratedDocumentOutline(PreparationDocumentOutline Outline, bool IsDemo);
public sealed record GeneratedPreparationDocument(string Markdown, bool IsDemo);
public sealed record PreparationDocumentContext(PreparationPackSources Sources, PreparationPackContent Pack,
    PreparationDiscovery Discovery, PreparationDocumentOutline Document);
public sealed record PreparationQuestionHelpContext(PreparationPackSources Sources, PreparationDocumentOutline Document,
    PreparationDiscoveryQuestion Question, IReadOnlyList<PreparationDiscoveryAnswer> SharedAnswers, PreparationHelpRequest Help);

public interface IInterviewPreparationPackGenerator
{
    Task<GeneratedPreparationPlan> PlanAsync(PreparationPackSources sources, CancellationToken ct);
    Task<GeneratedDocumentOutline> PrepareDocumentAsync(PreparationPackSources sources, PreparationDocument document,
        PreparationDiscovery? discovery, CancellationToken ct);
    Task<GeneratedPreparationDocument> WriteDocumentAsync(PreparationDocumentContext context, CancellationToken ct);
    Task<PreparationHelpResponse> HelpAsync(PreparationQuestionHelpContext context, CancellationToken ct);
}

public sealed class GoogleAiPreparationPackGenerator(
    HttpClient http,
    IOptions<GoogleAiOptions> googleOptions,
    IOptions<InterviewPrepAiOptions> packOptions,
    ILogger<GoogleAiPreparationPackGenerator> logger) : IInterviewPreparationPackGenerator
{
    public async Task<GeneratedPreparationPlan> PlanAsync(PreparationPackSources sources, CancellationToken ct)
    {
        if (packOptions.Value.UseFakeProvider) return new(DemoPlan(sources.Input), true);
        var plan = await CompleteAsync<PreparationDiscoveryPlan>(nameof(PlanAsync), InterviewPreparationPackPrompt.Plan, sources,
            InterviewPreparationPackPrompt.PlanSchema(sources.Input), 32768, p => PreparationDiscoveryValidation.Plan(p, sources.Input), ct);
        return new(plan, false);
    }

    public async Task<GeneratedDocumentOutline> PrepareDocumentAsync(PreparationPackSources sources,
        PreparationDocument document, PreparationDiscovery? discovery, CancellationToken ct)
    {
        if (packOptions.Value.UseFakeProvider)
            return new(new(document.Id, document.Title, document.Purpose, document.PrerequisiteIds, [DemoQuestion(document.Id)]), true);
        var outline = await CompleteAsync<PreparationDocumentOutline>(nameof(PrepareDocumentAsync), InterviewPreparationPackPrompt.Questions,
            new { sources, document = new { document.Id, document.Title, document.Purpose, document.PrerequisiteIds }, discovery },
            InterviewPreparationPackPrompt.DocumentOutlineSchema(), 8192,
            p => PreparationDiscoveryValidation.DocumentOutline(p, document), ct);
        return new(outline, false);
    }

    public async Task<GeneratedPreparationDocument> WriteDocumentAsync(PreparationDocumentContext context, CancellationToken ct)
    {
        if (packOptions.Value.UseFakeProvider) return new(DemoMarkdown, true);
        // Other document bodies are omitted: the outline supplies scope and saved answers supply shared facts.
        var payload = new
        {
            context.Sources, context.Document, context.Discovery,
            packOverview = context.Pack.OverviewMarkdown,
            knownUnknowns = context.Pack.Unknowns,
            relevantTopics = context.Pack.Topics.Where(t => t.DocumentId == context.Document.Id),
            studySessions = context.Pack.StudyPlan.Where(s => s.DocumentIds.Contains(context.Document.Id)),
            otherDocuments = context.Pack.Documents.Where(d => d.Id != context.Document.Id)
                .Select(d => new { d.Id, d.Title, d.Purpose })
        };
        var result = await CompleteAsync<MarkdownResult>(nameof(WriteDocumentAsync), InterviewPreparationPackPrompt.Document, payload,
            InterviewPreparationPackPrompt.MarkdownSchema(), 16384, result =>
            {
                var documents = context.Pack.Documents.Select(d => d.Id == context.Document.Id
                    ? d with { Markdown = result.Markdown, IsGenerated = true } : d).ToArray();
                InterviewPreparationPackValidation.Content(context.Pack with { Documents = documents }, context.Sources.Input, allowPending: true);
            }, ct);
        return new(result.Markdown, false);
    }

    public async Task<PreparationHelpResponse> HelpAsync(PreparationQuestionHelpContext context, CancellationToken ct)
    {
        if (packOptions.Value.UseFakeProvider)
            return new("Demo guidance: start with one concrete situation. Describe what you remember, what you personally did, and what remains uncertain. Look at your own notes, a ticket, or an example you can reproduce. If you still cannot establish the answer, choose **I don't know** or **Skip**. A blank structure is: ‘The situation was ___. My part was ___. I am unsure about ___.’ This is guidance, not a statement of your experience.", true);
        var result = await CompleteAsync<MarkdownResult>(nameof(HelpAsync), InterviewPreparationPackPrompt.Help, context,
            InterviewPreparationPackPrompt.MarkdownSchema(), 4096, result =>
            {
                if (string.IsNullOrWhiteSpace(result.Markdown) || result.Markdown.Length > 12000)
                    throw new InterviewPrepAiUnavailableException("The guidance was incomplete. Please try again.");
            }, ct);
        return new(result.Markdown);
    }

    private async Task<T> CompleteAsync<T>(string operationName, string operation, object payload, object schema, int maxTokens, Action<T> validate, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var options = packOptions.Value;
        var google = googleOptions.Value;
        if (!options.Enabled || !google.Enabled || string.IsNullOrWhiteSpace(google.ApiKey))
            throw new InterviewPrepAiUnavailableException("Preparation generation is not configured. Please try again once the AI service is available.");
        var lastFailure = "unknown";
        for (var attempt = 0; attempt <= options.MaxRetries; attempt++)
        {
            var failure = "provider_transport";
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds ?? google.TimeoutSeconds));
                var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(google.Model)}:generateContent";
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Add("x-goog-api-key", google.ApiKey);
                request.Content = JsonContent.Create(new
                {
                    systemInstruction = new { parts = new[] { new { text = InterviewPreparationPackPrompt.System + "\n\n" + operation + RetryHint(lastFailure) } } },
                    contents = new[] { new { parts = new[] { new { text = JsonSerializer.Serialize(payload, InterviewPreparationPackService.JsonOptions) } } } },
                    generationConfig = new { responseMimeType = "application/json", responseSchema = schema, maxOutputTokens = maxTokens }
                });
                using var response = await http.SendAsync(request, timeout.Token);
                if (!response.IsSuccessStatusCode)
                {
                    failure = $"provider_http_{(int)response.StatusCode}";
                    throw new HttpRequestException($"Preparation provider returned HTTP {(int)response.StatusCode}.");
                }
                failure = "provider_json";
                var responseJson = await response.Content.ReadFromJsonAsync<JsonElement>(timeout.Token);
                logger.LogInformation("Preparation response {Operation} attempt={Attempt} finish={Finish} outputTokens={OutputTokens} thoughtsTokens={ThoughtsTokens}",
                    operationName, attempt + 1, FinishReason(responseJson), TokenCount(responseJson, "candidatesTokenCount"), TokenCount(responseJson, "thoughtsTokenCount"));
                failure = "missing_candidate";
                var candidate = responseJson.GetProperty("candidates")[0];
                if (candidate.TryGetProperty("finishReason", out var finish) && finish.GetString() != "STOP")
                {
                    failure = finish.GetString() == "MAX_TOKENS" ? "output_limit" : "incomplete_output";
                    throw new JsonException("The provider did not finish the response.");
                }
                failure = "missing_content";
                var json = string.Concat(candidate.GetProperty("content").GetProperty("parts").EnumerateArray()
                    .Where(p => p.TryGetProperty("text", out _) && !(p.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True))
                    .Select(p => p.GetProperty("text").GetString()));
                if (json.Length > 400000) throw new JsonException("The preparation response is too large.");
                failure = "generated_json";
                var result = JsonSerializer.Deserialize<T>(json, InterviewPreparationPackService.JsonOptions)
                    ?? throw new JsonException("The preparation response was empty.");
                failure = "contract_validation";
                validate(result);
                return result;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException
                or KeyNotFoundException or IndexOutOfRangeException or OperationCanceledException or InterviewPrepAiUnavailableException)
            {
                if (ex is InterviewPrepAiUnavailableException invalid && invalid.FailureCode is not null)
                    failure = invalid.FailureCode;
                if (ex is OperationCanceledException) failure = "provider_timeout";
                lastFailure = failure;
                // Codes and token counts only; never exception text or generated/private content.
                logger.LogWarning("Preparation request {Operation} attempt={Attempt} failed code={FailureCode} type={FailureType}",
                    operationName, attempt + 1, failure, ex.GetType().Name);
                if (attempt < options.MaxRetries)
                {
                    if (failure == "output_limit") maxTokens = Math.Min(maxTokens * 2, 65536);
                    await Task.Delay(options.RetryDelayMilliseconds, ct);
                }
            }
        }
        throw new InterviewPrepAiUnavailableException("The AI service could not complete preparation. Saved documents and answers were preserved. Please try again.") { FailureCode = lastFailure };
    }

    private static string RetryHint(string failure) => failure switch
    {
        "study_document_reference" or "topic_reference" or "document_link" or "unscheduled_document" =>
            "\nREPAIR: Use exact ids from documents in every topic.documentId and studyPlan.documentIds. Do not use filenames or section names as ids. Schedule every document at least once. Markdown links alone use id.md.",
        "study_day_range" or "duplicate_study_day" or "study_minutes_range" or "study_session" or "study_activity" =>
            "\nREPAIR: Number study days uniquely from 1 to preparationDays. Each day has one session, positive minutes no greater than minutesPerDay, concrete activity, and existing document ids.",
        "study_prerequisite_order" or "prerequisite_order" =>
            "\nREPAIR: Put prerequisites before dependent documents in the document array and study schedule. For shared sessions, order documentIds so prerequisites come first.",
        "question_id" or "question_count" or "question_guidance" or "question_kind" or "discovery_structure" =>
            "\nREPAIR: Give each document 1-3 questions with globally unique ids beginning with document-id-. Each needs kind knowledge/experience/preference, prompt, whyItMatters, guidanceMarkdown, blank answerStarter, and hypotheticalExample.",
        "output_limit" => "\nREPAIR: The previous output hit its token limit. Return the complete JSON with concise overview, guidance, and study activities; do not write document bodies during planning.",
        "generated_json" => "\nREPAIR: Return complete valid JSON matching the response schema. No Markdown fences, prose prefix, or trailing text.",
        _ => ""
    };

    private static string FinishReason(JsonElement response)
    {
        if (response.ValueKind != JsonValueKind.Object || !response.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0)
            return "NO_CANDIDATE";
        if (candidates[0].ValueKind != JsonValueKind.Object || !candidates[0].TryGetProperty("finishReason", out var finish) || finish.ValueKind != JsonValueKind.String) return "MISSING";
        return finish.GetString() switch
        {
            "STOP" => "STOP", "MAX_TOKENS" => "MAX_TOKENS", "SAFETY" => "SAFETY",
            "RECITATION" => "RECITATION", "BLOCKLIST" => "BLOCKLIST", "PROHIBITED_CONTENT" => "PROHIBITED_CONTENT",
            _ => "OTHER"
        };
    }

    private static int? TokenCount(JsonElement response, string name) => response.ValueKind == JsonValueKind.Object && response.TryGetProperty("usageMetadata", out var usage)
        && usage.ValueKind == JsonValueKind.Object && usage.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var count) ? count : null;

    private sealed record MarkdownResult(string Markdown);

    private static PreparationDiscoveryQuestion DemoQuestion(string documentId) => new(documentId + "-needs", "knowledge",
        "What would you like to understand or demonstrate after studying this document?",
        "Your answer helps choose the explanation depth and the kind of practice.",
        "Pick one task you would like to handle. Describe what you can already do and the first step where you get stuck. If you have never tried it, say so. You can also choose **I don't know** or **Skip**.",
        "I want to be able to ___. I can already ___. I get stuck when ___.",
        "Hypothetical example: a learner can explain a process but struggles to adapt it when a requirement changes. This is an invented example, not a fact about you.");

    private static PreparationDiscoveryPlan DemoPlan(PreparationPackInput input) => new(
        "Preparation pack preview",
        "This is a demo outline, not a personalized preparation pack. Open the document, answer its clarification question or mark it unknown, and write the document to preview the guided flow. Live AI generation must be configured for personalized teaching.",
        ["Demo content does not analyze your sources or assess your ability."],
        [new("Preparation method", "Plan study around the role and available time.", "No candidate ability has been assessed in this demo.", "preparation-method")],
        [new("preparation-method", "Study, demonstrate, and adapt", "Preview the guided preparation experience.", [], [DemoQuestion("preparation-method")])],
        [new(1, Math.Min(15, input.MinutesPerDay), "Recall a requirement, study, attempt, check, explain, and record the next weakness.", ["preparation-method"])]);

    private const string DemoMarkdown = """
        This is a demo document, not a personalized analysis of your experience.

        Start with one requirement from your target role. Write down what you already know, the evidence you can point to, and what remains uncertain. A skill on your CV is a starting point, not proof that you can apply it to a new problem.

        Choose one concrete situation. Describe its constraint, explain one approach, compare an alternative, and identify what could fail. Keep actual experience separate from hypothetical practice. If you cannot support a contribution or outcome, leave it unknown rather than filling the gap with an invented achievement.

        ## Try it independently

        Close the notes. Explain the concept, apply it to a small example, and change one requirement. Check what still works and record the first uncertainty you encounter. A live document supplies explanations and exercises informed by your saved answers, including foundations when you report that you do not know a concept.

        ## Review your evidence

        Track reading separately from explaining, demonstrating, and adapting. Record the next weakness in your review log. This preview contains no validated reference implementation and makes no readiness claim.
        """;
}
