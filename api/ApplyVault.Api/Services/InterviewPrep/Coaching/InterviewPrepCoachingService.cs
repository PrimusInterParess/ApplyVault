using System.Text.Json;
using ApplyVault.Api.Data;
using ApplyVault.Api.Models.InterviewPrep;
using ApplyVault.Api.Services.InterviewPrep.Ai;
using ApplyVault.Api.Services.InterviewPrep.Ai.Contracts;
using ApplyVault.Api.Services.InterviewPrep.Domain;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Services.InterviewPrep.Coaching;

public interface IInterviewPrepCoachingService
{
    Task<InterviewPrepAnswerReviewDto> RequestReviewAsync(
        AppUserEntity user,
        Guid sessionId,
        Guid candidateTurnId,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepAnswerRetryResultDto> SubmitRetryAsync(
        AppUserEntity user,
        Guid sessionId,
        Guid candidateTurnId,
        InterviewPrepSubmitAnswerRetryRequest request,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepAnswerRetryResultDto> GetRetryAsync(
        AppUserEntity user,
        Guid sessionId,
        Guid candidateTurnId,
        CancellationToken cancellationToken = default);
}

public sealed class InterviewPrepCoachingService(
    ApplyVaultDbContext dbContext,
    IInterviewPrepAiGateway aiGateway) : IInterviewPrepCoachingService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<InterviewPrepAnswerReviewDto> RequestReviewAsync(
        AppUserEntity user,
        Guid sessionId,
        Guid candidateTurnId,
        CancellationToken cancellationToken = default)
    {
        // tracking:true — must persist new/updated CoachingFeedbackJson (AsNoTracking dropped writes).
        var session = await LoadSessionAsync(user.Id, sessionId, tracking: true, cancellationToken);
        EnsureReviewAllowed(session);

        var (candidateTurn, interviewerTurn, attempt) = ResolveCandidateTurnContext(session, candidateTurnId);
        var assessment = DeserializeAssessment(attempt);

        var existing = session.AnswerRetries.FirstOrDefault((retry) => retry.CandidateTurnId == candidateTurnId);
        if (existing is not null && !string.IsNullOrWhiteSpace(existing.CoachingFeedbackJson))
        {
            var cached = DeserializeCoaching(existing.CoachingFeedbackJson);
            // Legacy / empty Model answer: regenerate once so the section can renew.
            if (!string.IsNullOrWhiteSpace(cached.ModelAnswer))
            {
                return MapReview(existing, candidateTurn, interviewerTurn);
            }
        }

        var coaching = await BuildCoachingFeedbackAsync(session, interviewerTurn, candidateTurn, assessment, cancellationToken);
        var utcNow = DateTimeOffset.UtcNow;

        var retry = existing ?? new InterviewPrepAnswerRetryEntity
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            CandidateTurnId = candidateTurn.Id,
            InterviewerTurnId = interviewerTurn.Id,
            OriginalAnswerText = candidateTurn.Text,
            OriginalAssessmentJson = attempt.AssessmentJson,
            Status = InterviewPrepAnswerRetryStatuses.Reviewed,
            CreatedAt = utcNow
        };

        retry.CoachingFeedbackJson = JsonSerializer.Serialize(coaching, JsonOptions);
        retry.Status = InterviewPrepAnswerRetryStatuses.Reviewed;
        retry.UpdatedAt = utcNow;

        if (existing is null)
        {
            session.AnswerRetries.Add(retry);
            dbContext.InterviewPrepAnswerRetries.Add(retry);
        }

        session.UpdatedAt = utcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapReview(retry, candidateTurn, interviewerTurn);
    }

