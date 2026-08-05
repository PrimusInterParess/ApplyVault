using ApplyVault.Api.Services.InterviewPrep.Domain;

namespace ApplyVault.Api.Models.InterviewPrep;

public sealed record InterviewPrepCreateSessionRequest(
    InterviewPrepMode Mode,
    InterviewPrepPersona Persona,
    InterviewPrepLanguage Language,
    InterviewPrepMarket Market,
    InterviewPrepExperienceType ExperienceType,
    InterviewPrepInteractionType InteractionType,
    Guid? ScrapeResultId = null,
    string? IdempotencyKey = null);

public sealed record InterviewPrepSubmitTurnRequest(
    string ClientTurnId,
    string Answer);

public sealed record InterviewPrepSessionSummaryDto(
    Guid Id,
    string Status,
    string Mode,
    string Persona,
    string Language,
    string Market,
    string ExperienceType,
    string InteractionType,
    Guid? ScrapeResultId,
    string? JobTitle,
    string? CompanyName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PreparedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string ETag);

public sealed record InterviewPrepSessionListResponseDto(
    IReadOnlyList<InterviewPrepSessionSummaryDto> Items);

public sealed record InterviewPrepStageDto(
    Guid Id,
    int SortOrder,
    string StageType,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);

public sealed record InterviewPrepTurnDto(
    Guid Id,
    Guid StageId,
    int Sequence,
    string Role,
    string Text,
    string? QuestionSignature,
    string? CompetencyTag,
    string? Language,
    string? ClientTurnId,
    DateTimeOffset CreatedAt,
    string? ActionType = null);

public sealed record InterviewPrepBriefDto(
    string Summary,
    IReadOnlyList<string> Themes,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> TalkingPoints,
    IReadOnlyList<InterviewPrepBriefUnknownDto> Unknowns,
    IReadOnlyList<string> PresentCvSectionTypes,
    string? JobTitle,
    string? CompanyName,
    string Source,
    bool UsedAiFallback);

public sealed record InterviewPrepBriefUnknownDto(
    string Signal,
    string CoverageState);

public sealed record InterviewPrepPlanCompetencyDto(
    string CompetencyId,
    string DisplayName,
    int Priority,
    string InitialCoverageState,
    string ExpectedEvidenceClassification,
    string ExpectedEvidenceStrength,
    string ExpectedEvidenceConfidence);

public sealed record InterviewPrepQuestionIntentDto(
    string IntentId,
    string CompetencyId,
    string InternalGoal,
    int SortOrder,
    int ProbeBudget);

public sealed record InterviewPrepPlanBudgetsDto(
    int TargetQuestions,
    int MaxQuestions,
    int MaxProbes,
    int MaxTurns);

public sealed record InterviewPrepEvidenceExpectationDto(
    string CompetencyId,
    string Classification,
    string MinimumStrength,
    string MinimumConfidence,
    string TargetCoverageState);

public sealed record InterviewPrepCompletionConditionsDto(
    IReadOnlyList<string> RequiredCompetencyIds,
    int MinimumCoveredCompetencies,
    bool RequireClosingTurn);

public sealed record InterviewPrepPlanStageDto(
    string StageKey,
    string Goal,
    IReadOnlyList<string> CompetencyIds);

public sealed record InterviewPrepPlanDto(
    string PlanSummary,
    IReadOnlyList<InterviewPrepPlanCompetencyDto> Competencies,
    IReadOnlyList<InterviewPrepQuestionIntentDto> QuestionIntents,
    InterviewPrepPlanBudgetsDto Budgets,
    IReadOnlyList<InterviewPrepEvidenceExpectationDto> EvidenceExpectations,
    InterviewPrepCompletionConditionsDto CompletionConditions,
    IReadOnlyList<InterviewPrepPlanStageDto> Stages,
    string Source,
    bool UsedAiFallback);

public sealed record InterviewPrepSessionDetailDto(
    Guid Id,
    string Status,
    string Mode,
    string Persona,
    string Language,
    string Market,
    string ExperienceType,
    string InteractionType,
    Guid? ScrapeResultId,
    Guid? CvDocumentId,
    string? CatalogVersion,
    string? JobTitle,
    string? CompanyName,
    bool HasCvSnapshot,
    bool HasJobSnapshot,
    string? FailureReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PreparedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancelledAt,
    string ETag,
    InterviewPrepBriefDto? Brief,
    InterviewPrepPlanDto? Plan,
    IReadOnlyList<InterviewPrepStageDto> Stages,
    IReadOnlyList<InterviewPrepTurnDto> Turns);

public sealed record InterviewPrepTurnSubmitResponseDto(
    InterviewPrepSessionDetailDto Session,
    InterviewPrepTurnDto CandidateTurn,
    InterviewPrepTurnDto? NextInterviewerTurn,
    bool InterviewComplete,
    bool StageTransitionOccurred = false);

