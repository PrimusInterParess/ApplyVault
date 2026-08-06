using ApplyVault.Api.Data;
using ApplyVault.Api.Infrastructure;
using ApplyVault.Api.Models.InterviewPrep;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services.InterviewPrep;
using ApplyVault.Api.Services.InterviewPrep.Ai;
using ApplyVault.Api.Services.InterviewPrep.Ai.Contracts;
using ApplyVault.Api.Services.InterviewPrep.Ai.Prompts;
using ApplyVault.Api.Services.InterviewPrep.Catalogs;
using ApplyVault.Api.Services.InterviewPrep.Domain;
using ApplyVault.Api.Services.InterviewPrep.Planning;
using ApplyVault.Api.Services.InterviewPrep.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Tests.InterviewPrep;

public sealed class InterviewPrepM8CaseAndBarRaiserTests
{
    [Fact]
    public void Case_fact_text_is_immutable_from_catalog()
    {
        var catalog = new InterviewPrepCaseCatalog();
        var runtime = new InterviewPrepCaseRuntime();
        var definition = catalog.Get("b2b-saas-churn-spike");

        var first = runtime.GetFactText(definition, "fact-onboarding-nps");
        var second = runtime.GetFactText(definition, "fact-onboarding-nps");

        Assert.Equal(first, second);
        Assert.Equal(
            definition.FactsOnRequest.First((fact) => fact.FactId == "fact-onboarding-nps").Text,
            first);
    }

    [Fact]
    public void Case_hint_ladder_advances_in_order()
    {
        var catalog = new InterviewPrepCaseCatalog();
        var runtime = new InterviewPrepCaseRuntime();
        var definition = catalog.Get("b2b-saas-churn-spike");
        var session = MinimalSession(InterviewPrepMode.ProblemSolvingCase, InterviewPrepPersona.HiringManager);
        var plan = BuildCasePlan(definition.CaseId, definition.Title);
        var caseState = new InterviewPrepCaseRuntimeState { CaseId = definition.CaseId };

        var counters = new AdaptiveRuntimeCounters(MainQuestionCount: 1, ConsecutiveNoProgress: 1);
        var hint1 = runtime.TryResolveAfterAnswer(session, plan, caseState, definition, "I need more time.", counters);
        Assert.NotNull(hint1);
        Assert.Equal(InterviewPrepRuntimeActionType.OfferHint, hint1!.ActionType);
        Assert.Equal(1, caseState.HintsUsed);

        var hint2 = runtime.TryResolveAfterAnswer(session, plan, caseState, definition, "Still stuck.", counters);
        Assert.NotNull(hint2);
        Assert.Equal(InterviewPrepRuntimeActionType.OfferHint, hint2!.ActionType);
        Assert.Equal(2, caseState.HintsUsed);
        Assert.Contains("Hint:", hint1.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public void Case_completes_when_recommendation_captured()
    {
        var catalog = new InterviewPrepCaseCatalog();
        var runtime = new InterviewPrepCaseRuntime();
        var definition = catalog.Get("b2b-saas-churn-spike");
        var session = MinimalSession(InterviewPrepMode.ProblemSolvingCase, InterviewPrepPersona.HiringManager);
        var plan = BuildCasePlan(definition.CaseId, definition.Title);
        var caseState = new InterviewPrepCaseRuntimeState { CaseId = definition.CaseId };

        var recommendation =
            "My 90-day plan prioritizes onboarding fixes, integration delivery, and cohort churn reviews with weekly KPI tracking across seat bands and NPS recovery targets.";
        var counters = new AdaptiveRuntimeCounters(MainQuestionCount: 3, ConsecutiveNoProgress: 0);
        _ = runtime.TryResolveAfterAnswer(session, plan, caseState, definition, recommendation, counters);

        Assert.True(caseState.CaseRecommendationCaptured);
        Assert.True(runtime.IsCaseComplete(definition, caseState, counters));
    }

    [Fact]
    public async Task Bar_raiser_fake_plan_includes_consistency_style()
    {
        var gateway = CreateGateway();
        var config = new InterviewPrepAiSessionConfig(
            "behavioralAndCulture",
            "barRaiser",
            "english",
            "general",
            "realisticSimulation",
            "text");

        var brief = await gateway.CreateInterviewBriefAsync(new CreateInterviewBriefRequest(
            config,
            new InterviewPrepAiDocumentSnapshot("CV", "Led teams."),
            null));

        Assert.True(brief.Succeeded);
        Assert.Contains("Bar raiser", brief.Value!.Summary, StringComparison.Ordinal);

        var plan = await gateway.PlanInterviewAsync(new PlanInterviewRequest(config, brief.Value));
        Assert.True(plan.Succeeded);
        Assert.Contains("bar_raiser_consistency", plan.Value!.PlanSummary, StringComparison.Ordinal);
    }

    [Fact]
    public void Bar_raiser_contradiction_signal_when_evidence_conflicts()
    {
        var session = MinimalSession(InterviewPrepMode.BehavioralAndCulture, InterviewPrepPersona.BarRaiser);
        session.EvidenceItems.Add(new InterviewPrepEvidenceItemEntity
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            CompetencyId = "ownership",
            Claim = "Owned end-to-end delivery",
            Polarity = "positive"
        });
        session.EvidenceItems.Add(new InterviewPrepEvidenceItemEntity
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            CompetencyId = "ownership",
            Claim = "Only advised, did not own delivery",
            Polarity = "negative"
        });

        var signals = InterviewPrepBarRaiserSignals.DetectContradictions(session);

        Assert.Single(signals);
        Assert.Equal("ownership", signals[0].CompetencyId);
        Assert.True(InterviewPrepBarRaiserSignals.ShouldPrioritizeConsistencyProbe(
            InterviewPrepPersona.BarRaiser,
            session));
    }

