using ApplyVault.Api.Options;
using ApplyVault.Api.Services.InterviewPrep;
using ApplyVault.Api.Services.InterviewPrep.Ai;
using ApplyVault.Api.Services.InterviewPrep.Ai.Contracts;
using ApplyVault.Api.Services.InterviewPrep.Ai.Prompts;
using ApplyVault.Api.Services.InterviewPrep.Catalogs;
using ApplyVault.Api.Services.InterviewPrep.Domain;
using ApplyVault.Api.Services.InterviewPrep.Planning;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Tests.InterviewPrep;

public sealed class InterviewPrepCatalogTests
{
    [Fact]
    public void Competency_catalog_includes_core_set_and_role_depth_extension()
    {
        var catalog = new InterviewPrepCompetencyCatalog();

        Assert.Contains(catalog.Core, (c) => c.Id == InterviewPrepCompetencyCatalog.Motivation);
        Assert.Contains(catalog.Core, (c) => c.Id == InterviewPrepCompetencyCatalog.Communication);
        Assert.Contains(catalog.Core, (c) => c.Id == InterviewPrepCompetencyCatalog.Collaboration);
        Assert.Contains(catalog.Core, (c) => c.Id == InterviewPrepCompetencyCatalog.Ownership);
        Assert.Contains(catalog.Core, (c) => c.Id == InterviewPrepCompetencyCatalog.ProblemSolving);
        Assert.Contains(catalog.Core, (c) => c.Id == InterviewPrepCompetencyCatalog.Execution);
        Assert.Contains(catalog.Core, (c) => c.Id == InterviewPrepCompetencyCatalog.Leadership);

        Assert.True(catalog.TryGet(InterviewPrepCompetencyCatalog.RoleDepth, out var roleDepth));
        Assert.True(roleDepth.IsRoleDepthExtension);
        Assert.Equal(InterviewPrepCompetencyCatalog.ProblemSolving, catalog.ResolveId("problem_solving"));
        Assert.Equal(InterviewPrepCompetencyCatalog.Execution, catalog.ResolveId("impact"));
    }

    [Fact]
    public void Mode_and_persona_catalogs_are_separate_and_prioritize_competencies()
    {
        var modes = new InterviewPrepModeCatalog();
        var personas = new InterviewPrepPersonaCatalog();

        var screening = modes.Get(InterviewPrepMode.ScreeningAndMotivation);
        var behavioral = modes.Get(InterviewPrepMode.BehavioralAndCulture);
        var recruiter = personas.Get(InterviewPrepPersona.Recruiter);
        var hiringManager = personas.Get(InterviewPrepPersona.HiringManager);

        Assert.Contains(InterviewPrepCompetencyCatalog.Motivation, screening.PriorityCompetencyIds);
        Assert.Contains(InterviewPrepCompetencyCatalog.Collaboration, behavioral.PriorityCompetencyIds);
        Assert.Contains(InterviewPrepCompetencyCatalog.Communication, recruiter.EmphasisCompetencyIds);
        Assert.Contains(InterviewPrepCompetencyCatalog.Ownership, hiringManager.EmphasisCompetencyIds);
        Assert.NotEqual(screening.DisplayName, behavioral.DisplayName);
        Assert.NotEqual(recruiter.DisplayName, hiringManager.DisplayName);
    }

    [Fact]
    public void Evidence_and_coverage_enums_wire_as_camelCase()
    {
        Assert.Equal("unknown", InterviewPrepCatalogNames.ToWire(InterviewEvidenceStrength.Unknown));
        Assert.Equal("notStarted", InterviewPrepCatalogNames.ToWire(InterviewCoverageState.NotStarted));
        Assert.True(InterviewPrepCatalogNames.TryParse("gapsRemain", out InterviewCoverageState state));
        Assert.Equal(InterviewCoverageState.GapsRemain, state);
    }
}

