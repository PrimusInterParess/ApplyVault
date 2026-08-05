using ApplyVault.Api.Models.InterviewPrep;
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

public sealed class InterviewPrepOperationalCatalogTests
{
    [Theory]
    [InlineData(InterviewPrepMode.RoleAndDomainDepth, InterviewPrepPersona.SeniorPeer, true)]
    [InlineData(InterviewPrepMode.ProcessAndSystems, InterviewPrepPersona.HiringManager, true)]
    [InlineData(InterviewPrepMode.ProblemSolvingCase, InterviewPrepPersona.BarRaiser, true)]
    [InlineData(InterviewPrepMode.ProblemSolvingCase, InterviewPrepPersona.Recruiter, false)]
    [InlineData(InterviewPrepMode.FullLoop, InterviewPrepPersona.HiringManager, true)]
    [InlineData(InterviewPrepMode.FullLoop, InterviewPrepPersona.SeniorPeer, false)]
    [InlineData(InterviewPrepMode.ScreeningAndMotivation, InterviewPrepPersona.BarRaiser, false)]
    public void Operational_create_gate(
        InterviewPrepMode mode,
        InterviewPrepPersona persona,
        bool allowed)
    {
        if (allowed)
        {
            InterviewPrepOperationalCatalog.EnsureOperationalCreate(mode, persona);
        }
        else
        {
            Assert.Throws<InterviewPrepValidationException>(() =>
                InterviewPrepOperationalCatalog.EnsureOperationalCreate(mode, persona));
        }
    }

    [Fact]
    public void Mode_and_persona_enums_wire_as_camelCase()
    {
        Assert.Equal("roleAndDomainDepth", InterviewPrepEnumNames.ToWire(InterviewPrepMode.RoleAndDomainDepth));
        Assert.Equal("processAndSystems", InterviewPrepEnumNames.ToWire(InterviewPrepMode.ProcessAndSystems));
        Assert.Equal("seniorPeer", InterviewPrepEnumNames.ToWire(InterviewPrepPersona.SeniorPeer));
        Assert.True(InterviewPrepEnumNames.TryParseMode("processAndSystems", out var mode));
        Assert.Equal(InterviewPrepMode.ProcessAndSystems, mode);
        Assert.True(InterviewPrepEnumNames.TryParsePersona("seniorPeer", out var persona));
        Assert.Equal(InterviewPrepPersona.SeniorPeer, persona);
    }
}

public sealed class InterviewPrepExpandedCatalogTests
{
    [Fact]
    public void Expanded_mode_catalogs_prioritize_role_depth_and_process_competencies()
    {
        var modes = new InterviewPrepModeCatalog();
        var personas = new InterviewPrepPersonaCatalog();

        var roleDepthMode = modes.Get(InterviewPrepMode.RoleAndDomainDepth);
        var processMode = modes.Get(InterviewPrepMode.ProcessAndSystems);
        var seniorPeer = personas.Get(InterviewPrepPersona.SeniorPeer);
        var recruiter = personas.Get(InterviewPrepPersona.Recruiter);

        Assert.Equal(InterviewPrepCompetencyCatalog.RoleDepth, roleDepthMode.PriorityCompetencyIds[0]);
        Assert.Contains(InterviewPrepCompetencyCatalog.ProblemSolving, processMode.PriorityCompetencyIds);
        Assert.Equal("peer_technical_depth", seniorPeer.QuestionStyleHint);
        Assert.Equal("concise_screen", recruiter.QuestionStyleHint);
        Assert.NotEqual(seniorPeer.EmphasisCompetencyIds[0], recruiter.EmphasisCompetencyIds[0]);
    }

    [Fact]
    public void Fixed_bank_covers_senior_peer_and_new_modes()
    {
        var bank = new FixedInterviewPrepQuestionBank();
        var rolePeer = bank.GetQuestions(InterviewPrepMode.RoleAndDomainDepth, InterviewPrepPersona.SeniorPeer);
        var processHm = bank.GetQuestions(InterviewPrepMode.ProcessAndSystems, InterviewPrepPersona.HiringManager);
        var screeningPeer = bank.GetQuestions(InterviewPrepMode.ScreeningAndMotivation, InterviewPrepPersona.SeniorPeer);

        Assert.Equal(4, rolePeer.Count);
        Assert.Equal(4, processHm.Count);
        Assert.Equal(4, screeningPeer.Count);
    }
}

