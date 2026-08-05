using ApplyVault.Api.Data;
using ApplyVault.Api.Services.InterviewPrep.Catalogs;
using ApplyVault.Api.Services.InterviewPrep.Domain;
using ApplyVault.Api.Services.InterviewPrep.Planning;

namespace ApplyVault.Api.Services.InterviewPrep.Runtime;

public sealed record InterviewPrepCaseRuntimeState
{
    public string? CaseId { get; set; }

    public int HintsUsed { get; set; }

    public int ClarificationsUsed { get; set; }

    public int ComplicationsIntroduced { get; set; }

    public List<string> DisclosedFactIds { get; set; } = [];

    public List<string> TriggeredComplicationIds { get; set; } = [];

    public bool CaseRecommendationCaptured { get; set; }
}

public sealed record InterviewPrepCaseActionProposal(
    InterviewPrepRuntimeActionType ActionType,
    string IntentId,
    string MessageText,
    string? CompetencyId,
    string Rationale);

public interface IInterviewPrepCaseRuntime
{
    InterviewPrepCaseRuntimeState Read(InterviewPrepSessionEntity session);

    void Write(InterviewPrepSessionEntity session, InterviewPrepCaseRuntimeState state);

    void EnsureSeeded(InterviewPrepSessionEntity session, InterviewPlan plan);

    string BuildOpeningText(InterviewCaseDefinition caseDefinition);

    InterviewPrepCaseActionProposal? TryResolveAfterAnswer(
        InterviewPrepSessionEntity session,
        InterviewPlan plan,
        InterviewPrepCaseRuntimeState caseState,
        InterviewCaseDefinition caseDefinition,
        string candidateAnswerText,
        AdaptiveRuntimeCounters counters);

    bool IsCaseComplete(
        InterviewCaseDefinition caseDefinition,
        InterviewPrepCaseRuntimeState caseState,
        AdaptiveRuntimeCounters counters);

    string GetFactText(InterviewCaseDefinition caseDefinition, string factId);

    IReadOnlyList<string> GetAllFactTexts(InterviewCaseDefinition caseDefinition);
}

public sealed record AdaptiveRuntimeCounters(
    int MainQuestionCount,
    int ConsecutiveNoProgress);

public sealed class InterviewPrepCaseRuntime : IInterviewPrepCaseRuntime
{
    public InterviewPrepCaseRuntimeState Read(InterviewPrepSessionEntity session)
    {
        var runtime = InterviewPrepAdaptiveRuntimeStateHelper.ReadCaseState(session.RuntimeStateJson);
        return runtime ?? new InterviewPrepCaseRuntimeState();
    }

    public void Write(InterviewPrepSessionEntity session, InterviewPrepCaseRuntimeState state) =>
        InterviewPrepAdaptiveRuntimeStateHelper.WriteCaseState(session, state);

    public void EnsureSeeded(InterviewPrepSessionEntity session, InterviewPlan plan)
    {
        if (plan.CaseAttachment is null)
        {
            return;
        }

        var state = Read(session);
        if (string.IsNullOrWhiteSpace(state.CaseId))
        {
            state.CaseId = plan.CaseAttachment.CaseId;
            Write(session, state);
        }
    }

    public string BuildOpeningText(InterviewCaseDefinition caseDefinition) =>
        $"{caseDefinition.InitialSituation.Trim()} {caseDefinition.Objective.Trim()}";

