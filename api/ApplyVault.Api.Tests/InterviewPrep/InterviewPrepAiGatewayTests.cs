using ApplyVault.Api.Options;
using ApplyVault.Api.Services.InterviewPrep.Ai;
using ApplyVault.Api.Services.InterviewPrep.Ai.Contracts;
using ApplyVault.Api.Services.InterviewPrep.Ai.Prompts;
using ApplyVault.Api.Services.InterviewPrep.Ai.Validation;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Tests.InterviewPrep;

public sealed class InterviewPrepAiGatewayTests
{
    private static readonly InterviewPrepAiSessionConfig DefaultConfig = new(
        "screeningAndMotivation",
        "recruiter",
        "english",
        "general",
        "realisticSimulation",
        "text");

    [Fact]
    public void Prompt_registry_covers_all_named_operations_with_versions()
    {
        var registry = new InterviewPrepPromptRegistry();
        var operations = Enum.GetValues<InterviewPrepAiOperation>();

        Assert.Equal(operations.Length, registry.List().Count);

        foreach (var operation in operations)
        {
            var prompt = registry.Get(operation);
            Assert.Equal(operation, prompt.Operation);
            Assert.False(string.IsNullOrWhiteSpace(prompt.Name));
            Assert.False(string.IsNullOrWhiteSpace(prompt.Version));
            Assert.Contains("UNTRUSTED SOURCE DOCUMENTS", prompt.SystemInstruction, StringComparison.Ordinal);
            Assert.Contains("{{payloadJson}}", prompt.UserPromptTemplate, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Fake_provider_implements_all_operations_deterministically()
    {
        var gateway = CreateGateway(new FakeDeterministicInterviewPrepAiProvider());
        var brief = await gateway.CreateInterviewBriefAsync(new CreateInterviewBriefRequest(
            DefaultConfig,
            new InterviewPrepAiDocumentSnapshot("CV", "Built APIs for billing."),
            new InterviewPrepAiDocumentSnapshot("Engineer", "Backend role.")));

        Assert.True(brief.Succeeded);
        Assert.NotNull(brief.Value);
        Assert.Equal("interview-prep.create-interview-brief", brief.Meta.PromptName);
        Assert.Equal("2026-08-05.3", brief.Meta.PromptVersion);
        Assert.False(brief.Meta.UsedFallback);

        var plan = await gateway.PlanInterviewAsync(new PlanInterviewRequest(DefaultConfig, brief.Value!));
        Assert.True(plan.Succeeded);
        Assert.NotEmpty(plan.Value!.Stages);

        var opening = await gateway.GenerateOpeningAsync(new GenerateOpeningRequest(
            DefaultConfig, "Alex", "Backend Engineer", "Acme"));
        Assert.True(opening.Succeeded);
        Assert.Contains("Alex", opening.Value!.MessageText, StringComparison.Ordinal);

        var assess = await gateway.AssessAnswerAsync(new AssessAnswerRequest(
            "Tell me about a challenge.",
            "I led a migration that cut latency by measuring p95 and coordinating two teams over six weeks.",
            "problem_solving",
            new InterviewPrepAiDocumentSnapshot("CV", "API work"),
            null));
        Assert.True(assess.Succeeded);
        Assert.InRange(assess.Value!.Score, 0, 100);
        Assert.NotEmpty(assess.Value.Evidence);

        var next = await gateway.SelectNextActionAsync(new SelectNextActionRequest(
            DefaultConfig,
            "core",
            [new InterviewPrepAiTurnSnippet("interviewer", "Q1"), new InterviewPrepAiTurnSnippet("candidate", "A1")],
            ["problem_solving"],
            ["ownership"]));
        Assert.True(next.Succeeded);
        Assert.Equal("ask_question", next.Value!.ActionType);
        Assert.Equal("ownership", next.Value.CompetencyId);

        var message = await gateway.GenerateInterviewerMessageAsync(new GenerateInterviewerMessageRequest(
            DefaultConfig,
            "ask_question",
            "ownership",
            "ownership",
            [],
            []));
        Assert.True(message.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(message.Value!.MessageText));

        var summary = await gateway.SummarizeConversationAsync(new SummarizeConversationRequest(
        [
            new InterviewPrepAiTurnSnippet("interviewer", "Q"),
            new InterviewPrepAiTurnSnippet("candidate", "A")
        ]));
        Assert.True(summary.Succeeded);

        var stage = await gateway.EvaluateStageAsync(new EvaluateStageRequest(
            "core",
            "Probe ownership",
            [new InterviewPrepAiTurnSnippet("candidate", "I owned the rollout.")]));
        Assert.True(stage.Succeeded);

        var feedback = await gateway.GenerateFeedbackAsync(new GenerateFeedbackRequest(
            DefaultConfig,
            summary.Value!.Summary,
            ["clear structure"],
            ["add metrics"]));
        Assert.True(feedback.Succeeded);

        var compare = await gateway.CompareAnswerRetryAsync(new CompareAnswerRetryRequest(
            "Challenge?",
            "Short.",
            "I led a six-week migration with measured p95 improvements and clear stakeholder updates.",
            "problem_solving"));
        Assert.True(compare.Succeeded);
        Assert.True(compare.Value!.Improved);

        var stories = await gateway.ExtractProfessionalStoryAsync(new ExtractProfessionalStoryRequest(
            new InterviewPrepAiDocumentSnapshot("CV", "Owned billing APIs and reduced incidents.")));
        Assert.True(stories.Succeeded);
        Assert.NotEmpty(stories.Value!.Stories);

        var loop = await gateway.PlanFullLoopAsync(new PlanFullLoopRequest(DefaultConfig, brief.Value!));
        Assert.True(loop.Succeeded);
        Assert.True(loop.Value!.Stages.Count >= 3);

        var debrief = await gateway.GeneratePanelDebriefAsync(new GeneratePanelDebriefRequest(
            DefaultConfig,
            summary.Value.Summary,
            ["clear structure"],
            ["add metrics"]));
        Assert.True(debrief.Succeeded);
        Assert.NotEmpty(debrief.Value!.Perspectives);
    }

    [Fact]
    public async Task Gateway_retries_invalid_structured_output_then_succeeds()
    {
        var fake = new FakeDeterministicInterviewPrepAiProvider
        {
            ForceInvalidJsonOnceFor = InterviewPrepAiOperation.AssessAnswer
        };
        var gateway = CreateGateway(fake, new InterviewPrepAiOptions
        {
            Enabled = true,
            UseFakeProvider = true,
            MaxRetries = 1,
            RetryDelayMilliseconds = 0,
            AllowSafeFallback = false,
            TimeoutSeconds = 5
        });

        var result = await gateway.AssessAnswerAsync(new AssessAnswerRequest(
            "Q",
            "A detailed answer with enough text to score above the brief threshold for assessment.",
            "c1",
            null,
            null));

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Meta.AttemptCount);
        Assert.Null(result.Meta.ErrorCode);
        Assert.False(result.Meta.UsedFallback);
    }

    [Fact]
    public async Task Gateway_uses_safe_fallback_after_exhausted_retries()
    {
        var fake = new AlwaysInvalidProvider();
        var gateway = CreateGateway(fake, new InterviewPrepAiOptions
        {
            Enabled = true,
            UseFakeProvider = true,
            MaxRetries = 1,
            RetryDelayMilliseconds = 0,
            AllowSafeFallback = true,
            TimeoutSeconds = 5
        });

        var result = await gateway.GenerateInterviewerMessageAsync(new GenerateInterviewerMessageRequest(
            DefaultConfig,
            "ask_question",
            "ownership",
            "ownership",
            [],
            []));

        Assert.True(result.Succeeded);
        Assert.True(result.Meta.UsedFallback);
        Assert.Equal(2, result.Meta.AttemptCount);
        Assert.Contains("ownership", result.Value!.MessageText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Gateway_returns_normalized_error_when_fallback_disabled()
    {
        var fake = new AlwaysInvalidProvider();
        var gateway = CreateGateway(fake, new InterviewPrepAiOptions
        {
            Enabled = true,
            UseFakeProvider = true,
            MaxRetries = 0,
            RetryDelayMilliseconds = 0,
            AllowSafeFallback = false,
            TimeoutSeconds = 5
        });

        var result = await gateway.CreateInterviewBriefAsync(new CreateInterviewBriefRequest(
            DefaultConfig,
            null,
            null));

        Assert.False(result.Succeeded);
        Assert.Equal(InterviewPrepAiErrorCodes.ValidationFailed, result.Meta.ErrorCode);
        Assert.Equal("interview-prep.create-interview-brief", result.Meta.PromptName);
        Assert.False(string.IsNullOrWhiteSpace(result.Meta.PromptVersion));
    }

    [Fact]
    public void Validator_rejects_out_of_range_assessment_score()
    {
        var invalid = new AssessAnswerResponse(
            150,
            "ok",
            [],
            [],
            [new AssessAnswerEvidenceItem("c", "q", "positive")],
            0.5);

        var result = InterviewPrepAiResponseValidator.Validate(InterviewPrepAiOperation.AssessAnswer, invalid);
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Live_transport_reports_disabled_without_calling_network()
    {
        var transport = new GoogleAiInterviewPrepTransport(
            new HttpClient(),
            Microsoft.Extensions.Options.Options.Create(new GoogleAiOptions { Enabled = false, ApiKey = string.Empty, Model = "gemini-2.5-flash" }),
            Microsoft.Extensions.Options.Options.Create(new InterviewPrepAiOptions { Enabled = true, UseFakeProvider = false }));

        var prompt = new InterviewPrepPromptRegistry().Get(InterviewPrepAiOperation.AssessAnswer);
        var result = await transport.CompleteAsync(
            prompt,
            new AssessAnswerRequest("Q", "A", null, null, null));

        Assert.False(result.Succeeded);
        Assert.Equal(InterviewPrepAiErrorCodes.Disabled, result.ErrorCode);
    }

    [Fact]
    public async Task Live_transport_stubs_unimplemented_operations()
    {
        var transport = new GoogleAiInterviewPrepTransport(
            new HttpClient(),
            Microsoft.Extensions.Options.Options.Create(new GoogleAiOptions
            {
                Enabled = true,
                ApiKey = "test-key-not-used",
                Model = "gemini-2.5-flash"
            }),
            Microsoft.Extensions.Options.Options.Create(new InterviewPrepAiOptions { Enabled = true, UseFakeProvider = false }));

        var prompt = new InterviewPrepPromptRegistry().Get(InterviewPrepAiOperation.CreateInterviewBrief);
        var result = await transport.CompleteAsync(
            prompt,
            new CreateInterviewBriefRequest(DefaultConfig, null, null));

        Assert.False(result.Succeeded);
        Assert.Equal(InterviewPrepAiErrorCodes.OperationNotImplemented, result.ErrorCode);
    }

    private static InterviewPrepAiGateway CreateGateway(
        IInterviewPrepAiProvider provider,
        InterviewPrepAiOptions? aiOptions = null) =>
        new(
            provider,
            new InterviewPrepPromptRegistry(),
            Microsoft.Extensions.Options.Options.Create(aiOptions ?? new InterviewPrepAiOptions
            {
                Enabled = true,
                UseFakeProvider = true,
                MaxRetries = 1,
                RetryDelayMilliseconds = 0,
                AllowSafeFallback = true,
                TimeoutSeconds = 5
            }),
            Microsoft.Extensions.Options.Options.Create(new GoogleAiOptions { Enabled = false, TimeoutSeconds = 5 }));

    private sealed class AlwaysInvalidProvider : IInterviewPrepAiProvider
    {
        public string ProviderName => "always-invalid";

        public Task<InterviewPrepAiProviderResult> CompleteAsync(
            InterviewPrepPromptDefinition prompt,
            object request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new InterviewPrepAiProviderResult(
                true,
                """{"summary":""}""",
                null,
                null));
    }
}
