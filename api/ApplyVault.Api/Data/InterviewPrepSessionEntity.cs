using ApplyVault.Api.Services.InterviewPrep.Domain;

namespace ApplyVault.Api.Data;

public sealed class InterviewPrepSessionEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid? ScrapeResultId { get; set; }

    public required string Mode { get; set; }

    public required string Persona { get; set; }

    public required string Language { get; set; }

    public required string Market { get; set; }

    public required string ExperienceType { get; set; }

    public required string InteractionType { get; set; }

    public required string Status { get; set; }

    public Guid? CvDocumentId { get; set; }

    public string? CvSnapshotJson { get; set; }

    public string? JobSnapshotJson { get; set; }

    public string? JobTitle { get; set; }

    public string? CompanyName { get; set; }

    public string? CatalogVersion { get; set; }

    /// <summary>Serialized InterviewBrief (camelCase). Public GET maps a safe subset.</summary>
    public string? BriefJson { get; set; }

    /// <summary>Serialized InterviewPlan (camelCase) with internal intents; adaptive runtime drives turns from this.</summary>
    public string? PlanJson { get; set; }

    /// <summary>Private rolling conversation summary — never exposed on public DTOs.</summary>
    public string? ConversationSummary { get; set; }

    /// <summary>Serialized private runtime counters (no-progress streak, follow-ups, etc.).</summary>
    public string? RuntimeStateJson { get; set; }

    /// <summary>Private persisted candidate report artifact (M5) — map to public DTOs only.</summary>
    public string? CandidateReportJson { get; set; }

    /// <summary>Private stage assessment payloads from EvaluateStage — never exposed raw.</summary>
    public string? StageAssessmentsJson { get; set; }

    /// <summary>Private panel debrief artifact for full-loop sessions.</summary>
    public string? PanelDebriefJson { get; set; }

    public string? IdempotencyKey { get; set; }

    public string? FailureReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? PreparedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();

    public List<InterviewPrepStageEntity> Stages { get; set; } = [];

    public List<InterviewPrepTurnEntity> Turns { get; set; } = [];

    public List<InterviewPrepEvidenceItemEntity> EvidenceItems { get; set; } = [];

    public List<InterviewPrepCompetencyCoverageEntity> CompetencyCoverages { get; set; } = [];

    public List<InterviewPrepQuestionAttemptEntity> QuestionAttempts { get; set; } = [];

    public List<InterviewPrepAnswerRetryEntity> AnswerRetries { get; set; } = [];
}

public sealed class InterviewPrepAnswerRetryEntity
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public InterviewPrepSessionEntity? Session { get; set; }

    public Guid CandidateTurnId { get; set; }

    public Guid InterviewerTurnId { get; set; }

    /// <summary>Immutable copy of the original candidate answer (transcript turn is never updated).</summary>
    public required string OriginalAnswerText { get; set; }

    /// <summary>Private original AssessAnswer payload — never exposed on public API.</summary>
    public string? OriginalAssessmentJson { get; set; }

    /// <summary>Serialized coaching feedback for public review mapping only.</summary>
    public string? CoachingFeedbackJson { get; set; }

    public string? RevisedAnswerText { get; set; }

    /// <summary>Private revised AssessAnswer payload.</summary>
    public string? RevisedAssessmentJson { get; set; }

    /// <summary>Serialized CompareAnswerRetry response.</summary>
    public string? ComparisonJson { get; set; }

    public required string Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class InterviewPrepStageEntity
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public InterviewPrepSessionEntity? Session { get; set; }

    public int SortOrder { get; set; }

    public required string StageType { get; set; }

    public required string Status { get; set; }

    public string? PlanJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public List<InterviewPrepTurnEntity> Turns { get; set; } = [];
}

public sealed class InterviewPrepTurnEntity
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public InterviewPrepSessionEntity? Session { get; set; }

    public Guid StageId { get; set; }

    public InterviewPrepStageEntity? Stage { get; set; }

    public int Sequence { get; set; }

    public required string Role { get; set; }

    public required string Text { get; set; }

    public string? QuestionSignature { get; set; }

    public string? CompetencyTag { get; set; }

    public string? IntentId { get; set; }

    public string? ActionType { get; set; }

    public string? TargetEvidenceKey { get; set; }

    public string? ClientTurnId { get; set; }

    /// <summary>Wire language for this turn when session uses Danish or mixed allocation (M9).</summary>
    public string? Language { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class InterviewPrepEvidenceItemEntity
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public InterviewPrepSessionEntity? Session { get; set; }

    public Guid? CandidateTurnId { get; set; }

    public string CompetencyId { get; set; } = string.Empty;

    public string Classification { get; set; } = string.Empty;

    public string Strength { get; set; } = string.Empty;

    public string Confidence { get; set; } = string.Empty;

    public string Claim { get; set; } = string.Empty;

    public string EvidenceQuote { get; set; } = string.Empty;

    public string Polarity { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class InterviewPrepCompetencyCoverageEntity
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public InterviewPrepSessionEntity? Session { get; set; }

    public string CompetencyId { get; set; } = string.Empty;

    public string CoverageState { get; set; } = string.Empty;

    public string? LastProgressClass { get; set; }

    public int AttemptCount { get; set; }

    public int EvidenceCount { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class InterviewPrepQuestionAttemptEntity
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public InterviewPrepSessionEntity? Session { get; set; }

    public Guid? InterviewerTurnId { get; set; }

    public Guid? CandidateTurnId { get; set; }

    public string? IntentId { get; set; }

    public string? CompetencyId { get; set; }

    public string? TargetEvidenceKey { get; set; }

    public string? ProgressClass { get; set; }

    public int? Score { get; set; }

    /// <summary>Private assessment JSON — never mapped to public API.</summary>
    public string? AssessmentJson { get; set; }

    public string AssessmentStatus { get; set; } = "pending";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

public static class InterviewPrepStageTypes
{
    public const string CoreAssessment = "CoreAssessment";
}

public static class InterviewPrepPersistence
{
    public static string Status(InterviewPrepSessionStatus status) =>
        InterviewPrepEnumNames.ToWire(status);

    public static string Status(InterviewPrepStageStatus status) =>
        InterviewPrepEnumNames.ToWire(status);

    public static string Role(InterviewPrepTurnRole role) =>
        InterviewPrepEnumNames.ToWire(role);

    public static string Config(Enum value) =>
        InterviewPrepEnumNames.ToWire(value);
}