    public InterviewPrepCaseActionProposal? TryResolveAfterAnswer(
        InterviewPrepSessionEntity session,
        InterviewPlan plan,
        InterviewPrepCaseRuntimeState caseState,
        InterviewCaseDefinition caseDefinition,
        string candidateAnswerText,
        AdaptiveRuntimeCounters counters)
    {
        _ = session;
        _ = plan;

        if (LooksLikeClarificationRequest(candidateAnswerText))
        {
            var nextFact = caseDefinition.FactsOnRequest
                .FirstOrDefault((fact) => !caseState.DisclosedFactIds.Contains(fact.FactId, StringComparer.Ordinal));
            if (nextFact is not null)
            {
                caseState.ClarificationsUsed++;
                caseState.DisclosedFactIds.Add(nextFact.FactId);
                var text = GetFactText(caseDefinition, nextFact.FactId);
                return new InterviewPrepCaseActionProposal(
                    InterviewPrepRuntimeActionType.DiscloseFact,
                    $"case-fact-{nextFact.FactId}",
                    $"Clarification: {text}",
                    InterviewPrepCompetencyCatalog.ProblemSolving,
                    "Disclose catalog fact on request without altering case facts.");
            }
        }

        if (counters.MainQuestionCount >= caseDefinition.Completion.MinimumMainQuestionsBeforeRecommendation
            && candidateAnswerText.Trim().Length >= caseDefinition.Completion.MinimumRecommendationAnswerLength)
        {
            caseState.CaseRecommendationCaptured = true;
            return null;
        }

        if (counters.ConsecutiveNoProgress >= 1
            && caseState.HintsUsed < caseDefinition.HintLadder.Count)
        {
            var hint = caseDefinition.HintLadder
                .OrderBy((step) => step.Level)
                .Skip(caseState.HintsUsed)
                .First();
            caseState.HintsUsed++;
            return new InterviewPrepCaseActionProposal(
                InterviewPrepRuntimeActionType.OfferHint,
                $"case-hint-{hint.Level}",
                $"Hint: {hint.Text}",
                InterviewPrepCompetencyCatalog.ProblemSolving,
                "Offer next hint from controlled ladder.");
        }

        var pendingComplication = caseDefinition.PlannedComplications
            .FirstOrDefault((entry) => !caseState.TriggeredComplicationIds.Contains(entry.ComplicationId, StringComparer.Ordinal));
        if (pendingComplication is not null
            && int.TryParse(pendingComplication.TriggerAfterMainQuestions, out var triggerAfter)
            && counters.MainQuestionCount >= triggerAfter)
        {
            caseState.ComplicationsIntroduced++;
            caseState.TriggeredComplicationIds.Add(pendingComplication.ComplicationId);
            return new InterviewPrepCaseActionProposal(
                InterviewPrepRuntimeActionType.IntroduceComplication,
                $"case-complication-{pendingComplication.ComplicationId}",
                pendingComplication.Narrative.Trim(),
                InterviewPrepCompetencyCatalog.ProblemSolving,
                "Introduce planned case complication from catalog.");
        }

        if (caseState.CaseRecommendationCaptured)
        {
            return null;
        }

        if (counters.MainQuestionCount < caseDefinition.Completion.MinimumMainQuestionsBeforeRecommendation)
        {
            return new InterviewPrepCaseActionProposal(
                InterviewPrepRuntimeActionType.AskQuestion,
                "case-structure",
                "What information would you want to validate first, and how would you structure your approach?",
                InterviewPrepCompetencyCatalog.ProblemSolving,
                "Guide case structure before final recommendation.");
        }

        return new InterviewPrepCaseActionProposal(
            InterviewPrepRuntimeActionType.AskQuestion,
            "case-recommendation",
            "Please share your prioritized recommendation for the next 90 days, including metrics you would track.",
            InterviewPrepCompetencyCatalog.ProblemSolving,
            "Elicit final case recommendation.");
    }

    public bool IsCaseComplete(
        InterviewCaseDefinition caseDefinition,
        InterviewPrepCaseRuntimeState caseState,
        AdaptiveRuntimeCounters counters)
    {
        if (caseState.CaseRecommendationCaptured)
        {
            return true;
        }

        return counters.MainQuestionCount >= Math.Max(caseDefinition.Completion.MinimumMainQuestionsBeforeRecommendation + 2, 4);
    }

    public string GetFactText(InterviewCaseDefinition caseDefinition, string factId)
    {
        var fact = caseDefinition.FixedFacts
            .Concat(caseDefinition.FactsOnRequest)
            .FirstOrDefault((entry) => string.Equals(entry.FactId, factId, StringComparison.Ordinal))
            ?? throw new InterviewPrepValidationException($"Case fact '{factId}' is not in the catalog.");

        // Always return canonical catalog text (immutable at runtime).
        return fact.Text;
    }

    public IReadOnlyList<string> GetAllFactTexts(InterviewCaseDefinition caseDefinition) =>
        caseDefinition.FixedFacts
            .Concat(caseDefinition.FactsOnRequest)
            .Select((fact) => fact.Text)
            .ToArray();

    private static bool LooksLikeClarificationRequest(string answerText)
    {
        if (string.IsNullOrWhiteSpace(answerText))
        {
            return false;
        }

        var trimmed = answerText.Trim();
        if (trimmed.Contains('?', StringComparison.Ordinal))
        {
            return true;
        }

        string[] markers =
        [
            "can you clarify",
            "could you clarify",
            "can you share",
            "what data",
            "what numbers",
            "do we know",
            "clarification",
            "more detail",
            "additional context"
        ];

        return markers.Any((marker) => trimmed.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}

internal static class InterviewPrepAdaptiveRuntimeStateHelper
{
    // Shared JSON envelope keys between adaptive runtime and case runtime.
    public const string CaseStateProperty = "case";

    public static InterviewPrepCaseRuntimeState? ReadCaseState(string? runtimeStateJson) =>
        InterviewPrepCombinedRuntimeState.Read(runtimeStateJson)?.Case;

    public static void WriteCaseState(InterviewPrepSessionEntity session, InterviewPrepCaseRuntimeState caseState) =>
        InterviewPrepCombinedRuntimeState.WriteCase(session, caseState);
}
