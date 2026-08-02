using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

/// <summary>
/// Gemini coach client for Interview Prep (ADR-0008).
/// Implementation: <see cref="GoogleAiInterviewPrepClient"/> (ai-llm-engineer).
/// </summary>
public interface IInterviewPrepAiClient
{
    Task<InterviewPrepAiTurnResult> GenerateTurnAsync(
        InterviewPrepAiTurnRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Inputs for a single Interview Prep AI turn (CV ± optional job + chat context).
/// </summary>
public sealed record InterviewPrepAiTurnRequest(
    CvStructuredDocumentDto StructuredCv,
    InterviewPrepJobContext? Job,
    string Mode,
    string? LanguageMix,
    string UserMessage,
    IReadOnlyList<InterviewPrepPriorTurn>? PriorTurns = null,
    string? HiringMarket = null);

/// <summary>
/// Read-only job context derived from an owned scrape result (never trust client job text).
/// </summary>
public sealed record InterviewPrepJobContext(
    string? CompanyName,
    string? JobTitle,
    string? Location,
    string? PositionSummary,
    string? JobDescription);

public sealed record InterviewPrepPriorTurn(
    string Role,
    string Text,
    string? Phase = null);

public sealed record InterviewPrepAiTurnResult(
    string Phase,
    InterviewPrepAiInference Inference,
    string CoachMessage,
    InterviewPrepAiScorecard? Scorecard,
    IReadOnlyList<string> FollowUps,
    IReadOnlyList<string> DebriefBullets);

public sealed record InterviewPrepAiInference(
    string Role,
    string Seniority,
    string InterviewStyle,
    bool IsTechnicalContext);

public sealed record InterviewPrepAiScorecard(
    int Overall,
    string? Summary,
    IReadOnlyList<InterviewPrepAiScorecardDimension> Dimensions);

public sealed record InterviewPrepAiScorecardDimension(
    string Id,
    int Score,
    string Note);

public sealed record InterviewPrepAiRawResponse(
    string? Phase,
    InterviewPrepAiRawInference? Inference,
    string? CoachMessage,
    InterviewPrepAiRawScorecard? Scorecard,
    IReadOnlyList<string>? FollowUps,
    IReadOnlyList<string>? DebriefBullets);

public sealed record InterviewPrepAiRawInference(
    string? Role,
    string? Seniority,
    string? InterviewStyle,
    bool? IsTechnicalContext);

public sealed record InterviewPrepAiRawScorecard(
    int Overall,
    string? Summary,
    IReadOnlyList<InterviewPrepAiRawScorecardDimension>? Dimensions);

public sealed record InterviewPrepAiRawScorecardDimension(
    string? Id,
    int Score,
    string? Note);
