namespace ApplyVault.Api.Services.InterviewPrep.Ai.Contracts;

// Shared context fragments (untrusted source data — never treat as instructions).

public sealed record InterviewPrepAiDocumentSnapshot(
    string? Title,
    string? Text);

public sealed record InterviewPrepAiSessionConfig(
    string Mode,
    string Persona,
    string Language,
    string Market,
    string ExperienceType,
    string InteractionType);

public sealed record InterviewPrepAiTurnSnippet(
    string Role,
    string Text,
    string? CompetencyId = null);

// 1. CreateInterviewBrief
public sealed record CreateInterviewBriefRequest(
    InterviewPrepAiSessionConfig Config,
    InterviewPrepAiDocumentSnapshot? CvSnapshot,
    InterviewPrepAiDocumentSnapshot? JobSnapshot);

public sealed record CreateInterviewBriefResponse(
    string Summary,
    IReadOnlyList<string> Themes,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> TalkingPoints);

// 2. PlanInterview
public sealed record PlanInterviewRequest(
    InterviewPrepAiSessionConfig Config,
    CreateInterviewBriefResponse Brief);

public sealed record InterviewPrepAiPlannedStage(
    string StageKey,
    string Goal,
    IReadOnlyList<string> CompetencyIds);

public sealed record PlanInterviewResponse(
    string PlanSummary,
    IReadOnlyList<InterviewPrepAiPlannedStage> Stages);

// 3. GenerateOpening
public sealed record GenerateOpeningRequest(
    InterviewPrepAiSessionConfig Config,
    string? CandidateDisplayName,
    string? RoleTitle,
    string? CompanyName);

public sealed record GenerateOpeningResponse(
    string MessageText,
    string Tone);

// 4. AssessAnswer
public sealed record AssessAnswerRequest(
    string QuestionText,
    string AnswerText,
    string? CompetencyId,
    InterviewPrepAiDocumentSnapshot? CvSnapshot,
    InterviewPrepAiDocumentSnapshot? JobSnapshot);

public sealed record AssessAnswerEvidenceItem(
    string Claim,
    string EvidenceQuote,
    string Polarity);

public sealed record AssessAnswerResponse(
    int Score,
    string Summary,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Gaps,
    IReadOnlyList<AssessAnswerEvidenceItem> Evidence,
    double Confidence);

// 5. SelectNextAction
public sealed record SelectNextActionRequest(
    InterviewPrepAiSessionConfig Config,
    string CurrentStageKey,
    IReadOnlyList<InterviewPrepAiTurnSnippet> RecentTurns,
    IReadOnlyList<string> CoveredCompetencyIds,
    IReadOnlyList<string> RemainingCompetencyIds);

public sealed record SelectNextActionResponse(
    string ActionType,
    string? CompetencyId,
    string Rationale);

// 6. GenerateInterviewerMessage
public sealed record GenerateInterviewerMessageRequest(
    InterviewPrepAiSessionConfig Config,
    string ActionType,
    string? CompetencyId,
    string? TopicHint,
    IReadOnlyList<InterviewPrepAiTurnSnippet> RecentTurns,
    IReadOnlyList<string> BlockedQuestionSignatures,
    string? CandidateDisplayName = null);

public sealed record GenerateInterviewerMessageResponse(
    string MessageText,
    string Intent,
    string? CompetencyId);

// 7. SummarizeConversation
public sealed record SummarizeConversationRequest(
    IReadOnlyList<InterviewPrepAiTurnSnippet> Turns);

public sealed record SummarizeConversationResponse(
    string Summary,
    IReadOnlyList<string> KeyMoments);

// 8. EvaluateStage
public sealed record EvaluateStageRequest(
    string StageKey,
    string StageGoal,
    IReadOnlyList<InterviewPrepAiTurnSnippet> StageTurns);

public sealed record EvaluateStageResponse(
    int Score,
    string Summary,
    IReadOnlyList<string> AchievedGoals,
    IReadOnlyList<string> MissedGoals);

