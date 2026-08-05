namespace ApplyVault.Api.Services.InterviewPrep.Reporting;

internal sealed record InterviewPrepReportArtifact(
    DateTimeOffset GeneratedAt,
    string Disclaimer,
    string Summary,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> DevelopmentAreas,
    IReadOnlyList<InterviewPrepMissingEvidenceArtifact> MissingEvidence,
    InterviewPrepJobCoverageArtifact? JobCoverage,
    IReadOnlyList<InterviewPrepAnswerQualityPatternArtifact> AnswerQualityPatterns,
    IReadOnlyList<string> PracticeRecommendations,
    string OverallConfidence,
    IReadOnlyList<InterviewPrepEvidenceTraceArtifact> EvidenceTrace,
    IReadOnlyList<InterviewPrepStageAssessmentArtifact> StageAssessments,
    IReadOnlyList<InterviewPrepCompetencyResultArtifact> CompetencyResults,
    IReadOnlyList<string>? LanguageFeedback,
    bool UsedAiFallback);

internal sealed record InterviewPrepMissingEvidenceArtifact(
    string Signal,
    string Reason,
    bool IsUnknownNotWeakness);

internal sealed record InterviewPrepJobCoverageArtifact(
    string? JobTitle,
    string? CompanyName,
    IReadOnlyList<InterviewPrepJobRequirementCoverageArtifact> Requirements);

internal sealed record InterviewPrepJobRequirementCoverageArtifact(
    string CompetencyId,
    string DisplayName,
    string CoverageState,
    string? JobAlignmentNote,
    string Confidence);

internal sealed record InterviewPrepAnswerQualityPatternArtifact(
    string Pattern,
    string Detail,
    string Confidence);

internal sealed record InterviewPrepEvidenceTraceArtifact(
    string CompetencyId,
    string Claim,
    string EvidenceQuote,
    int? CandidateTurnSequence,
    string Classification);

internal sealed record InterviewPrepStageAssessmentArtifact(
    string StageKey,
    string Summary,
    IReadOnlyList<string> Highlights,
    IReadOnlyList<string> MissedGoals,
    string Confidence,
    bool UsedAiFallback);

internal sealed record InterviewPrepCompetencyResultArtifact(
    string CompetencyId,
    string DisplayName,
    string CoverageState,
    int EvidenceCount,
    int AttemptCount,
    string Confidence,
    IReadOnlyList<InterviewPrepCompetencyEvidenceArtifact> SupportingEvidence,
    IReadOnlyList<string> ObservedGaps);

internal sealed record InterviewPrepCompetencyEvidenceArtifact(
    string Claim,
    string EvidenceQuote,
    string Classification,
    string Strength,
    string Confidence);