    public async Task<InterviewPrepAnswerRetryResultDto> SubmitRetryAsync(
        AppUserEntity user,
        Guid sessionId,
        Guid candidateTurnId,
        InterviewPrepSubmitAnswerRetryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RevisedAnswer))
        {
            throw new InterviewPrepValidationException("revisedAnswer is required.");
        }

        var session = await LoadSessionAsync(user.Id, sessionId, tracking: true, cancellationToken);
        EnsureRetryAllowed(session);

        var (candidateTurn, interviewerTurn, attempt) = ResolveCandidateTurnContext(session, candidateTurnId);
        _ = attempt;

        var retry = session.AnswerRetries.FirstOrDefault((entry) => entry.CandidateTurnId == candidateTurnId);
        if (retry is null || string.IsNullOrWhiteSpace(retry.CoachingFeedbackJson))
        {
            throw new InterviewPrepConflictException(
                "Request answer review before submitting a revised answer.")
            {
                ErrorCode = "interview_prep_coaching_review_required"
            };
        }

        var revisedText = request.RevisedAnswer.Trim();
        var competencyId = interviewerTurn.CompetencyTag ?? attempt.CompetencyId;

        var revisedAssessment = await AssessRetryAsync(
            session,
            interviewerTurn.Text,
            revisedText,
            competencyId,
            cancellationToken);

        var compareExecution = await aiGateway.CompareAnswerRetryAsync(
            new CompareAnswerRetryRequest(
                interviewerTurn.Text,
                candidateTurn.Text,
                revisedText,
                competencyId),
            cancellationToken);

        var comparison = compareExecution.Value
            ?? new CompareAnswerRetryResponse(
                "Comparison could not be generated.",
                false,
                [],
                []);

        var utcNow = DateTimeOffset.UtcNow;
        retry.RevisedAnswerText = revisedText;
        retry.RevisedAssessmentJson = JsonSerializer.Serialize(revisedAssessment, JsonOptions);
        retry.ComparisonJson = JsonSerializer.Serialize(comparison, JsonOptions);
        retry.Status = InterviewPrepAnswerRetryStatuses.Compared;
        retry.UpdatedAt = utcNow;
        session.UpdatedAt = utcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapRetryResult(retry, candidateTurn, interviewerTurn);
    }

    public async Task<InterviewPrepAnswerRetryResultDto> GetRetryAsync(
        AppUserEntity user,
        Guid sessionId,
        Guid candidateTurnId,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadSessionAsync(user.Id, sessionId, tracking: false, cancellationToken);
        EnsureReviewAllowed(session);

        var (candidateTurn, interviewerTurn, _) = ResolveCandidateTurnContext(session, candidateTurnId);
        var retry = session.AnswerRetries.FirstOrDefault((entry) => entry.CandidateTurnId == candidateTurnId)
            ?? throw new InterviewPrepNotFoundException();

        return MapRetryResult(retry, candidateTurn, interviewerTurn);
    }

    private async Task<PersistedCoachingFeedback> BuildCoachingFeedbackAsync(
        InterviewPrepSessionEntity session,
        InterviewPrepTurnEntity interviewerTurn,
        InterviewPrepTurnEntity candidateTurn,
        AssessAnswerResponse assessment,
        CancellationToken cancellationToken)
    {
        if (!TrySessionConfig(session, out var config))
        {
            throw new InterviewPrepValidationException("Session configuration is invalid.");
        }

        InterviewPrepAiDocumentSnapshot? cv = null;
        InterviewPrepAiDocumentSnapshot? job = null;
        if (!string.IsNullOrWhiteSpace(session.CvSnapshotJson))
        {
            cv = new InterviewPrepAiDocumentSnapshot(session.JobTitle, session.CvSnapshotJson);
        }

        if (!string.IsNullOrWhiteSpace(session.JobSnapshotJson))
        {
            job = new InterviewPrepAiDocumentSnapshot(session.JobTitle, session.JobSnapshotJson);
        }

        var reviewRequest = new GenerateAnswerReviewRequest(
            config,
            interviewerTurn.Text,
            candidateTurn.Text,
            assessment.Strengths,
            assessment.Gaps,
            cv,
            job);

        var execution = await aiGateway.GenerateAnswerReviewAsync(reviewRequest, cancellationToken);
        // Gateway owns safe fallback; defensive coalesce if Value is still null.
        var review = execution.Value
            ?? FakeDeterministicInterviewPrepAiProvider.SafeAnswerReviewFallback(reviewRequest);

        return new PersistedCoachingFeedback(
            string.Empty,
            review.CoachingTips,
            review.PracticeSuggestions,
            assessment.Summary,
            assessment.Strengths,
            assessment.Gaps,
            review.ModelAnswer ?? string.Empty);
    }

    private async Task<AssessAnswerResponse> AssessRetryAsync(
        InterviewPrepSessionEntity session,
        string questionText,
        string answerText,
        string? competencyId,
        CancellationToken cancellationToken)
    {
        InterviewPrepAiDocumentSnapshot? cv = null;
        InterviewPrepAiDocumentSnapshot? job = null;
        if (!string.IsNullOrWhiteSpace(session.CvSnapshotJson))
        {
            cv = new InterviewPrepAiDocumentSnapshot(session.JobTitle, session.CvSnapshotJson);
        }

        if (!string.IsNullOrWhiteSpace(session.JobSnapshotJson))
        {
            job = new InterviewPrepAiDocumentSnapshot(session.JobTitle, session.JobSnapshotJson);
        }

        var execution = await aiGateway.AssessAnswerAsync(
            new AssessAnswerRequest(questionText, answerText, competencyId, cv, job),
            cancellationToken);

        return execution.Value
            ?? FakeDeterministicInterviewPrepAiProvider.SafeAssessFallback(
                new AssessAnswerRequest(questionText, answerText, competencyId, cv, job));
    }

    private static (InterviewPrepTurnEntity Candidate, InterviewPrepTurnEntity Interviewer, InterviewPrepQuestionAttemptEntity Attempt)
        ResolveCandidateTurnContext(InterviewPrepSessionEntity session, Guid candidateTurnId)
    {
        var candidateTurn = session.Turns.FirstOrDefault((turn) => turn.Id == candidateTurnId)
            ?? throw new InterviewPrepNotFoundException();

        if (!string.Equals(
                candidateTurn.Role,
                InterviewPrepPersistence.Role(InterviewPrepTurnRole.Candidate),
                StringComparison.Ordinal))
        {
            throw new InterviewPrepValidationException("Review is only available for candidate turns.");
        }

        var interviewerTurn = session.Turns
            .Where((turn) =>
                turn.Sequence < candidateTurn.Sequence
                && string.Equals(
                    turn.Role,
                    InterviewPrepPersistence.Role(InterviewPrepTurnRole.Interviewer),
                    StringComparison.Ordinal))
            .OrderByDescending((turn) => turn.Sequence)
            .FirstOrDefault()
            ?? throw new InterviewPrepConflictException("No interviewer question found for this answer.");

        var attempt = session.QuestionAttempts
            .FirstOrDefault((entry) => entry.CandidateTurnId == candidateTurnId)
            ?? throw new InterviewPrepConflictException(
                "This answer has not been assessed yet.")
            {
                ErrorCode = "interview_prep_coaching_assessment_pending"
            };

        if (!string.Equals(attempt.AssessmentStatus, "complete", StringComparison.OrdinalIgnoreCase))
        {
            throw new InterviewPrepConflictException(
                "Assessment is still pending for this answer.")
            {
                ErrorCode = "interview_prep_coaching_assessment_pending"
            };
        }

        return (candidateTurn, interviewerTurn, attempt);
    }

    /// <summary>
    /// GuidedCoaching allows in-session review and retry. RealisticSimulation allows review/retry only after the session is completed (post-session coaching).
    /// </summary>
    private static void EnsureReviewAllowed(InterviewPrepSessionEntity session)
    {
        if (!InterviewPrepEnumNames.TryParseSessionStatus(session.Status, out var status))
        {
            throw new InterviewPrepValidationException("Session status is invalid.");
        }

        if (status is InterviewPrepSessionStatus.Cancelled
            or InterviewPrepSessionStatus.Failed
            or InterviewPrepSessionStatus.Created
            or InterviewPrepSessionStatus.Preparing
            or InterviewPrepSessionStatus.Ready)
        {
            throw new InterviewPrepConflictException(
                "Answer review is not available for this session state.")
            {
                ErrorCode = "interview_prep_coaching_session_not_ready"
            };
        }

        if (!InterviewPrepEnumNames.TryParseExperienceType(session.ExperienceType, out var experience))
        {
            throw new InterviewPrepValidationException("Session experienceType is invalid.");
        }

        if (experience == InterviewPrepExperienceType.RealisticSimulation
            && status is InterviewPrepSessionStatus.InProgress
                or InterviewPrepSessionStatus.Paused
                or InterviewPrepSessionStatus.Completing)
        {
            throw new InterviewPrepConflictException(
                "Realistic simulation does not offer in-interview coaching. Complete the session for post-session review.")
            {
                ErrorCode = "interview_prep_coaching_not_allowed_during_simulation"
            };
        }
    }

    private static void EnsureRetryAllowed(InterviewPrepSessionEntity session)
    {
        EnsureReviewAllowed(session);

        if (!InterviewPrepEnumNames.TryParseExperienceType(session.ExperienceType, out var experience))
        {
            throw new InterviewPrepValidationException("Session experienceType is invalid.");
        }

        if (experience == InterviewPrepExperienceType.RealisticSimulation
            && InterviewPrepEnumNames.TryParseSessionStatus(session.Status, out var status)
            && status is InterviewPrepSessionStatus.InProgress
                or InterviewPrepSessionStatus.Paused
                or InterviewPrepSessionStatus.Completing)
        {
            throw new InterviewPrepConflictException(
                "Realistic simulation does not offer in-interview answer retry.")
            {
                ErrorCode = "interview_prep_coaching_not_allowed_during_simulation"
            };
        }
    }

    private async Task<InterviewPrepSessionEntity> LoadSessionAsync(
        Guid userId,
        Guid sessionId,
        bool tracking = false,
        CancellationToken cancellationToken = default)
    {
        IQueryable<InterviewPrepSessionEntity> query = dbContext.InterviewPrepSessions
            .Include((session) => session.Turns)
            .Include((session) => session.QuestionAttempts)
            .Include((session) => session.AnswerRetries);

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var session = await query.FirstOrDefaultAsync(
            (entry) => entry.Id == sessionId && entry.UserId == userId,
            cancellationToken);

        return session ?? throw new InterviewPrepNotFoundException();
    }

    private static AssessAnswerResponse DeserializeAssessment(InterviewPrepQuestionAttemptEntity attempt)
    {
        if (string.IsNullOrWhiteSpace(attempt.AssessmentJson))
        {
            return FakeDeterministicInterviewPrepAiProvider.SafeAssessFallback(
                new AssessAnswerRequest(string.Empty, string.Empty, attempt.CompetencyId, null, null));
        }

        return JsonSerializer.Deserialize<AssessAnswerResponse>(attempt.AssessmentJson, JsonOptions)
            ?? FakeDeterministicInterviewPrepAiProvider.SafeAssessFallback(
                new AssessAnswerRequest(string.Empty, string.Empty, attempt.CompetencyId, null, null));
    }

    private static bool TrySessionConfig(InterviewPrepSessionEntity session, out InterviewPrepAiSessionConfig config)
    {
        config = default!;
        if (!InterviewPrepEnumNames.TryParseMode(session.Mode, out _)
            || !InterviewPrepEnumNames.TryParsePersona(session.Persona, out _)
            || !InterviewPrepEnumNames.TryParseLanguage(session.Language, out _)
            || !InterviewPrepEnumNames.TryParseMarket(session.Market, out _)
            || !InterviewPrepEnumNames.TryParseExperienceType(session.ExperienceType, out _)
            || !InterviewPrepEnumNames.TryParseInteractionType(session.InteractionType, out _))
        {
            return false;
        }

        config = new InterviewPrepAiSessionConfig(
            session.Mode,
            session.Persona,
            session.Language,
            session.Market,
            session.ExperienceType,
            session.InteractionType);
        return true;
    }

    private static InterviewPrepAnswerReviewDto MapReview(
        InterviewPrepAnswerRetryEntity retry,
        InterviewPrepTurnEntity candidateTurn,
        InterviewPrepTurnEntity interviewerTurn)
    {
        var coaching = DeserializeCoaching(retry.CoachingFeedbackJson);
        return new InterviewPrepAnswerReviewDto(
            retry.Id,
            candidateTurn.Id,
            interviewerTurn.Id,
            interviewerTurn.Text,
            candidateTurn.Text,
            coaching.AnswerSummary,
            coaching.Strengths,
            coaching.Gaps,
            coaching.OverallFeedback,
            coaching.ModelAnswer,
            coaching.CoachingTips,
            coaching.PracticeSuggestions,
            retry.Status,
            retry.UpdatedAt);
    }

    private static InterviewPrepAnswerRetryResultDto MapRetryResult(
        InterviewPrepAnswerRetryEntity retry,
        InterviewPrepTurnEntity candidateTurn,
        InterviewPrepTurnEntity interviewerTurn)
    {
        var coaching = DeserializeCoaching(retry.CoachingFeedbackJson);
        CompareAnswerRetryResponse? comparison = null;
        if (!string.IsNullOrWhiteSpace(retry.ComparisonJson))
        {
            comparison = JsonSerializer.Deserialize<CompareAnswerRetryResponse>(retry.ComparisonJson, JsonOptions);
        }

        return new InterviewPrepAnswerRetryResultDto(
            retry.Id,
            candidateTurn.Id,
            interviewerTurn.Id,
            interviewerTurn.Text,
            candidateTurn.Text,
            retry.RevisedAnswerText,
            coaching.AnswerSummary,
            coaching.Strengths,
            coaching.Gaps,
            coaching.OverallFeedback,
            coaching.ModelAnswer,
            coaching.CoachingTips,
            coaching.PracticeSuggestions,
            comparison?.ComparisonSummary,
            comparison?.Improved,
            comparison?.Improvements ?? [],
            comparison?.RemainingGaps ?? [],
            retry.Status,
            retry.UpdatedAt);
    }

    private static PersistedCoachingFeedback EmptyCoaching() =>
        new(string.Empty, [], [], string.Empty, [], [], string.Empty);

    private static PersistedCoachingFeedback DeserializeCoaching(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return EmptyCoaching();
        }

        var coaching = JsonSerializer.Deserialize<PersistedCoachingFeedback>(json, JsonOptions)
            ?? EmptyCoaching();

        // Legacy rows omit modelAnswer; coalesce null/missing → "".
        return coaching with
        {
            OverallFeedback = coaching.OverallFeedback ?? string.Empty,
            CoachingTips = coaching.CoachingTips ?? [],
            PracticeSuggestions = coaching.PracticeSuggestions ?? [],
            AnswerSummary = coaching.AnswerSummary ?? string.Empty,
            Strengths = coaching.Strengths ?? [],
            Gaps = coaching.Gaps ?? [],
            ModelAnswer = coaching.ModelAnswer ?? string.Empty
        };
    }
}
