using System.Net;
using System.Text.Json;
using ApplyVault.Api.Models.InterviewPrep;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services.InterviewPrep;
using ApplyVault.Api.Services.InterviewPrep.Ai;
using ApplyVault.Api.Services.InterviewPrep.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApplyVault.Api.Tests.InterviewPrep;

public sealed class PreparationPackGeneratorTests
{
    [Fact]
    public async Task Live_transport_sends_structured_sources_and_validates_output()
    {
        using var handler = new Handler();
        using var http = new HttpClient(handler);
        var generator = Generator(http);
        var result = await generator.PlanAsync(Sources(), default);
        Assert.False(result.IsDemo);
        Assert.Equal("Role preparation", result.Plan.Title);
        Assert.True(handler.KeyHeaderPresent);
        Assert.DoesNotContain("secret-key", handler.RequestUri);
        using var payload = JsonDocument.Parse(handler.Payload!);
        var config = payload.RootElement.GetProperty("generationConfig");
        Assert.Equal("application/json", config.GetProperty("responseMimeType").GetString());
        Assert.True(config.GetProperty("responseSchema").GetProperty("properties").TryGetProperty("documents", out _));
        var studyProperties = config.GetProperty("responseSchema").GetProperty("properties").GetProperty("studyPlan").GetProperty("items").GetProperty("properties");
        Assert.Equal(14, studyProperties.GetProperty("day").GetProperty("maximum").GetInt32());
        Assert.Equal(60, studyProperties.GetProperty("minutes").GetProperty("maximum").GetInt32());
        var sourceJson = payload.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString();
        Assert.Contains("Product designer", sourceJson);
        Assert.Contains("Portfolio review", sourceJson);
        Assert.DoesNotContain("systemInstruction", JsonSerializer.Serialize(result.Plan));
    }

