using ApplyVault.Api.Services.InterviewPrep.Catalogs;
using ApplyVault.Api.Services.InterviewPrep.Domain;

namespace ApplyVault.Api.Services.InterviewPrep.Planning;

public sealed record InterviewPrepSnapshotComparison(
    bool HasCv,
    bool HasJob,
    string? JobTitle,
    string? CompanyName,
    IReadOnlyList<string> PresentCvSectionTypes,
    IReadOnlyList<string> MissingExpectedCvSectionTypes,
    IReadOnlyList<string> UnknownSignals,
    string? CvPlainText,
    string? JobPlainText);

public sealed record InterviewBriefUnknown(
    string Signal,
    string CoverageState);

public sealed record InterviewBrief(
    string Summary,
    IReadOnlyList<string> Themes,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> TalkingPoints,
    IReadOnlyList<InterviewBriefUnknown> Unknowns,
    IReadOnlyList<string> PresentCvSectionTypes,
    string? JobTitle,
    string? CompanyName,
    string Source,
    bool UsedAiFallback);

public sealed record InterviewPlanCompetency(
    string CompetencyId,
    string DisplayName,
    int Priority,
    string InitialCoverageState,
    string ExpectedEvidenceClassification,
    string ExpectedEvidenceStrength,
    string ExpectedEvidenceConfidence);

public sealed record InterviewQuestionIntent(
    string IntentId,
    string CompetencyId,
    string InternalGoal,
    int SortOrder,
    int ProbeBudget);

/// <summary>
/// Public plan DTO fields must not include private rubrics or system prompts.
/// Candidate-facing wording lives on stage PlanJson (fixed bank) for M1/M3 turns.
/// </summary>
/// <summary>
/// Soft <see cref="TargetQuestions"/> guides pacing (~8–12). Hard <see cref="MaxQuestions"/>
/// is the safety ceiling that forces Stage end (~15–18).
/// </summary>
public sealed record InterviewPlanBudgets(
    int TargetQuestions,
    int MaxQuestions,
    int MaxProbes,
    int MaxTurns);

public sealed record InterviewEvidenceExpectation(
    string CompetencyId,
    string Classification,
    string MinimumStrength,
    string MinimumConfidence,
    string TargetCoverageState);

public sealed record InterviewCompletionConditions(
    IReadOnlyList<string> RequiredCompetencyIds,
    int MinimumCoveredCompetencies,
    bool RequireClosingTurn);

public sealed record InterviewPlanStage(
    string StageKey,
    string Goal,
    IReadOnlyList<string> CompetencyIds);

public sealed record InterviewCasePlanAttachment(
    string CaseId,
    string DisplayTitle);

public sealed record InterviewPlan(
    string PlanSummary,
    IReadOnlyList<InterviewPlanCompetency> Competencies,
    IReadOnlyList<InterviewQuestionIntent> QuestionIntents,
    InterviewPlanBudgets Budgets,
    IReadOnlyList<InterviewEvidenceExpectation> EvidenceExpectations,
    InterviewCompletionConditions CompletionConditions,
    IReadOnlyList<InterviewPlanStage> Stages,
    string Source,
    bool UsedAiFallback,
    InterviewCasePlanAttachment? CaseAttachment = null,
    IReadOnlyList<InterviewPlanLanguageSlot>? LanguageAllocation = null);

public sealed record InterviewPrepPlanningContext(
    InterviewPrepMode Mode,
    InterviewPrepPersona Persona,
    InterviewPrepLanguage Language,
    InterviewPrepMarket Market,
    InterviewPrepExperienceType ExperienceType,
    InterviewPrepInteractionType InteractionType,
    InterviewPrepSnapshotComparison Comparison);

public static class InterviewPrepArtifactSources
{
    public const string Ai = "ai";
    public const string DeterministicFallback = "deterministicFallback";
}
