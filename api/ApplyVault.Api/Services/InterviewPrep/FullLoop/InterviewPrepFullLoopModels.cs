using ApplyVault.Api.Services.InterviewPrep.Domain;
using ApplyVault.Api.Services.InterviewPrep.Planning;

namespace ApplyVault.Api.Services.InterviewPrep.FullLoop;

public sealed record InterviewPrepFullLoopStageSlot(
    string StageKey,
    string Goal,
    InterviewPrepMode Mode,
    InterviewPrepPersona Persona,
    IReadOnlyList<string> CompetencyIds);

public sealed record InterviewPrepFullLoopOrchestration(
    string LoopSummary,
    IReadOnlyList<string> CoverageGoals,
    IReadOnlyList<InterviewPrepFullLoopStageSlot> StageSlots,
    string Source,
    bool UsedAiFallback);

public sealed record InterviewPrepStagePlanBundle(
    InterviewPlan InterviewPlan,
    InterviewPrepMode Mode,
    InterviewPrepPersona Persona);

public sealed record InterviewPrepStageHandoffArtifact(
    string StageKey,
    string FactualSummary,
    IReadOnlyList<string> CompetenciesCovered,
    IReadOnlyList<string> StoriesDiscussed,
    IReadOnlyList<string> UnresolvedQuestions);

public sealed record InterviewPrepLoopGuardRevisitApproval(
    string QuestionSignature,
    string Reason);

public sealed record InterviewPrepStageAssessmentEntry(
    Guid StageId,
    string StageKey,
    string Summary,
    IReadOnlyList<string> Highlights,
    IReadOnlyList<string> MissedGoals,
    string Confidence);

public sealed record InterviewPrepPanelDebriefArtifact(
    string OverallDebrief,
    IReadOnlyList<InterviewPrepPanelPerspectiveDto> Perspectives,
    IReadOnlyList<string> EvidenceHighlights,
    IReadOnlyList<string> Contradictions,
    IReadOnlyList<InterviewPrepPanelMissingEvidenceDto> MissingEvidence,
    string OverallConfidence,
    string Source,
    bool UsedAiFallback,
    DateTimeOffset GeneratedAt);

public sealed record InterviewPrepPanelPerspectiveDto(
    string PersonaLabel,
    string Assessment,
    int Score);

public sealed record InterviewPrepPanelMissingEvidenceDto(
    string Signal,
    string Reason);