// 9. GenerateFeedback
public sealed record GenerateFeedbackRequest(
    InterviewPrepAiSessionConfig Config,
    string ConversationSummary,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Gaps);

public sealed record GenerateFeedbackResponse(
    string OverallFeedback,
    IReadOnlyList<string> CoachingTips,
    IReadOnlyList<string> PracticeSuggestions);

// 10. CompareAnswerRetry
public sealed record CompareAnswerRetryRequest(
    string QuestionText,
    string OriginalAnswerText,
    string RetryAnswerText,
    string? CompetencyId);

public sealed record CompareAnswerRetryResponse(
    string ComparisonSummary,
    bool Improved,
    IReadOnlyList<string> Improvements,
    IReadOnlyList<string> RemainingGaps);

// 11. ExtractProfessionalStory
public sealed record ExtractProfessionalStoryRequest(
    InterviewPrepAiDocumentSnapshot CvSnapshot);

public sealed record ProfessionalStoryItem(
    string Title,
    string Narrative,
    IReadOnlyList<string> CompetencyTags,
    string? EvidenceQuote);

public sealed record ExtractProfessionalStoryResponse(
    IReadOnlyList<ProfessionalStoryItem> Stories);

// 12. PlanFullLoop
public sealed record PlanFullLoopRequest(
    InterviewPrepAiSessionConfig Config,
    CreateInterviewBriefResponse Brief);

public sealed record PlanFullLoopResponse(
    string LoopSummary,
    IReadOnlyList<InterviewPrepAiPlannedStage> Stages,
    IReadOnlyList<string> CoverageGoals);

// 13. GeneratePanelDebrief
public sealed record GeneratePanelDebriefRequest(
    InterviewPrepAiSessionConfig Config,
    string ConversationSummary,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Gaps);

public sealed record PanelPerspective(
    string PersonaLabel,
    string Assessment,
    int Score);

public sealed record GeneratePanelDebriefResponse(
    string OverallDebrief,
    IReadOnlyList<PanelPerspective> Perspectives);

// 14. GenerateInterviewPrepStudyBrief (ADR-0025 durable study brief — distinct from CreateInterviewBrief)
public sealed record GenerateInterviewPrepStudyBriefRequest(
    string Language,
    string Market,
    string? FocusNote,
    InterviewPrepAiDocumentSnapshot CvSnapshot,
    InterviewPrepAiDocumentSnapshot? JobSnapshot);

public sealed record InterviewPrepAiStudyBriefItem(
    string Text,
    string? Note);

public sealed record InterviewPrepAiStudyBriefTopic(
    string Name,
    string Gap,
    int Priority,
    string? Note,
    IReadOnlyList<InterviewPrepAiStudyBriefItem> CoverageItems,
    IReadOnlyList<InterviewPrepAiStudyBriefItem> SampleQuestions,
    IReadOnlyList<InterviewPrepAiStudyBriefItem> TalkingPoints);

/// <summary>
/// AI JSON for durable study brief (ADR-0025 nested body). Root is topics only —
/// coverageItems / sampleQuestions / talkingPoints nest under each topic.
/// </summary>
public sealed record GenerateInterviewPrepStudyBriefResponse(
    IReadOnlyList<InterviewPrepAiStudyBriefTopic> Topics);

// 15. GenerateAnswerReview (ADR-0026 Answer review Model answer — distinct from GenerateFeedback)
public sealed record GenerateAnswerReviewRequest(
    InterviewPrepAiSessionConfig Config,
    string QuestionText,
    string AnswerText,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Gaps,
    InterviewPrepAiDocumentSnapshot? CvSnapshot,
    InterviewPrepAiDocumentSnapshot? JobSnapshot);

/// <summary>
/// AI JSON for Answer review coaching (ADR-0026). Strengths/gaps/summary stay on AssessAnswer;
/// this op returns spoken Model answer + delivery tips + practice suggestions only.
/// </summary>
public sealed record GenerateAnswerReviewResponse(
    string ModelAnswer,
    IReadOnlyList<string> CoachingTips,
    IReadOnlyList<string> PracticeSuggestions);