public sealed class InterviewPrepExpandedPlanningTests
{
    [Fact]
    public async Task Planner_includes_role_depth_when_job_snapshot_present()
    {
        var planner = CreatePlanner();
        var bank = new FixedInterviewPrepQuestionBank().GetQuestions(
            InterviewPrepMode.RoleAndDomainDepth,
            InterviewPrepPersona.SeniorPeer);

        var comparison = new InterviewPrepSnapshotComparison(
            HasCv: true,
            HasJob: true,
            JobTitle: "Platform Engineer",
            CompanyName: "Acme",
            PresentCvSectionTypes: ["Experience"],
            MissingExpectedCvSectionTypes: [],
            UnknownSignals: [],
            CvPlainText: "Built platforms.",
            JobPlainText: "Own internal platforms.");

        var brief = new InterviewBrief(
            "Role depth brief",
            Themes: ["roleDepth", "execution"],
            Risks: [],
            TalkingPoints: [],
            Unknowns: [],
            PresentCvSectionTypes: ["Experience"],
            JobTitle: "Platform Engineer",
            CompanyName: "Acme",
            Source: InterviewPrepArtifactSources.Ai,
            UsedAiFallback: false);

        var plan = await planner.PlanAsync(
            new InterviewPrepPlanningContext(
                InterviewPrepMode.RoleAndDomainDepth,
                InterviewPrepPersona.SeniorPeer,
                InterviewPrepLanguage.English,
                InterviewPrepMarket.General,
                InterviewPrepExperienceType.RealisticSimulation,
                InterviewPrepInteractionType.Text,
                comparison),
            brief,
            bank);

        Assert.Contains(plan.Competencies, (c) => c.CompetencyId == InterviewPrepCompetencyCatalog.RoleDepth);
        Assert.Contains(plan.QuestionIntents, (intent) =>
            intent.InternalGoal.Contains("roleDepth", StringComparison.OrdinalIgnoreCase)
            || intent.CompetencyId == InterviewPrepCompetencyCatalog.RoleDepth);
    }

    [Fact]
    public async Task Fake_provider_plan_reflects_senior_peer_persona_style()
    {
        var gateway = CreateGateway();
        var config = new InterviewPrepAiSessionConfig(
            "roleAndDomainDepth",
            "seniorPeer",
            "english",
            "general",
            "realisticSimulation",
            "text");

        var brief = await gateway.CreateInterviewBriefAsync(new CreateInterviewBriefRequest(
            config,
            new InterviewPrepAiDocumentSnapshot("CV", "API engineer."),
            new InterviewPrepAiDocumentSnapshot("Backend Engineer", "Build services.")));

        Assert.True(brief.Succeeded);
        Assert.Contains("Senior peer", brief.Value!.Summary, StringComparison.Ordinal);
        Assert.Contains(InterviewPrepCompetencyCatalog.RoleDepth, brief.Value.Themes);

        var plan = await gateway.PlanInterviewAsync(new PlanInterviewRequest(config, brief.Value));
        Assert.True(plan.Succeeded);
        Assert.Contains("personaStyle=peer_technical_depth", plan.Value!.PlanSummary, StringComparison.Ordinal);
        Assert.Contains(plan.Value.Stages, (stage) =>
            stage.Goal.Contains("peer_technical_depth", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Fake_provider_message_framing_differs_senior_peer_vs_recruiter()
    {
        var provider = new FakeDeterministicInterviewPrepAiProvider();
        var registry = new InterviewPrepPromptRegistry();
        var prompt = registry.Get(InterviewPrepAiOperation.GenerateInterviewerMessage);

        var peerConfig = new InterviewPrepAiSessionConfig(
            "processAndSystems", "seniorPeer", "english", "general", "realisticSimulation", "text");
        var recruiterConfig = new InterviewPrepAiSessionConfig(
            "processAndSystems", "recruiter", "english", "general", "realisticSimulation", "text");

        var requestBase = new GenerateInterviewerMessageRequest(
            peerConfig,
            "ask_question",
            "execution",
            new string('x', 90),
            [],
            []);

        var peerResult = await provider.CompleteAsync(prompt, requestBase with { Config = peerConfig });
        var recruiterResult = await provider.CompleteAsync(
            prompt,
            requestBase with { Config = recruiterConfig });

        Assert.DoesNotContain("[peerTone]", peerResult.ResponseJson!, StringComparison.Ordinal);
        Assert.DoesNotContain("[screenTone]", recruiterResult.ResponseJson!, StringComparison.Ordinal);
        Assert.NotEqual(peerResult.ResponseJson, recruiterResult.ResponseJson);
    }

    [Fact]
    public void Create_request_validation_rejects_unfinished_modes()
    {
        var request = new InterviewPrepCreateSessionRequest(
            InterviewPrepMode.FullLoop,
            InterviewPrepPersona.Recruiter,
            InterviewPrepLanguage.English,
            InterviewPrepMarket.General,
            InterviewPrepExperienceType.RealisticSimulation,
            InterviewPrepInteractionType.Text);

        Assert.Throws<InterviewPrepValidationException>(() =>
            InterviewPrepOperationalCatalog.EnsureOperationalCreate(request.Mode, request.Persona));
    }

    [Fact]
    public void Language_practice_mode_is_operational_with_recruiter()
    {
        InterviewPrepOperationalCatalog.EnsureOperationalCreate(
            InterviewPrepMode.LanguagePractice,
            InterviewPrepPersona.Recruiter);
        InterviewPrepLanguageMarketCatalog.EnsureOperational(
            InterviewPrepLanguage.English,
            InterviewPrepMarket.General);
    }

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
}
