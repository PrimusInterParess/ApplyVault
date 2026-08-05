using System.Text.Json;
using ApplyVault.Api.Data;
using ApplyVault.Api.Models.InterviewPrep;
using ApplyVault.Api.Services.InterviewPrep.Adapters;
using ApplyVault.Api.Services.InterviewPrep.Catalogs;
using ApplyVault.Api.Services.InterviewPrep.Domain;
using ApplyVault.Api.Services.InterviewPrep.FullLoop;
using ApplyVault.Api.Services.InterviewPrep.Planning;
using ApplyVault.Api.Services.InterviewPrep.Reporting;
using ApplyVault.Api.Services.InterviewPrep.Runtime;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Services.InterviewPrep;

public interface IInterviewPrepSessionService
{
    Task<InterviewPrepSessionSummaryDto> CreateAsync(
        AppUserEntity user,
        InterviewPrepCreateSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepSessionListResponseDto> ListAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepSessionDetailDto> GetAsync(
        AppUserEntity user,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepSessionDetailDto> PrepareAsync(
        AppUserEntity user,
        Guid sessionId,
        string? ifMatch,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepSessionDetailDto> StartAsync(
        AppUserEntity user,
        Guid sessionId,
        string? ifMatch,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepSessionDetailDto> PauseAsync(
        AppUserEntity user,
        Guid sessionId,
        string? ifMatch,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepSessionDetailDto> ResumeAsync(
        AppUserEntity user,
        Guid sessionId,
        string? ifMatch,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepSessionDetailDto> CancelAsync(
        AppUserEntity user,
        Guid sessionId,
        string? ifMatch,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepSessionDetailDto> CompleteAsync(
        AppUserEntity user,
        Guid sessionId,
        string? ifMatch,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        AppUserEntity user,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepTurnSubmitResponseDto> SubmitTurnAsync(
        AppUserEntity user,
        Guid sessionId,
        InterviewPrepSubmitTurnRequest request,
        string? ifMatch,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepSessionDetailDto> StartNextFullLoopStageAsync(
        AppUserEntity user,
        Guid sessionId,
        string? ifMatch,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepSessionDetailDto> ApproveLoopGuardRevisitAsync(
        AppUserEntity user,
        Guid sessionId,
        InterviewPrepLoopGuardRevisitRequest request,
        string? ifMatch,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepPanelDebriefDto> GetPanelDebriefAsync(
        AppUserEntity user,
        Guid sessionId,
        CancellationToken cancellationToken = default);
}

public sealed class InterviewPrepSessionService(
    ApplyVaultDbContext dbContext,
    IInterviewPrepCandidateContextAdapter candidateAdapter,
    IInterviewPrepJobContextAdapter jobAdapter,
    IInterviewPrepQuestionBank questionBank,
    IInterviewContextBuilder contextBuilder,
    IInterviewPlanner planner,
    IInterviewPrepAdaptiveRuntime adaptiveRuntime,
    IInterviewPrepReportingService reportingService,
    IInterviewPrepFullLoopService fullLoopService) : IInterviewPrepSessionService
{
    private static readonly JsonSerializerOptions PlanSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<InterviewPrepSessionSummaryDto> CreateAsync(
        AppUserEntity user,
        InterviewPrepCreateSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateCreateRequest(request);

        var idempotencyKey = NormalizeOptionalKey(request.IdempotencyKey);
        if (idempotencyKey is not null)
        {
            var existing = await dbContext.InterviewPrepSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    (session) => session.UserId == user.Id && session.IdempotencyKey == idempotencyKey,
                    cancellationToken);

            if (existing is not null)
            {
                return MapSummary(existing);
            }
        }

        if (request.ScrapeResultId is Guid scrapeResultId)
        {
            // Validate ownership early; snapshot still happens at Prepare.
            _ = await jobAdapter.CaptureAsync(user, scrapeResultId, cancellationToken);
        }

        var utcNow = DateTimeOffset.UtcNow;
        var entity = new InterviewPrepSessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ScrapeResultId = request.ScrapeResultId,
            Mode = InterviewPrepPersistence.Config(request.Mode),
            Persona = InterviewPrepPersistence.Config(request.Persona),
            Language = InterviewPrepPersistence.Config(request.Language),
            Market = InterviewPrepPersistence.Config(request.Market),
            ExperienceType = InterviewPrepPersistence.Config(request.ExperienceType),
            InteractionType = InterviewPrepPersistence.Config(request.InteractionType),
            Status = InterviewPrepPersistence.Status(InterviewPrepSessionStatus.Created),
            IdempotencyKey = idempotencyKey,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            ConcurrencyStamp = Guid.NewGuid()
        };

        dbContext.InterviewPrepSessions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapSummary(entity);
    }

    public async Task<InterviewPrepSessionListResponseDto> ListAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default)
    {
        var sessions = await dbContext.InterviewPrepSessions
            .AsNoTracking()
            .Where((session) => session.UserId == user.Id)
            .OrderByDescending((session) => session.UpdatedAt)
            .ToListAsync(cancellationToken);

        return new InterviewPrepSessionListResponseDto(sessions.Select(MapSummary).ToArray());
    }

    public async Task<InterviewPrepSessionDetailDto> GetAsync(
        AppUserEntity user,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadOwnedSessionAsync(user.Id, sessionId, tracking: false, cancellationToken);
        return MapDetail(session);
    }

    public async Task<InterviewPrepSessionDetailDto> PrepareAsync(
        AppUserEntity user,
        Guid sessionId,
        string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadOwnedSessionAsync(user.Id, sessionId, tracking: true, cancellationToken);
        EnsureIfMatch(session, ifMatch);

        if (InterviewPrepEnumNames.TryParseSessionStatus(session.Status, out var status)
            && status is InterviewPrepSessionStatus.Ready or InterviewPrepSessionStatus.InProgress
                or InterviewPrepSessionStatus.Paused or InterviewPrepSessionStatus.Completing
                or InterviewPrepSessionStatus.Completed)
        {
            // Idempotent prepare after success.
            return MapDetail(session);
        }

        EnsureSessionTransition(session, InterviewPrepSessionStatus.Preparing);
        var utcNow = DateTimeOffset.UtcNow;
        session.Status = InterviewPrepPersistence.Status(InterviewPrepSessionStatus.Preparing);
        session.UpdatedAt = utcNow;
        BumpToken(session);

        try
        {
            var candidate = await candidateAdapter.CaptureAsync(user, cancellationToken);
            InterviewPrepJobSnapshot? job = null;
            if (session.ScrapeResultId is Guid scrapeResultId)
            {
                job = await jobAdapter.CaptureAsync(user, scrapeResultId, cancellationToken);
            }

            if (!InterviewPrepEnumNames.TryParseMode(session.Mode, out var mode)
                || !InterviewPrepEnumNames.TryParsePersona(session.Persona, out var persona)
                || !InterviewPrepEnumNames.TryParseLanguage(session.Language, out var language)
                || !InterviewPrepEnumNames.TryParseMarket(session.Market, out var market)
                || !InterviewPrepEnumNames.TryParseExperienceType(session.ExperienceType, out var experienceType)
                || !InterviewPrepEnumNames.TryParseInteractionType(session.InteractionType, out var interactionType))
            {
                throw new InterviewPrepValidationException("Session configuration is invalid.");
            }

            session.CvDocumentId = candidate.CvDocumentId;
            session.CvSnapshotJson = candidate.SnapshotJson;
            session.CatalogVersion = candidate.CatalogVersion;
            session.JobSnapshotJson = job?.SnapshotJson;
            session.JobTitle = job?.JobTitle;
            session.CompanyName = job?.CompanyName;
            session.PreparedAt = utcNow;

            var comparison = contextBuilder.CompareSnapshots(session.CvSnapshotJson, session.JobSnapshotJson);
            var planningContext = new InterviewPrepPlanningContext(
                mode,
                persona,
                language,
                market,
                experienceType,
                interactionType,
                comparison);

            // AI proposes; application validates/persists. On AI failure: deterministic minimal brief/plan
            // fallback — session still becomes Ready (never stuck in Preparing without Failed/Ready).
            var brief = await contextBuilder.BuildBriefAsync(planningContext, cancellationToken);
            if (mode == InterviewPrepMode.FullLoop)
            {
                await fullLoopService.PrepareFullLoopAsync(session, planningContext, brief, cancellationToken);
                session.BriefJson = JsonSerializer.Serialize(brief, PlanSerializerOptions);
            }
            else
            {
                var questions = questionBank.GetQuestions(mode, persona);
                var plan = await planner.PlanAsync(planningContext, brief, questions, cancellationToken);

                session.BriefJson = JsonSerializer.Serialize(brief, PlanSerializerOptions);
                session.PlanJson = JsonSerializer.Serialize(plan, PlanSerializerOptions);

                var existingStages = session.Stages.ToList();
                if (existingStages.Count > 0)
                {
                    dbContext.InterviewPrepStages.RemoveRange(existingStages);
                    session.Stages.Clear();
                }

                // Spoken fixed-bank copy remains on stage PlanJson as Loop Guard / wording fallback only.
                // Adaptive runtime (M4+) drives turns from session PlanJson intents + AI gateway.
                var stage = new InterviewPrepStageEntity
                {
                    Id = Guid.NewGuid(),
                    SessionId = session.Id,
                    SortOrder = 0,
                    StageType = InterviewPrepStageTypes.CoreAssessment,
                    Status = InterviewPrepPersistence.Status(InterviewPrepStageStatus.Planned),
                    PlanJson = JsonSerializer.Serialize(
                        questions.Select((question, index) => new StagePlanQuestion(index, question.Text, question.CompetencyTag)).ToArray(),
                        PlanSerializerOptions),
                    CreatedAt = utcNow,
                    UpdatedAt = utcNow
                };

                session.Stages.Add(stage);
                dbContext.InterviewPrepStages.Add(stage);
            }
            EnsureSessionTransition(session, InterviewPrepSessionStatus.Ready);
            session.Status = InterviewPrepPersistence.Status(InterviewPrepSessionStatus.Ready);
            session.UpdatedAt = utcNow;

            await dbContext.SaveChangesAsync(cancellationToken);
            return MapDetail(session);
        }
        catch (InterviewPrepValidationException)
        {
            session.Status = InterviewPrepPersistence.Status(InterviewPrepSessionStatus.Failed);
            session.FailureReason = "prepare_validation_failed";
            session.UpdatedAt = DateTimeOffset.UtcNow;
            BumpToken(session);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Best-effort failure stamp; rethrow original validation error.
            }

            throw;
        }
        catch (Exception ex) when (ex is not InterviewPrepConflictException and not OperationCanceledException)
        {
            session.Status = InterviewPrepPersistence.Status(InterviewPrepSessionStatus.Failed);
            session.FailureReason = "prepare_failed";
            session.UpdatedAt = DateTimeOffset.UtcNow;
            BumpToken(session);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
            }

            throw;
        }
    }

    public async Task<InterviewPrepSessionDetailDto> StartAsync(
        AppUserEntity user,
        Guid sessionId,
        string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadOwnedSessionAsync(user.Id, sessionId, tracking: true, cancellationToken);
        EnsureIfMatch(session, ifMatch);
        EnsureSessionTransition(session, InterviewPrepSessionStatus.InProgress);

        var stage = fullLoopService.GetActiveInterviewStage(session);
        if (stage is null && fullLoopService.IsFullLoopSession(session))
        {
            stage = fullLoopService.GetNextPlannedStage(session);
        }

        stage ??= session.Stages.OrderBy((entry) => entry.SortOrder).FirstOrDefault()
            ?? throw new InterviewPrepValidationException("Session has no stages. Prepare the session first.");

        if (!InterviewPrepEnumNames.TryParseStageStatus(stage.Status, out var stageStatus))
        {
            throw new InterviewPrepValidationException("Stage status is invalid.");
        }

        if (stageStatus == InterviewPrepStageStatus.Planned)
        {
            if (fullLoopService.IsFullLoopSession(session))
            {
                await fullLoopService.OpenPlannedStageAsync(session, stage, cancellationToken);
            }
            else
            {
                InterviewPrepStageStateMachine.EnsureTransition(stageStatus, InterviewPrepStageStatus.Opening);
                stage.Status = InterviewPrepPersistence.Status(InterviewPrepStageStatus.Opening);
                InterviewPrepStageStateMachine.EnsureTransition(InterviewPrepStageStatus.Opening, InterviewPrepStageStatus.CoreAssessment);
                stage.Status = InterviewPrepPersistence.Status(InterviewPrepStageStatus.CoreAssessment);
                stage.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        var utcNow = DateTimeOffset.UtcNow;
        session.Status = InterviewPrepPersistence.Status(InterviewPrepSessionStatus.InProgress);
        session.StartedAt ??= utcNow;
        session.UpdatedAt = utcNow;
        BumpToken(session);

        var hasInterviewer = session.Turns.Any((turn) =>
            string.Equals(turn.Role, InterviewPrepPersistence.Role(InterviewPrepTurnRole.Interviewer), StringComparison.Ordinal));

        // Persist lifecycle transition before AI opening generation (no open DB transaction during Gemini).
        await dbContext.SaveChangesAsync(cancellationToken);

        if (!hasInterviewer)
        {
            await adaptiveRuntime.StartAdaptiveAsync(session, stage, cancellationToken);
            session.UpdatedAt = DateTimeOffset.UtcNow;
            BumpToken(session);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return MapDetail(session);
    }

    public async Task<InterviewPrepSessionDetailDto> PauseAsync(
        AppUserEntity user,
        Guid sessionId,
        string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadOwnedSessionAsync(user.Id, sessionId, tracking: true, cancellationToken);
        EnsureIfMatch(session, ifMatch);
        EnsureSessionTransition(session, InterviewPrepSessionStatus.Paused);
        session.Status = InterviewPrepPersistence.Status(InterviewPrepSessionStatus.Paused);
        session.UpdatedAt = DateTimeOffset.UtcNow;
        BumpToken(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapDetail(session);
    }

    public async Task<InterviewPrepSessionDetailDto> ResumeAsync(
        AppUserEntity user,
        Guid sessionId,
        string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadOwnedSessionAsync(user.Id, sessionId, tracking: true, cancellationToken);
        EnsureIfMatch(session, ifMatch);
        EnsureSessionTransition(session, InterviewPrepSessionStatus.InProgress);
        session.Status = InterviewPrepPersistence.Status(InterviewPrepSessionStatus.InProgress);
        session.UpdatedAt = DateTimeOffset.UtcNow;
        BumpToken(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapDetail(session);
    }

    public async Task<InterviewPrepSessionDetailDto> CancelAsync(
        AppUserEntity user,
        Guid sessionId,
        string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadOwnedSessionAsync(user.Id, sessionId, tracking: true, cancellationToken);
        EnsureIfMatch(session, ifMatch);
        EnsureSessionTransition(session, InterviewPrepSessionStatus.Cancelled);
        session.Status = InterviewPrepPersistence.Status(InterviewPrepSessionStatus.Cancelled);
        session.CancelledAt = DateTimeOffset.UtcNow;
        session.UpdatedAt = session.CancelledAt.Value;
        BumpToken(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapDetail(session);
    }

    public async Task<InterviewPrepSessionDetailDto> CompleteAsync(
        AppUserEntity user,
        Guid sessionId,
        string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadOwnedSessionAsync(user.Id, sessionId, tracking: true, cancellationToken);
        EnsureIfMatch(session, ifMatch);

        if (!InterviewPrepEnumNames.TryParseSessionStatus(session.Status, out var status))
        {
            throw new InterviewPrepValidationException("Session status is invalid.");
        }

        if (status == InterviewPrepSessionStatus.Completed)
        {
            return MapDetail(session);
        }

        if (status == InterviewPrepSessionStatus.InProgress)
        {
            InterviewPrepSessionStateMachine.EnsureTransition(status, InterviewPrepSessionStatus.Completing);
            session.Status = InterviewPrepPersistence.Status(InterviewPrepSessionStatus.Completing);
            status = InterviewPrepSessionStatus.Completing;
        }

        InterviewPrepSessionStateMachine.EnsureTransition(status, InterviewPrepSessionStatus.Completed);
        var utcNow = DateTimeOffset.UtcNow;
        session.Status = InterviewPrepPersistence.Status(InterviewPrepSessionStatus.Completed);
        session.CompletedAt = utcNow;
        session.UpdatedAt = utcNow;

        var stage = session.Stages.OrderBy((entry) => entry.SortOrder).FirstOrDefault();
        if (stage is not null
            && InterviewPrepEnumNames.TryParseStageStatus(stage.Status, out var stageStatus)
            && stageStatus != InterviewPrepStageStatus.Completed)
        {
            if (stageStatus == InterviewPrepStageStatus.CoreAssessment
                || stageStatus == InterviewPrepStageStatus.CandidateQuestions)
            {
                if (stageStatus == InterviewPrepStageStatus.CoreAssessment)
                {
                    InterviewPrepStageStateMachine.EnsureTransition(stageStatus, InterviewPrepStageStatus.Closing);
                }
                else
                {
                    InterviewPrepStageStateMachine.EnsureTransition(stageStatus, InterviewPrepStageStatus.Closing);
                }

                stage.Status = InterviewPrepPersistence.Status(InterviewPrepStageStatus.Closing);
                stageStatus = InterviewPrepStageStatus.Closing;
            }

            if (stageStatus == InterviewPrepStageStatus.Closing)
            {
                InterviewPrepStageStateMachine.EnsureTransition(stageStatus, InterviewPrepStageStatus.Completed);
                stage.Status = InterviewPrepPersistence.Status(InterviewPrepStageStatus.Completed);
            }

            stage.CompletedAt = utcNow;
            stage.UpdatedAt = utcNow;
        }

        BumpToken(session);
        await reportingService.EnsureReportGeneratedAsync(session, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapDetail(session);
    }

    public async Task<bool> DeleteAsync(
        AppUserEntity user,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await dbContext.InterviewPrepSessions
            .FirstOrDefaultAsync((entry) => entry.Id == sessionId && entry.UserId == user.Id, cancellationToken);

        if (session is null)
        {
            return false;
        }

        dbContext.InterviewPrepSessions.Remove(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<InterviewPrepTurnSubmitResponseDto> SubmitTurnAsync(
        AppUserEntity user,
        Guid sessionId,
        InterviewPrepSubmitTurnRequest request,
        string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ClientTurnId))
        {
            throw new InterviewPrepValidationException("clientTurnId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Answer))
        {
            throw new InterviewPrepValidationException("answer is required.");
        }

        var clientTurnId = request.ClientTurnId.Trim();
        var session = await LoadOwnedSessionAsync(user.Id, sessionId, tracking: true, cancellationToken);
        EnsureIfMatch(session, ifMatch);

        var existing = session.Turns.FirstOrDefault((turn) =>
            string.Equals(turn.ClientTurnId, clientTurnId, StringComparison.Ordinal));
        if (existing is not null)
        {
            var nextExisting = session.Turns
                .Where((turn) => turn.Sequence > existing.Sequence)
                .OrderBy((turn) => turn.Sequence)
                .FirstOrDefault((turn) =>
                    string.Equals(turn.Role, InterviewPrepPersistence.Role(InterviewPrepTurnRole.Interviewer), StringComparison.Ordinal));

            return new InterviewPrepTurnSubmitResponseDto(
                MapDetail(session),
                MapTurn(existing),
                nextExisting is null ? null : MapTurn(nextExisting),
                InterviewPrepEnumNames.TryParseSessionStatus(session.Status, out var replayStatus)
                    && replayStatus is InterviewPrepSessionStatus.Completing or InterviewPrepSessionStatus.Completed);
        }

        if (!InterviewPrepEnumNames.TryParseSessionStatus(session.Status, out var status)
            || status != InterviewPrepSessionStatus.InProgress)
        {
            throw new InterviewPrepConflictException(
                "Candidate turns are only accepted while the session is inProgress.");
        }

        var stage = fullLoopService.GetActiveInterviewStage(session)
            ?? throw new InterviewPrepValidationException(
                fullLoopService.IsFullLoopSession(session)
                    ? "No active full-loop stage. Start the next stage first."
                    : "Session has no stages.");

        var pendingQuestion = session.Turns
            .Where((turn) =>
                string.Equals(turn.Role, InterviewPrepPersistence.Role(InterviewPrepTurnRole.Interviewer), StringComparison.Ordinal)
                && !string.Equals(
                    turn.ActionType,
                    InterviewPrepRuntimeNames.ToWire(InterviewPrepRuntimeActionType.Opening),
                    StringComparison.OrdinalIgnoreCase))
            .OrderByDescending((turn) => turn.Sequence)
            .FirstOrDefault()
            ?? session.Turns
                .Where((turn) =>
                    string.Equals(turn.Role, InterviewPrepPersistence.Role(InterviewPrepTurnRole.Interviewer), StringComparison.Ordinal))
                .OrderByDescending((turn) => turn.Sequence)
                .FirstOrDefault()
            ?? throw new InterviewPrepConflictException("No pending interviewer question to answer.");

        // One main question at a time: the latest non-opening interviewer turn must not already have a candidate reply after it.
        var alreadyAnswered = session.Turns.Any((turn) =>
            turn.Sequence > pendingQuestion.Sequence
            && string.Equals(turn.Role, InterviewPrepPersistence.Role(InterviewPrepTurnRole.Candidate), StringComparison.Ordinal));
        if (alreadyAnswered)
        {
            throw new InterviewPrepConflictException(
                "The current interviewer question has already been answered. Wait for the next question.");
        }

        var utcNow = DateTimeOffset.UtcNow;
        var candidateTurn = new InterviewPrepTurnEntity
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            StageId = stage.Id,
            Sequence = NextSequence(session),
            Role = InterviewPrepPersistence.Role(InterviewPrepTurnRole.Candidate),
            Text = request.Answer.Trim(),
            ClientTurnId = clientTurnId,
            Language = pendingQuestion.Language,
            CreatedAt = utcNow
        };

        session.Turns.Add(candidateTurn);
        dbContext.InterviewPrepTurns.Add(candidateTurn);
        session.UpdatedAt = utcNow;
        BumpToken(session);

        // Persist answer BEFORE AI assessment — never lose candidate text if AI fails.
        await dbContext.SaveChangesAsync(cancellationToken);

        var (nextInterviewer, interviewComplete) = await adaptiveRuntime.AdvanceAfterAnswerAsync(
            session,
            stage,
            candidateTurn,
            pendingQuestion,
            cancellationToken);

        session.UpdatedAt = DateTimeOffset.UtcNow;
        BumpToken(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new InterviewPrepTurnSubmitResponseDto(
            MapDetail(session),
            MapTurn(candidateTurn),
            nextInterviewer is null ? null : MapTurn(nextInterviewer),
            interviewComplete);
    }

    public async Task<InterviewPrepSessionDetailDto> StartNextFullLoopStageAsync(
        AppUserEntity user,
        Guid sessionId,
        string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadOwnedSessionAsync(user.Id, sessionId, tracking: true, cancellationToken);
        EnsureIfMatch(session, ifMatch);

        if (!fullLoopService.IsFullLoopSession(session))
        {
            throw new InterviewPrepValidationException("Session is not a full-loop interview.");
        }

        if (!InterviewPrepEnumNames.TryParseSessionStatus(session.Status, out var status)
            || status is not InterviewPrepSessionStatus.InProgress and not InterviewPrepSessionStatus.Ready)
        {
            throw new InterviewPrepConflictException(
                "Full-loop next stage is only available while the session is ready or inProgress.");
        }

        var active = fullLoopService.GetActiveInterviewStage(session);
        if (active is not null
            && InterviewPrepEnumNames.TryParseStageStatus(active.Status, out var activeStatus)
            && activeStatus is not InterviewPrepStageStatus.Completed)
        {
            throw new InterviewPrepConflictException(
                "The current full-loop stage is still active. Complete it before starting the next stage.");
        }

        var next = fullLoopService.GetNextPlannedStage(session)
            ?? throw new InterviewPrepConflictException("No further full-loop stages are planned.");

        if (status == InterviewPrepSessionStatus.Ready)
        {
            InterviewPrepSessionStateMachine.EnsureTransition(status, InterviewPrepSessionStatus.InProgress);
            session.Status = InterviewPrepPersistence.Status(InterviewPrepSessionStatus.InProgress);
            session.StartedAt ??= DateTimeOffset.UtcNow;
        }

        await fullLoopService.OpenPlannedStageAsync(session, next, cancellationToken);
        session.UpdatedAt = DateTimeOffset.UtcNow;
        BumpToken(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        var hasInterviewer = session.Turns.Any((turn) =>
            turn.StageId == next.Id
            && string.Equals(
                turn.Role,
                InterviewPrepPersistence.Role(InterviewPrepTurnRole.Interviewer),
                StringComparison.Ordinal));
        if (!hasInterviewer)
        {
            await adaptiveRuntime.StartAdaptiveAsync(session, next, cancellationToken);
            session.UpdatedAt = DateTimeOffset.UtcNow;
            BumpToken(session);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return MapDetail(session);
    }

    public async Task<InterviewPrepSessionDetailDto> ApproveLoopGuardRevisitAsync(
        AppUserEntity user,
        Guid sessionId,
        InterviewPrepLoopGuardRevisitRequest request,
        string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        var session = await LoadOwnedSessionAsync(user.Id, sessionId, tracking: true, cancellationToken);
        EnsureIfMatch(session, ifMatch);
        fullLoopService.ApproveLoopGuardRevisit(session, request.QuestionSignature, request.Reason);
        session.UpdatedAt = DateTimeOffset.UtcNow;
        BumpToken(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapDetail(session);
    }

    public Task<InterviewPrepPanelDebriefDto> GetPanelDebriefAsync(
        AppUserEntity user,
        Guid sessionId,
        CancellationToken cancellationToken = default) =>
        fullLoopService.GetPanelDebriefAsync(user, sessionId, cancellationToken);

    private async Task<InterviewPrepSessionEntity> LoadOwnedSessionAsync(
        Guid userId,
        Guid sessionId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        IQueryable<InterviewPrepSessionEntity> query = dbContext.InterviewPrepSessions
            .Include((session) => session.Stages)
            .Include((session) => session.Turns)
            .Include((session) => session.EvidenceItems)
            .Include((session) => session.CompetencyCoverages)
            .Include((session) => session.QuestionAttempts);

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var session = await query.FirstOrDefaultAsync(
            (entry) => entry.Id == sessionId && entry.UserId == userId,
            cancellationToken);

        return session ?? throw new InterviewPrepNotFoundException();
    }

    private static void ValidateCreateRequest(InterviewPrepCreateSessionRequest request)
    {
        if (!Enum.IsDefined(request.Mode)
            || !Enum.IsDefined(request.Persona)
            || !Enum.IsDefined(request.Language)
            || !Enum.IsDefined(request.Market)
            || !Enum.IsDefined(request.ExperienceType)
            || !Enum.IsDefined(request.InteractionType))
        {
            throw new InterviewPrepValidationException("One or more session configuration values are invalid.");
        }

        InterviewPrepOperationalCatalog.EnsureOperationalCreate(request.Mode, request.Persona);
        InterviewPrepLanguageMarketCatalog.EnsureOperational(request.Language, request.Market);

        if (request.InteractionType != InterviewPrepInteractionType.Text)
        {
            throw new InterviewPrepValidationException("Only interactionType=text is supported in Milestone 1.");
        }
    }

    private static void EnsureSessionTransition(
        InterviewPrepSessionEntity session,
        InterviewPrepSessionStatus to)
    {
        if (!InterviewPrepEnumNames.TryParseSessionStatus(session.Status, out var from))
        {
            throw new InterviewPrepValidationException("Session status is invalid.");
        }

        InterviewPrepSessionStateMachine.EnsureTransition(from, to);
    }

    private static void EnsureIfMatch(InterviewPrepSessionEntity session, string? ifMatch)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return;
        }

        var expected = NormalizeETag(ifMatch);
        var actual = ToETag(session.ConcurrencyStamp);
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InterviewPrepConflictException("If-Match does not match the current session ETag.")
            {
                ErrorCode = "interview_prep_concurrency_conflict"
            };
        }
    }

    private static string? NormalizeOptionalKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void BumpToken(InterviewPrepSessionEntity session) =>
        session.ConcurrencyStamp = Guid.NewGuid();

    private static int NextSequence(InterviewPrepSessionEntity session) =>
        session.Turns.Count == 0 ? 1 : session.Turns.Max((turn) => turn.Sequence) + 1;

    private static InterviewPrepBriefDto? DeserializeBrief(string? briefJson)
    {
        if (string.IsNullOrWhiteSpace(briefJson))
        {
            return null;
        }

        var brief = JsonSerializer.Deserialize<InterviewBrief>(briefJson, PlanSerializerOptions);
        return brief is null ? null : MapBrief(brief);
    }

    private static InterviewPrepPlanDto? DeserializePlan(string? planJson, string modeWire)
    {
        if (string.IsNullOrWhiteSpace(planJson))
        {
            return null;
        }

        if (InterviewPrepEnumNames.TryParseMode(modeWire, out var mode)
            && mode == InterviewPrepMode.FullLoop)
        {
            var orchestration = InterviewPrepFullLoopSerialization.DeserializeOrchestration(planJson);
            if (orchestration is not null)
            {
                return new InterviewPrepPlanDto(
                    orchestration.LoopSummary,
                    [],
                    [],
                    new InterviewPrepPlanBudgetsDto(0, 0, 0),
                    [],
                    new InterviewPrepCompletionConditionsDto([], 0, false),
                    orchestration.StageSlots
                        .Select((slot) => new InterviewPrepPlanStageDto(slot.StageKey, slot.Goal, slot.CompetencyIds))
                        .ToArray(),
                    orchestration.Source,
                    orchestration.UsedAiFallback);
            }
        }

        var plan = JsonSerializer.Deserialize<InterviewPlan>(planJson, PlanSerializerOptions);
        return plan is null ? null : MapPlan(plan);
    }

    private static InterviewPrepBriefDto MapBrief(InterviewBrief brief) =>
        new(
            brief.Summary,
            brief.Themes,
            brief.Risks,
            brief.TalkingPoints,
            brief.Unknowns
                .Select((unknown) => new InterviewPrepBriefUnknownDto(unknown.Signal, unknown.CoverageState))
                .ToArray(),
            brief.PresentCvSectionTypes,
            brief.JobTitle,
            brief.CompanyName,
            brief.Source,
            brief.UsedAiFallback);

    private static InterviewPrepPlanDto MapPlan(InterviewPlan plan) =>
        new(
            plan.PlanSummary,
            plan.Competencies
                .Select((competency) => new InterviewPrepPlanCompetencyDto(
                    competency.CompetencyId,
                    competency.DisplayName,
                    competency.Priority,
                    competency.InitialCoverageState,
                    competency.ExpectedEvidenceClassification,
                    competency.ExpectedEvidenceStrength,
                    competency.ExpectedEvidenceConfidence))
                .ToArray(),
            plan.QuestionIntents
                .Select((intent) => new InterviewPrepQuestionIntentDto(
                    intent.IntentId,
                    intent.CompetencyId,
                    intent.InternalGoal,
                    intent.SortOrder,
                    intent.ProbeBudget))
                .ToArray(),
            new InterviewPrepPlanBudgetsDto(
                plan.Budgets.MaxQuestions,
                plan.Budgets.MaxProbes,
                plan.Budgets.MaxTurns),
            plan.EvidenceExpectations
                .Select((expectation) => new InterviewPrepEvidenceExpectationDto(
                    expectation.CompetencyId,
                    expectation.Classification,
                    expectation.MinimumStrength,
                    expectation.MinimumConfidence,
                    expectation.TargetCoverageState))
                .ToArray(),
            new InterviewPrepCompletionConditionsDto(
                plan.CompletionConditions.RequiredCompetencyIds,
                plan.CompletionConditions.MinimumCoveredCompetencies,
                plan.CompletionConditions.RequireClosingTurn),
            plan.Stages
                .Select((stage) => new InterviewPrepPlanStageDto(stage.StageKey, stage.Goal, stage.CompetencyIds))
                .ToArray(),
            plan.Source,
            plan.UsedAiFallback);

    private static InterviewPrepSessionSummaryDto MapSummary(InterviewPrepSessionEntity session) =>
        new(
            session.Id,
            session.Status,
            session.Mode,
            session.Persona,
            session.Language,
            session.Market,
            session.ExperienceType,
            session.InteractionType,
            session.ScrapeResultId,
            session.JobTitle,
            session.CompanyName,
            session.CreatedAt,
            session.UpdatedAt,
            session.PreparedAt,
            session.StartedAt,
            session.CompletedAt,
            ToETag(session.ConcurrencyStamp));

    private static InterviewPrepSessionDetailDto MapDetail(InterviewPrepSessionEntity session) =>
        new(
            session.Id,
            session.Status,
            session.Mode,
            session.Persona,
            session.Language,
            session.Market,
            session.ExperienceType,
            session.InteractionType,
            session.ScrapeResultId,
            session.CvDocumentId,
            session.CatalogVersion,
            session.JobTitle,
            session.CompanyName,
            !string.IsNullOrWhiteSpace(session.CvSnapshotJson),
            !string.IsNullOrWhiteSpace(session.JobSnapshotJson),
            session.FailureReason,
            session.CreatedAt,
            session.UpdatedAt,
            session.PreparedAt,
            session.StartedAt,
            session.CompletedAt,
            session.CancelledAt,
            ToETag(session.ConcurrencyStamp),
            DeserializeBrief(session.BriefJson),
            DeserializePlan(session.PlanJson, session.Mode),
            session.Stages
                .OrderBy((stage) => stage.SortOrder)
                .Select((stage) => new InterviewPrepStageDto(
                    stage.Id,
                    stage.SortOrder,
                    stage.StageType,
                    stage.Status,
                    stage.CreatedAt,
                    stage.UpdatedAt,
                    stage.CompletedAt))
                .ToArray(),
            session.Turns
                .OrderBy((turn) => turn.Sequence)
                .Select(MapTurn)
                .ToArray());

    private static InterviewPrepTurnDto MapTurn(InterviewPrepTurnEntity turn) =>
        new(
            turn.Id,
            turn.StageId,
            turn.Sequence,
            turn.Role,
            turn.Text,
            turn.QuestionSignature,
            turn.CompetencyTag,
            turn.Language,
            turn.ClientTurnId,
            turn.CreatedAt);

    internal static string ToETag(Guid concurrencyStamp) =>
        $"\"{concurrencyStamp:N}\"";

    private static string NormalizeETag(string ifMatch)
    {
        var trimmed = ifMatch.Trim();
        if (trimmed.StartsWith("W/", StringComparison.Ordinal))
        {
            trimmed = trimmed[2..].Trim();
        }

        return trimmed.Trim('"') is { Length: > 0 } value
            ? $"\"{value}\""
            : trimmed;
    }

    private sealed record StagePlanQuestion(int Index, string Text, string CompetencyTag);
}