    [Fact]
    public async Task Planner_attaches_case_metadata_for_problem_solving_mode()
    {
        var planner = CreatePlanner();
        var bank = new FixedInterviewPrepQuestionBank().GetQuestions(
            InterviewPrepMode.ProblemSolvingCase,
            InterviewPrepPersona.BarRaiser);
        var comparison = new InterviewPrepSnapshotComparison(
            true, false, "Product Manager", null, ["Experience"], [], [], "cv", null);
        var brief = new InterviewBrief(
            "Case brief",
            ["problemSolving"],
            [],
            [],
            [],
            ["Experience"],
            "Product Manager",
            null,
            InterviewPrepArtifactSources.DeterministicFallback,
            true);

        var plan = await planner.PlanAsync(
            new InterviewPrepPlanningContext(
                InterviewPrepMode.ProblemSolvingCase,
                InterviewPrepPersona.BarRaiser,
                InterviewPrepLanguage.English,
                InterviewPrepMarket.General,
                InterviewPrepExperienceType.RealisticSimulation,
                InterviewPrepInteractionType.Text,
                comparison),
            brief,
            bank);

        Assert.NotNull(plan.CaseAttachment);
        Assert.Equal("b2b-saas-churn-spike", plan.CaseAttachment!.CaseId);
    }

    private static InterviewPlan BuildCasePlan(string caseId, string title) =>
        new(
            "case plan",
            [],
            [],
            new InterviewPlanBudgets(5, 2, 40, 50),
            [],
            new InterviewCompletionConditions([], 1, true),
            [],
            InterviewPrepArtifactSources.DeterministicFallback,
            true,
            new InterviewCasePlanAttachment(caseId, title));

    private static InterviewPlanner CreatePlanner() =>
        new(
            CreateGateway(),
            new InterviewPrepCompetencyCatalog(),
            new InterviewPrepModeCatalog(),
            new InterviewPrepPersonaCatalog(),
            new InterviewPrepCaseCatalog(),
            Microsoft.Extensions.Options.Options.Create(new InterviewPrepOptions()));

    private static InterviewPrepSessionEntity MinimalSession(
        InterviewPrepMode mode,
        InterviewPrepPersona persona) =>
        new()
        {
            Id = Guid.NewGuid(),
            Mode = InterviewPrepPersistence.Config(mode),
            Persona = InterviewPrepPersistence.Config(persona),
            Language = InterviewPrepPersistence.Config(InterviewPrepLanguage.English),
            Market = InterviewPrepPersistence.Config(InterviewPrepMarket.General),
            ExperienceType = InterviewPrepPersistence.Config(InterviewPrepExperienceType.RealisticSimulation),
            InteractionType = InterviewPrepPersistence.Config(InterviewPrepInteractionType.Text),
            Status = InterviewPrepPersistence.Status(InterviewPrepSessionStatus.InProgress)
        };

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
            Microsoft.Extensions.Options.Options.Create(new GoogleAiOptions { Enabled = false }),
            NullLogger<InterviewPrepAiGateway>.Instance,
            new InterviewPrepDebugTraceContext(),
            new InterviewPrepDebugFileTraceLogger());
}