public sealed record InterviewPrepTranscriptTurnDto(
    Guid Id,
    int Sequence,
    string Role,
    string Text,
    DateTimeOffset CreatedAt);

public sealed record InterviewPrepTranscriptDto(
    Guid SessionId,
    string Status,
    IReadOnlyList<InterviewPrepTranscriptTurnDto> Turns);

public sealed record InterviewPrepMissingEvidenceDto(
    string Signal,
    string Reason,
    bool IsUnknownNotWeakness);

public sealed record InterviewPrepJobRequirementCoverageDto(
    string CompetencyId,
    string DisplayName,
    string CoverageState,
    string? JobAlignmentNote,
    string Confidence);

public sealed record InterviewPrepJobCoverageSummaryDto(
    string? JobTitle,
    string? CompanyName,
    IReadOnlyList<InterviewPrepJobRequirementCoverageDto> Requirements);

public sealed record InterviewPrepAnswerQualityPatternDto(
    string Pattern,
    string Detail,
    string Confidence);

public sealed record InterviewPrepEvidenceTraceDto(
    string CompetencyId,
    string Claim,
    string EvidenceQuote,
    int? CandidateTurnSequence,
    string Classification);

public sealed record InterviewPrepStageSummaryDto(
    string StageKey,
    string Summary,
    IReadOnlyList<string> Highlights,
    IReadOnlyList<string> MissedGoals,
    string Confidence);

public sealed record InterviewPrepCandidateReportDto(
    Guid SessionId,
    string Status,
    string Disclaimer,
    string Summary,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> DevelopmentAreas,
    IReadOnlyList<InterviewPrepMissingEvidenceDto> MissingEvidence,
    InterviewPrepJobCoverageSummaryDto? JobCoverage,
    IReadOnlyList<InterviewPrepAnswerQualityPatternDto> AnswerQualityPatterns,
    IReadOnlyList<string> PracticeRecommendations,
    string OverallConfidence,
    IReadOnlyList<InterviewPrepEvidenceTraceDto> EvidenceTrace,
    IReadOnlyList<InterviewPrepStageSummaryDto> StageSummaries,
    IReadOnlyList<string>? LanguageFeedback,
    DateTimeOffset GeneratedAt,
    bool UsedAiFallback);

public sealed record InterviewPrepCompetencyEvidenceDto(
    string Claim,
    string EvidenceQuote,
    string Classification,
    string Strength,
    string Confidence);

public sealed record InterviewPrepCompetencyResultDto(
    string CompetencyId,
    string DisplayName,
    string CoverageState,
    int EvidenceCount,
    int AttemptCount,
    string Confidence,
    IReadOnlyList<InterviewPrepCompetencyEvidenceDto> SupportingEvidence,
    IReadOnlyList<string> ObservedGaps);

public sealed record InterviewPrepCompetencyResultsDto(
    Guid SessionId,
    string Disclaimer,
    IReadOnlyList<InterviewPrepCompetencyResultDto> Competencies);

public sealed record InterviewPrepSubmitAnswerRetryRequest(string RevisedAnswer);

public sealed record InterviewPrepLoopGuardRevisitRequest(
    string QuestionSignature,
    string Reason);

public sealed record InterviewPrepAnswerReviewDto(
    Guid RetryId,
    Guid CandidateTurnId,
    Guid InterviewerTurnId,
    string QuestionText,
    string OriginalAnswerText,
    string AnswerSummary,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Gaps,
    string CoachingFeedback,
    IReadOnlyList<string> CoachingTips,
    IReadOnlyList<string> PracticeRecommendations,
    string Status,
    DateTimeOffset UpdatedAt);

public sealed record InterviewPrepPanelPerspectivePublicDto(
    string PersonaLabel,
    string Assessment,
    int Score);

public sealed record InterviewPrepPanelMissingEvidencePublicDto(
    string Signal,
    string Reason);

public sealed record InterviewPrepPanelDebriefDto(
    string OverallDebrief,
    IReadOnlyList<InterviewPrepPanelPerspectivePublicDto> Perspectives,
    IReadOnlyList<string> EvidenceHighlights,
    IReadOnlyList<string> Contradictions,
    IReadOnlyList<InterviewPrepPanelMissingEvidencePublicDto> MissingEvidence,
    string OverallConfidence,
    string Source,
    bool UsedAiFallback,
    DateTimeOffset GeneratedAt);

public sealed record InterviewPrepAnswerRetryResultDto(
    Guid RetryId,
    Guid CandidateTurnId,
    Guid InterviewerTurnId,
    string QuestionText,
    string OriginalAnswerText,
    string? RevisedAnswerText,
    string AnswerSummary,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Gaps,
    string CoachingFeedback,
    IReadOnlyList<string> CoachingTips,
    IReadOnlyList<string> PracticeRecommendations,
    string? ComparisonSummary,
    bool? Improved,
    IReadOnlyList<string> Improvements,
    IReadOnlyList<string> RemainingGaps,
    string Status,
    DateTimeOffset UpdatedAt);
