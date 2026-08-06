using ApplyVault.Api.Models.InterviewPrep;
using ApplyVault.Api.Services.InterviewPrep.Catalogs;
using ApplyVault.Api.Services.InterviewPrep.Domain;
using ApplyVault.Api.Services.InterviewPrep.Planning;

namespace ApplyVault.Api.Tests.InterviewPrep;

public sealed class InterviewPrepM9LanguageMarketTests
{
    [Fact]
    public void Mixed_language_allocation_alternates_english_and_danish()
    {
        var allocation = InterviewPrepTurnLanguage.BuildAllocation(
            InterviewPrepLanguage.MixedEnglishDanish,
            mainQuestionSlots: 4);

        Assert.Equal(4, allocation.Count);
        Assert.Equal(InterviewPrepLanguage.English, allocation[0].Language);
        Assert.Equal(InterviewPrepLanguage.Danish, allocation[1].Language);
        Assert.Equal(InterviewPrepLanguage.English, allocation[2].Language);
        Assert.Equal(InterviewPrepLanguage.Danish, allocation[3].Language);
    }

    [Fact]
    public void Danish_allocation_tags_all_main_question_slots()
    {
        var allocation = InterviewPrepTurnLanguage.BuildAllocation(
            InterviewPrepLanguage.Danish,
            mainQuestionSlots: 3);

        Assert.All(allocation, (slot) => Assert.Equal(InterviewPrepLanguage.Danish, slot.Language));
    }

    [Fact]
    public void Language_and_market_enums_wire_as_camelCase()
    {
        Assert.Equal("mixedEnglishDanish", InterviewPrepEnumNames.ToWire(InterviewPrepLanguage.MixedEnglishDanish));
        Assert.Equal("danish", InterviewPrepEnumNames.ToWire(InterviewPrepMarket.Danish));
        Assert.True(InterviewPrepEnumNames.TryParseLanguage("danish", out var language));
        Assert.Equal(InterviewPrepLanguage.Danish, language);
        Assert.True(InterviewPrepEnumNames.TryParseMarket("danish", out var market));
        Assert.Equal(InterviewPrepMarket.Danish, market);
    }

    [Fact]
    public void Language_practice_create_request_passes_operational_gates()
    {
        var request = new InterviewPrepCreateSessionRequest(
            InterviewPrepMode.LanguagePractice,
            InterviewPrepPersona.HiringManager,
            InterviewPrepLanguage.MixedEnglishDanish,
            InterviewPrepMarket.Danish,
            InterviewPrepExperienceType.RealisticSimulation,
            InterviewPrepInteractionType.Text);

        InterviewPrepOperationalCatalog.EnsureOperationalCreate(request.Mode, request.Persona);
        InterviewPrepLanguageMarketCatalog.EnsureOperational(request.Language, request.Market);
    }

    [Fact]
    public void Danish_market_prompt_supplement_is_profession_agnostic()
    {
        var text = InterviewPrepLanguageMarketCatalog.PromptSupplement(
            InterviewPrepLanguage.English,
            InterviewPrepMarket.Danish);

        Assert.Contains("Danish-market", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stereotypes", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Planner_persists_language_allocation_for_mixed_session()
    {
        var allocation = InterviewPrepTurnLanguage.BuildAllocation(
            InterviewPrepLanguage.MixedEnglishDanish,
            mainQuestionSlots: 5);

        var plan = new InterviewPlan(
            "Mixed plan",
            Competencies: [],
            QuestionIntents: [],
            Budgets: new InterviewPlanBudgets(5, 2, 20, 40),
            EvidenceExpectations: [],
            CompletionConditions: new InterviewCompletionConditions([], 0, true),
            Stages: [],
            Source: InterviewPrepArtifactSources.DeterministicFallback,
            UsedAiFallback: true,
            LanguageAllocation: allocation);

        Assert.NotNull(plan.LanguageAllocation);
        Assert.Equal(InterviewPrepLanguage.Danish, plan.LanguageAllocation![1].Language);
    }
}