public sealed class InterviewPrepPlanningTests
{
    [Fact]
    public void Context_builder_marks_missing_cv_sections_as_unknown_not_weak()
    {
        var builder = CreateBuilder();
        var cvJson = """
            {
              "sections": [
                { "heading": "Experience", "sectionType": "Experience", "entries": [ { "title": "Engineer", "summary": "Built APIs." } ] }
              ]
            }
            """;

        var comparison = builder.CompareSnapshots(cvJson, jobSnapshotJson: null);

        Assert.True(comparison.HasCv);
        Assert.False(comparison.HasJob);
        Assert.Contains("Experience", comparison.PresentCvSectionTypes);
        Assert.Contains("Summary", comparison.MissingExpectedCvSectionTypes);
        Assert.Contains(comparison.UnknownSignals, (signal) => signal.StartsWith("cv_section_unknown:", StringComparison.Ordinal));
        Assert.Contains(comparison.UnknownSignals, (signal) => signal == "job_snapshot_absent");
        Assert.DoesNotContain(comparison.UnknownSignals, (signal) => signal.Contains("weak", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Context_builder_uses_ai_brief_when_gateway_succeeds()
    {
        var builder = CreateBuilder();
        var comparison = builder.CompareSnapshots(
            """{"sections":[{"heading":"Experience","sectionType":"Experience","entries":[{"title":"Eng","summary":"Worked"}]}]}""",
            """{"jobTitle":"Backend Engineer","companyName":"Acme","jobDescription":"Build APIs"}""");

        var brief = await builder.BuildBriefAsync(CreatePlanningContext(comparison));

        Assert.Equal(InterviewPrepArtifactSources.Ai, brief.Source);
        Assert.False(brief.UsedAiFallback);
        Assert.False(string.IsNullOrWhiteSpace(brief.Summary));
        Assert.NotEmpty(brief.Themes);
        Assert.Equal("Backend Engineer", brief.JobTitle);
        Assert.Equal("Acme", brief.CompanyName);
    }

    [Fact]
    public async Task Context_builder_falls_back_deterministically_when_ai_fails()
    {
        var failing = new FailingInterviewPrepAiGateway();
        var builder = new InterviewContextBuilder(
            failing,
            new InterviewPrepModeCatalog(),
            new InterviewPrepPersonaCatalog());

        var comparison = builder.CompareSnapshots(
            """{"sections":[{"heading":"Experience","sectionType":"Experience","entries":[]}]}""",
            null);

        var brief = await builder.BuildBriefAsync(CreatePlanningContext(comparison));

        Assert.Equal(InterviewPrepArtifactSources.DeterministicFallback, brief.Source);
        Assert.True(brief.UsedAiFallback);
        Assert.Contains(brief.Unknowns, (unknown) => unknown.CoverageState == "unknown");
        Assert.DoesNotContain(brief.Summary, "invented", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Planner_produces_intents_budgets_and_evidence_expectations()
    {
        var planner = CreatePlanner();
        var bank = new FixedInterviewPrepQuestionBank().GetQuestions(
            InterviewPrepMode.ScreeningAndMotivation,
            InterviewPrepPersona.Recruiter);

        var comparison = new InterviewPrepSnapshotComparison(
            HasCv: true,
            HasJob: false,
            JobTitle: null,
            CompanyName: null,
            PresentCvSectionTypes: ["Experience"],
            MissingExpectedCvSectionTypes: ["Summary"],
            UnknownSignals: ["cv_section_unknown:Summary", "job_snapshot_absent"],
            CvPlainText: "Experience: Engineer",
            JobPlainText: null);

        var brief = new InterviewBrief(
            "Test brief",
            Themes: ["motivation", "ownership"],
            Risks: ["limited_context"],
            TalkingPoints: ["clarify goals"],
            Unknowns: [new InterviewBriefUnknown("cv_section_unknown:Summary", "unknown")],
            PresentCvSectionTypes: ["Experience"],
            JobTitle: null,
            CompanyName: null,
            Source: InterviewPrepArtifactSources.Ai,
            UsedAiFallback: false);

        var plan = await planner.PlanAsync(CreatePlanningContext(comparison), brief, bank);

        Assert.Equal(InterviewPrepArtifactSources.Ai, plan.Source);
        Assert.NotEmpty(plan.Competencies);
        Assert.NotEmpty(plan.QuestionIntents);
        Assert.True(plan.Budgets.MaxQuestions >= bank.Count);
        Assert.NotEmpty(plan.EvidenceExpectations);
        Assert.NotEmpty(plan.CompletionConditions.RequiredCompetencyIds);
        Assert.All(plan.QuestionIntents, (intent) =>
        {
            Assert.False(string.IsNullOrWhiteSpace(intent.IntentId));
            Assert.False(string.IsNullOrWhiteSpace(intent.InternalGoal));
            Assert.DoesNotContain("Tell me", intent.InternalGoal, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Contains(plan.Stages, (stage) => stage.StageKey == "core");
    }

    [Fact]
    public async Task Planner_falls_back_when_plan_ai_fails()
    {
        var planner = new InterviewPlanner(
            new FailingInterviewPrepAiGateway(),
            new InterviewPrepCompetencyCatalog(),
            new InterviewPrepModeCatalog(),
            new InterviewPrepPersonaCatalog(),
            new InterviewPrepCaseCatalog(),
            Microsoft.Extensions.Options.Options.Create(new InterviewPrepOptions()));

        var bank = new FixedInterviewPrepQuestionBank().GetQuestions(
            InterviewPrepMode.BehavioralAndCulture,
            InterviewPrepPersona.HiringManager);

        var comparison = new InterviewPrepSnapshotComparison(
            true, false, null, null, ["Experience"], [], [], "cv", null);

        var brief = new InterviewBrief(
            "Fallback brief",
            ["collaboration"],
            [],
            [],
            [],
            ["Experience"],
            null,
            null,
            InterviewPrepArtifactSources.DeterministicFallback,
            true);

        var plan = await planner.PlanAsync(
            new InterviewPrepPlanningContext(
                InterviewPrepMode.BehavioralAndCulture,
                InterviewPrepPersona.HiringManager,
                InterviewPrepLanguage.English,
                InterviewPrepMarket.General,
                InterviewPrepExperienceType.RealisticSimulation,
                InterviewPrepInteractionType.Text,
                comparison),
            brief,
            bank);

        Assert.Equal(InterviewPrepArtifactSources.DeterministicFallback, plan.Source);
        Assert.True(plan.UsedAiFallback);
        Assert.NotEmpty(plan.QuestionIntents);
        Assert.Contains(plan.Competencies, (c) => c.CompetencyId == InterviewPrepCompetencyCatalog.Leadership);
    }

    private static InterviewContextBuilder CreateBuilder() =>
        new(
            CreateGateway(),
            new InterviewPrepModeCatalog(),
            new InterviewPrepPersonaCatalog());

    private static InterviewPlanner CreatePlanner() =>
        new(
            CreateGateway(),
            new InterviewPrepCompetencyCatalog(),
            new InterviewPrepModeCatalog(),
            new InterviewPrepPersonaCatalog(),
            new InterviewPrepCaseCatalog(),
            Microsoft.Extensions.Options.Options.Create(new InterviewPrepOptions()));

    private static InterviewPrepAiGateway CreateGateway() =>
        new(
            new FakeDeterministicInterviewPrepAiProvider(),
            new InterviewPrepPromptRegistry(),
            Microsoft.Extensions.Options.Options.Create(new InterviewPrepAiOptions
            {
                Enabled = true,
                UseFakeProvider = true,
                MaxRetries = 0,
                AllowSafeFallback = false
            }),
            Microsoft.Extensions.Options.Options.Create(new GoogleAiOptions { Enabled = false }));

    private static InterviewPrepPlanningContext CreatePlanningContext(InterviewPrepSnapshotComparison comparison) =>
        new(
            InterviewPrepMode.ScreeningAndMotivation,
            InterviewPrepPersona.Recruiter,
            InterviewPrepLanguage.English,
            InterviewPrepMarket.General,
            InterviewPrepExperienceType.RealisticSimulation,
            InterviewPrepInteractionType.Text,
            comparison);

    private sealed class FailingInterviewPrepAiGateway : IInterviewPrepAiGateway
    {
        private static InterviewPrepAiExecutionResult<T> Fail<T>() =>
            InterviewPrepAiExecutionResult<T>.Failure(
                new InterviewPrepPromptDefinition(
                    InterviewPrepAiOperation.CreateInterviewBrief,
                    "fail",
                    "1",
                    "sys",
                    "user {{payloadJson}}"),
                1,
                "forced_failure",
                "forced failure for tests");

        public Task<InterviewPrepAiExecutionResult<CreateInterviewBriefResponse>> CreateInterviewBriefAsync(
            CreateInterviewBriefRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Fail<CreateInterviewBriefResponse>());

        public Task<InterviewPrepAiExecutionResult<PlanInterviewResponse>> PlanInterviewAsync(
            PlanInterviewRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Fail<PlanInterviewResponse>());

        public Task<InterviewPrepAiExecutionResult<GenerateOpeningResponse>> GenerateOpeningAsync(
            GenerateOpeningRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Fail<GenerateOpeningResponse>());

        public Task<InterviewPrepAiExecutionResult<AssessAnswerResponse>> AssessAnswerAsync(
            AssessAnswerRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Fail<AssessAnswerResponse>());

        public Task<InterviewPrepAiExecutionResult<SelectNextActionResponse>> SelectNextActionAsync(
            SelectNextActionRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Fail<SelectNextActionResponse>());

        public Task<InterviewPrepAiExecutionResult<GenerateInterviewerMessageResponse>> GenerateInterviewerMessageAsync(
            GenerateInterviewerMessageRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Fail<GenerateInterviewerMessageResponse>());

        public Task<InterviewPrepAiExecutionResult<SummarizeConversationResponse>> SummarizeConversationAsync(
            SummarizeConversationRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Fail<SummarizeConversationResponse>());

        public Task<InterviewPrepAiExecutionResult<EvaluateStageResponse>> EvaluateStageAsync(
            EvaluateStageRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Fail<EvaluateStageResponse>());

        public Task<InterviewPrepAiExecutionResult<GenerateFeedbackResponse>> GenerateFeedbackAsync(
            GenerateFeedbackRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Fail<GenerateFeedbackResponse>());

        public Task<InterviewPrepAiExecutionResult<CompareAnswerRetryResponse>> CompareAnswerRetryAsync(
            CompareAnswerRetryRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Fail<CompareAnswerRetryResponse>());

        public Task<InterviewPrepAiExecutionResult<ExtractProfessionalStoryResponse>> ExtractProfessionalStoryAsync(
            ExtractProfessionalStoryRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Fail<ExtractProfessionalStoryResponse>());

        public Task<InterviewPrepAiExecutionResult<PlanFullLoopResponse>> PlanFullLoopAsync(
            PlanFullLoopRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Fail<PlanFullLoopResponse>());

        public Task<InterviewPrepAiExecutionResult<GeneratePanelDebriefResponse>> GeneratePanelDebriefAsync(
            GeneratePanelDebriefRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Fail<GeneratePanelDebriefResponse>());
    }
}