    [Fact]
    public async Task Incomplete_provider_output_retries_then_fails_without_a_demo_fallback()
    {
        using var handler = new Handler { FinishReason = "MAX_TOKENS" };
        using var http = new HttpClient(handler);
        var generator = Generator(http, retries: 1);
        await Assert.ThrowsAsync<InterviewPrepAiUnavailableException>(() => generator.PlanAsync(Sources(), default));
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task Token_limited_plans_retry_with_a_larger_budget_instead_of_repeating_truncation()
    {
        using var handler = new Handler { FinishReason = "MAX_TOKENS", StopOnLargerBudget = true };
        using var http = new HttpClient(handler);
        var result = await Generator(http, retries: 1).PlanAsync(Sources(), default);
        Assert.Equal("Role preparation", result.Plan.Title);
        Assert.Equal([32768, 65536], handler.RequestedOutputLimits);
    }

    [Fact]
    public async Task Disabled_live_generation_does_not_silently_return_demo_documents()
    {
        using var handler = new Handler();
        using var http = new HttpClient(handler);
        var generator = new GoogleAiPreparationPackGenerator(http, Microsoft.Extensions.Options.Options.Create(new GoogleAiOptions()),
            Microsoft.Extensions.Options.Options.Create(new InterviewPrepAiOptions { UseFakeProvider = false }), NullLogger<GoogleAiPreparationPackGenerator>.Instance);
        await Assert.ThrowsAsync<InterviewPrepAiUnavailableException>(() => generator.PlanAsync(Sources(), default));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Explicit_demo_is_labeled_and_does_not_call_external_services()
    {
        using var handler = new Handler();
        using var http = new HttpClient(handler);
        var generator = new GoogleAiPreparationPackGenerator(http, Microsoft.Extensions.Options.Options.Create(new GoogleAiOptions()),
            Microsoft.Extensions.Options.Options.Create(new InterviewPrepAiOptions { UseFakeProvider = true }), NullLogger<GoogleAiPreparationPackGenerator>.Instance);
        var result = await generator.PlanAsync(Sources(), default);
        Assert.True(result.IsDemo);
        Assert.Contains("not a personalized preparation pack", result.Plan.OverviewMarkdown);
        PreparationDiscoveryValidation.Plan(result.Plan, Sources().Input);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Caller_cancellation_is_propagated()
    {
        using var handler = new Handler();
        using var http = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Generator(http).PlanAsync(Sources(), cancellation.Token));
    }

    [Fact]
    public async Task Document_generation_sends_shared_uncertainty_but_writes_only_the_selected_document()
    {
        using var handler = new Handler { Result = new { markdown = InterviewPreparationPackTests.Content().Documents[0].Markdown } };
        using var http = new HttpClient(handler);
        var plan = InterviewPreparationPackTests.Plan(twoDocuments: true);
        var discovery = new PreparationDiscovery(plan.Documents, [new("foundations", "foundations-needs", "unknown", "I need a worked example.")], 1);
        var result = await Generator(http).WriteDocumentAsync(new(Sources(), PreparationDiscoveryValidation.DraftContent(plan), discovery, plan.Documents[0]), default);
        Assert.Contains("change a constraint", result.Markdown);
        using var request = JsonDocument.Parse(handler.Payload!);
        using var payload = JsonDocument.Parse(request.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString()!);
        Assert.Equal("foundations", payload.RootElement.GetProperty("document").GetProperty("id").GetString());
        Assert.Equal("unknown", payload.RootElement.GetProperty("discovery").GetProperty("answers")[0].GetProperty("status").GetString());
        Assert.False(payload.RootElement.GetProperty("otherDocuments")[0].TryGetProperty("markdown", out _));
        Assert.Equal(30, payload.RootElement.GetProperty("studySessions")[0].GetProperty("minutes").GetInt32());
    }

    [Fact]
    public async Task Follow_up_guidance_uses_the_user_request_and_unconfirmed_draft()
    {
        using var handler = new Handler { Result = new { markdown = "Start by finding a concrete task and describe what remains uncertain." } };
        using var http = new HttpClient(handler);
        var outline = InterviewPreparationPackTests.Plan().Documents[0];
        var help = new PreparationHelpRequest(DateTimeOffset.UtcNow, "I don't know how to do it.", "My tentative draft", "Previous guidance");
        var result = await Generator(http).HelpAsync(new(Sources(), outline, outline.Questions[0], [], help), default);
        Assert.False(result.IsDemo);
        Assert.Contains("concrete task", result.Markdown);
        using var request = JsonDocument.Parse(handler.Payload!);
        var payload = request.RootElement.GetProperty("contents")[0].GetProperty("parts")[0].GetProperty("text").GetString();
        Assert.Contains("My tentative draft", payload);
        Assert.Contains("Previous guidance", payload);
    }

    [Fact]
    public async Task Outline_without_practical_guidance_is_rejected()
    {
        var plan = InterviewPreparationPackTests.Plan();
        using var handler = new Handler { Result = plan with { Documents = [plan.Documents[0] with
        {
            Questions = [plan.Documents[0].Questions[0] with { GuidanceMarkdown = "" }]
        }] } };
        using var http = new HttpClient(handler);
        await Assert.ThrowsAsync<InterviewPrepAiUnavailableException>(() => Generator(http).PlanAsync(Sources(), default));
    }

    [Fact]
    public async Task Invalid_study_references_are_retried_with_specific_repair_guidance()
    {
        var plan = InterviewPreparationPackTests.Plan();
        using var handler = new Handler { FirstResult = plan with
        {
            StudyPlan = [plan.StudyPlan[0] with { DocumentIds = ["unknown-document"] }]
        } };
        using var http = new HttpClient(handler);
        var result = await Generator(http, retries: 1).PlanAsync(Sources(), default);
        Assert.Equal("Role preparation", result.Plan.Title);
        Assert.Equal(2, handler.Calls);
        Assert.Contains("REPAIR: Use exact ids from documents", handler.Payload);
        Assert.Equal([32768, 32768], handler.RequestedOutputLimits);
    }

    [Fact]
    public async Task Diagnostics_log_only_safe_finish_codes_and_never_provider_text_or_credentials()
    {
        const string privateMarker = "PRIVATE_GENERATED_CONTENT_DO_NOT_LOG";
        using var handler = new Handler { FinishReason = privateMarker };
        using var http = new HttpClient(handler);
        var logger = new CaptureLogger();
        var provider = new GoogleAiPreparationPackGenerator(http,
            Microsoft.Extensions.Options.Options.Create(new GoogleAiOptions { Enabled = true, ApiKey = "secret-key" }),
            Microsoft.Extensions.Options.Options.Create(new InterviewPrepAiOptions { Enabled = true, UseFakeProvider = false, MaxRetries = 0 }), logger);
        var error = await Assert.ThrowsAsync<InterviewPrepAiUnavailableException>(() => provider.PlanAsync(Sources(), default));
        Assert.Equal("incomplete_output", error.FailureCode);
        Assert.Contains(logger.Messages, m => m.Contains("finish=OTHER"));
        Assert.Contains(logger.Messages, m => m.Contains("code=incomplete_output"));
        Assert.DoesNotContain(logger.Messages, m => m.Contains(privateMarker) || m.Contains("secret-key"));
    }

    private static GoogleAiPreparationPackGenerator Generator(HttpClient http, int retries = 0) => new(http,
        Microsoft.Extensions.Options.Options.Create(new GoogleAiOptions { Enabled = true, ApiKey = "secret-key" }),
        Microsoft.Extensions.Options.Options.Create(new InterviewPrepAiOptions { Enabled = true, UseFakeProvider = false, MaxRetries = retries, RetryDelayMilliseconds = 0 }),
        NullLogger<GoogleAiPreparationPackGenerator>.Instance);

    private static PreparationPackSources Sources() => new(InterviewPreparationPackTests.Input() with
    {
        InterviewFormats = ["Portfolio review"]
    }, "{\"experience\":\"Design\"}", null, null, null, Guid.NewGuid(), []);

    private sealed class CaptureLogger : ILogger<GoogleAiPreparationPackGenerator>
    {
        internal readonly List<string> Messages = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    private sealed class Handler : HttpMessageHandler
    {
        internal object Result = InterviewPreparationPackTests.Plan();
        internal object? FirstResult;
        internal int Calls;
        internal string FinishReason = "STOP";
        internal bool StopOnLargerBudget;
        internal readonly List<int> RequestedOutputLimits = [];
        internal string? Payload;
        internal string? RequestUri;
        internal bool KeyHeaderPresent;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++; Payload = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var payload = JsonDocument.Parse(Payload);
            var limit = payload.RootElement.GetProperty("generationConfig").GetProperty("maxOutputTokens").GetInt32();
            RequestedOutputLimits.Add(limit);
            var finish = StopOnLargerBudget && limit > 32768 ? "STOP" : FinishReason;
            RequestUri = request.RequestUri!.ToString(); KeyHeaderPresent = request.Headers.Contains("x-goog-api-key");
            return new(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    candidates = new[] { new { finishReason = finish, content = new { parts = new[]
                    {
                        new { text = JsonSerializer.Serialize(Calls == 1 && FirstResult is not null ? FirstResult : Result, InterviewPreparationPackService.JsonOptions) }
                    } } } }
                }))
            };
        }
    }
}
