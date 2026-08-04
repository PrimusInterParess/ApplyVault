using System.Text;
using System.Text.Json;
using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public interface IInterviewPrepService
{
    Task<InterviewPrepTurnResponseDto> CreateTurnAsync(
        AppUserEntity user,
        InterviewPrepTurnRequest request,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepSessionSummaryDto> CreateSessionAsync(
        AppUserEntity user,
        InterviewPrepCreateSessionRequest request,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepSessionListResponseDto> ListSessionsAsync(
        AppUserEntity user,
        int take,
        int skip,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepSessionDetailDto> GetSessionAsync(
        AppUserEntity user,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteSessionAsync(
        AppUserEntity user,
        Guid sessionId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Thrown when a durable turn targets a completed (read-only) session.
/// Controllers map this to HTTP 409.
/// </summary>
public sealed class InterviewPrepSessionConflictException(string message) : Exception(message);

public sealed class InterviewPrepService(
    ApplyVaultDbContext dbContext,
    ICvStructuredDocumentService structuredDocumentService,
    IScrapeResultStore scrapeResultStore,
    IInterviewPrepAiClient interviewPrepAiClient,
    IOptions<InterviewPrepAiOptions> interviewPrepAiOptions,
    ILogger<InterviewPrepService> logger) : IInterviewPrepService
{
    private const string CoachDuplicateRetryNudge =
        "Corrective instruction (server retry): Your previous coachMessage duplicated an earlier coach question. Ask a NEW competency/topic for this mode. Do not paraphrase the same theme or story.";

    private const string AnsweredQuestionStallRetryNudge =
        "Corrective instruction (server retry): Your previous coachMessage re-asked a question the candidate already answered. Treat the latest user message as an answer even if it is partial. Do not ask for the same elaboration, specifics, outcome, timing, considerations, stakeholder/support details, communication steps, architectural decision, or technical-choice explanation again. Briefly evaluate what is still missing, lower scorecard/listeningNotes/memorySummary as needed, then move to a clearly new competency/topic for this mode, or debrief.";

    private const string UserEchoedCoachQuestionNudge =
        "Corrective instruction: The latest user message appears to repeat the previous coach question instead of answering it. Treat this as a non-answer. Stay on the same agenda step, use interviewMove=challenge_claim or clarify_ambiguity, and respond like a critical but professional interviewer: point out that they have not answered, ask for one concrete example with their action and result, and do not paraphrase the same question neutrally.";

    private const int MaxRecentAnsweredCoachPrompts = 3;
    private const int MinSignificantTokenLength = 6;
    private const int MinTokensForStallCompare = 4;
    private const int MinTokensForImmediatePromptRepeatCompare = 2;
    private const int MinSharedTokensForStall = 3;
    private const int MinSharedTokensForImmediatePromptRepeat = 2;
    private const double AnsweredPromptJaccardThreshold = 0.45;
    private const double AnsweredPromptContainmentThreshold = 0.72;
    private const double ImmediatePromptRepeatJaccardThreshold = 0.25;
    private const double ImmediatePromptRepeatContainmentThreshold = 0.50;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private sealed record InterviewPrepAgendaStep(
        string Id,
        string Label,
        string Goal);

    public async Task<InterviewPrepTurnResponseDto> CreateTurnAsync(
        AppUserEntity user,
        InterviewPrepTurnRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.SessionId is { } sessionId)
        {
            return await CreateDurableTurnAsync(user, sessionId, request, cancellationToken);
        }

        return await CreateEphemeralTurnAsync(user, request, cancellationToken);
    }

    public async Task<InterviewPrepSessionSummaryDto> CreateSessionAsync(
        AppUserEntity user,
        InterviewPrepCreateSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var options = interviewPrepAiOptions.Value;
        var mode = ValidateMode(request.Mode);
        var languageMix = ResolveLanguageMix(request.LanguageMix, options.DefaultLanguageMix);
        var hiringMarket = ResolveHiringMarket(request.HiringMarket, options.DefaultHiringMarket);
        var interviewerProfile = ResolveInterviewerProfile(request.InterviewerProfile, mode);

        string? jobTitle = null;
        string? companyName = null;
        Guid? scrapeResultId = null;

        if (request.ScrapeResultId is { } scrapeId)
        {
            var scrape = await scrapeResultStore.GetByIdAsync(scrapeId, user.Id, cancellationToken)
                ?? throw new KeyNotFoundException("Scrape result was not found.");

            scrapeResultId = scrape.Id;
            var details = scrape.Payload.JobDetails;
            jobTitle = NullIfWhiteSpace(details.JobTitle);
            companyName = NullIfWhiteSpace(details.CompanyName);
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new InterviewPrepSessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Mode = mode,
            LanguageMix = languageMix,
            HiringMarket = hiringMarket,
            InterviewerProfile = interviewerProfile,
            AgendaJson = JsonSerializer.Serialize(BuildAgenda(mode, interviewerProfile), JsonOptions),
            CurrentAgendaStep = "opening",
            ScrapeResultId = scrapeResultId,
            JobTitle = jobTitle,
            CompanyName = companyName,
            Status = InterviewPrepSessionStatuses.InProgress,
            Phase = InterviewPrepPhases.Interview,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.InterviewPrepSessions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapSessionSummary(entity);
    }

    public async Task<InterviewPrepSessionListResponseDto> ListSessionsAsync(
        AppUserEntity user,
        int take,
        int skip,
        CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take <= 0 ? 20 : take, 1, 50);
        skip = Math.Max(0, skip);

        var query = dbContext.InterviewPrepSessions
            .AsNoTracking()
            .Where((session) => session.UserId == user.Id);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending((session) => session.UpdatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return new InterviewPrepSessionListResponseDto(
            items.Select(MapSessionSummary).ToArray(),
            totalCount);
    }

    public async Task<InterviewPrepSessionDetailDto> GetSessionAsync(
        AppUserEntity user,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await dbContext.InterviewPrepSessions
            .AsNoTracking()
            .Include((entity) => entity.Messages.OrderBy((message) => message.Sequence))
            .SingleOrDefaultAsync(
                (entity) => entity.Id == sessionId && entity.UserId == user.Id,
                cancellationToken)
            ?? throw new KeyNotFoundException("Interview Prep session was not found.");

        return MapSessionDetail(session);
    }

    public async Task<bool> DeleteSessionAsync(
        AppUserEntity user,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await dbContext.InterviewPrepSessions
            .SingleOrDefaultAsync(
                (entity) => entity.Id == sessionId && entity.UserId == user.Id,
                cancellationToken);

        if (session is null)
        {
            return false;
        }

        dbContext.InterviewPrepSessions.Remove(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<InterviewPrepTurnResponseDto> CreateEphemeralTurnAsync(
        AppUserEntity user,
        InterviewPrepTurnRequest request,
        CancellationToken cancellationToken)
    {
        var options = interviewPrepAiOptions.Value;

        var mode = ValidateMode(request.Mode);
        var languageMix = ResolveLanguageMix(request.LanguageMix, options.DefaultLanguageMix);
        var hiringMarket = ResolveHiringMarket(request.HiringMarket, options.DefaultHiringMarket);
        var userMessage = CapUserMessage(request.UserMessage, options.MaxUserMessageChars);
        // Digest = coach questions outside MaxPriorTurns window (ADR-0017); recent stay in priorTurns.
        var alreadyAsked = InterviewPrepAlreadyAskedDigest.Build(
            request.PriorTurns,
            options.MaxPriorTurns,
            options.MaxAlreadyAskedItems,
            options.MaxAlreadyAskedItemChars,
            options.MaxAlreadyAskedTotalChars);
        var priorTurns = NormalizePriorTurns(
            request.PriorTurns,
            options.MaxPriorTurns,
            options.MaxPriorTurnChars);

        var structured = await LoadStructuredCvAsync(user, cancellationToken);
        var jobContext = await LoadJobContextAsync(
            user,
            request.ScrapeResultId,
            requireOwnedScrape: true,
            cancellationToken);

        var aiResult = await GenerateTurnWithDuplicateGateAsync(
            new InterviewPrepAiTurnRequest(
                structured,
                jobContext,
                mode,
                languageMix,
                userMessage,
                priorTurns,
                hiringMarket,
                alreadyAsked,
                BuildEphemeralSessionState(mode)),
            request.PriorTurns,
            cancellationToken);

        return MapResponse(aiResult, sessionId: null);
    }

    private async Task<InterviewPrepTurnResponseDto> CreateDurableTurnAsync(
        AppUserEntity user,
        Guid sessionId,
        InterviewPrepTurnRequest request,
        CancellationToken cancellationToken)
    {
        var options = interviewPrepAiOptions.Value;

        var session = await dbContext.InterviewPrepSessions
            .Include((entity) => entity.Messages)
            .SingleOrDefaultAsync(
                (entity) => entity.Id == sessionId && entity.UserId == user.Id,
                cancellationToken)
            ?? throw new KeyNotFoundException("Interview Prep session was not found.");

        if (session.Status == InterviewPrepSessionStatuses.Completed)
        {
            throw new InterviewPrepSessionConflictException(
                "This Interview Prep session is completed and read-only.");
        }

        var messageCount = session.Messages.Count;
        if (messageCount + 2 > options.MaxMessagesPerSession)
        {
            throw new InvalidOperationException(
                $"Session message limit of {options.MaxMessagesPerSession} has been reached.");
        }

        // Session-wins: ignore client mode / languageMix / hiringMarket / scrape / priorTurns.
        var userMessage = CapUserMessage(request.UserMessage, options.MaxUserMessageChars);
        var priorTurnDtos = session.Messages
            .OrderBy((message) => message.Sequence)
            .Select((message) => new InterviewPrepPriorTurnDto(message.Role, message.Text, message.Phase))
            .ToArray();
        // Digest = coach questions outside MaxPriorTurns window (ADR-0017); recent stay in priorTurns.
        // modelAnswer is never mapped into priorTurnDtos (ADR-0015).
        var alreadyAsked = InterviewPrepAlreadyAskedDigest.Build(
            priorTurnDtos,
            options.MaxPriorTurns,
            options.MaxAlreadyAskedItems,
            options.MaxAlreadyAskedItemChars,
            options.MaxAlreadyAskedTotalChars);
        var priorTurns = NormalizePriorTurns(
            priorTurnDtos,
            options.MaxPriorTurns,
            options.MaxPriorTurnChars);

        var structured = await LoadStructuredCvAsync(user, cancellationToken);
        var jobContext = await LoadJobContextAsync(
            user,
            session.ScrapeResultId,
            requireOwnedScrape: false,
            cancellationToken);

        var aiResult = await GenerateTurnWithDuplicateGateAsync(
            new InterviewPrepAiTurnRequest(
                structured,
                jobContext,
                session.Mode,
                session.LanguageMix,
                userMessage,
                priorTurns,
                session.HiringMarket,
                alreadyAsked,
                BuildSessionState(session)),
            priorTurnDtos,
            cancellationToken);

        var response = MapResponse(aiResult, session.Id);
        var now = DateTimeOffset.UtcNow;
        var nextSequence = messageCount == 0
            ? 0
            : session.Messages.Max((message) => message.Sequence) + 1;

        var userEntity = new InterviewPrepSessionMessageEntity
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Sequence = nextSequence,
            Role = InterviewPrepTurnRoles.User,
            Text = userMessage,
            Phase = session.Phase,
            CreatedAt = now
        };

        var coachEntity = new InterviewPrepSessionMessageEntity
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            Sequence = nextSequence + 1,
            Role = InterviewPrepTurnRoles.Coach,
            Text = response.CoachMessage,
            Phase = response.Phase,
            ScorecardJson = SerializeOptional(response.Scorecard),
            FollowUpsJson = JsonSerializer.Serialize(response.FollowUps, JsonOptions),
            DebriefBulletsJson = JsonSerializer.Serialize(response.DebriefBullets, JsonOptions),
            ModelAnswer = response.ModelAnswer,
            InferenceJson = JsonSerializer.Serialize(response.Inference, JsonOptions),
            TurnStateJson = SerializeOptional(response.TurnState),
            CreatedAt = now
        };

        dbContext.InterviewPrepSessionMessages.Add(userEntity);
        dbContext.InterviewPrepSessionMessages.Add(coachEntity);

        session.Phase = response.Phase;
        session.InferenceJson = coachEntity.InferenceJson;
        session.LatestInterviewMove = response.TurnState?.InterviewMove;
        session.CurrentAgendaStep = ResolveNextAgendaStep(
            session.AgendaJson,
            session.CurrentAgendaStep,
            response.TurnState,
            response.Phase);
        if (!string.IsNullOrWhiteSpace(response.TurnState?.MemorySummary))
        {
            session.InterviewerMemoryJson = JsonSerializer.Serialize(
                new { summary = response.TurnState.MemorySummary },
                JsonOptions);
        }

        if (response.Scorecard is { } scorecard)
        {
            session.LatestScorecardJson = coachEntity.ScorecardJson;
            session.LatestOverallScore = scorecard.Overall;
        }

        session.UpdatedAt = now;

        if (response.Phase == InterviewPrepPhases.Debrief)
        {
            session.Status = InterviewPrepSessionStatuses.Completed;
            session.CompletedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return response;
    }

    private async Task<CvStructuredDocumentDto> LoadStructuredCvAsync(
        AppUserEntity user,
        CancellationToken cancellationToken)
    {
        var structured = await structuredDocumentService.GetStructuredAsync(user, cancellationToken)
            ?? throw new KeyNotFoundException("Structured CV content was not found.");

        if (structured.Sections.Count == 0)
        {
            throw new InvalidOperationException(
                "Import or create structured CV sections before starting Interview Prep.");
        }

        return structured;
    }

    private async Task<InterviewPrepJobContext?> LoadJobContextAsync(
        AppUserEntity user,
        Guid? scrapeResultId,
        bool requireOwnedScrape,
        CancellationToken cancellationToken)
    {
        if (scrapeResultId is not { } id)
        {
            return null;
        }

        var scrape = await scrapeResultStore.GetByIdAsync(id, user.Id, cancellationToken);
        if (scrape is null)
        {
            if (requireOwnedScrape)
            {
                throw new KeyNotFoundException("Scrape result was not found.");
            }

            // Durable sessions keep title/company snapshots; scrape may be gone (SET NULL / soft-delete).
            return null;
        }

        var details = scrape.Payload.JobDetails;
        return new InterviewPrepJobContext(
            details.CompanyName,
            details.JobTitle,
            details.Location,
            details.PositionSummary,
            details.JobDescription);
    }

    private static string ValidateMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode) || !InterviewPrepModes.All.Contains(mode))
        {
            throw new InvalidOperationException(
                "mode must be one of: screening, behavioral, role_domain, problem_solving, process_systems, language_practice, full_loop.");
        }

        return mode;
    }

    private static string ResolveLanguageMix(string? languageMix, string defaultLanguageMix)
    {
        var resolved = string.IsNullOrWhiteSpace(languageMix)
            ? defaultLanguageMix
            : languageMix;

        if (!InterviewPrepLanguageMixes.All.Contains(resolved))
        {
            throw new InvalidOperationException(
                "languageMix must be one of: en, da, mixed.");
        }

        return resolved;
    }

    private static string ResolveHiringMarket(string? hiringMarket, string defaultHiringMarket)
    {
        var resolved = string.IsNullOrWhiteSpace(hiringMarket)
            ? defaultHiringMarket
            : hiringMarket;

        if (!InterviewPrepHiringMarkets.All.Contains(resolved))
        {
            throw new InvalidOperationException(
                "hiringMarket must be one of: general, dk.");
        }

        return resolved;
    }

    private static string ResolveInterviewerProfile(string? interviewerProfile, string mode)
    {
        if (!string.IsNullOrWhiteSpace(interviewerProfile))
        {
            var resolved = interviewerProfile.Trim();
            if (!InterviewPrepInterviewerProfiles.All.Contains(resolved))
            {
                throw new InvalidOperationException(
                    "interviewerProfile must be one of: recruiter, hiring_manager, senior_peer, bar_raiser.");
            }

            return resolved;
        }

        return mode switch
        {
            InterviewPrepModes.Screening or InterviewPrepModes.LanguagePractice =>
                InterviewPrepInterviewerProfiles.Recruiter,
            InterviewPrepModes.ProblemSolving or InterviewPrepModes.ProcessSystems =>
                InterviewPrepInterviewerProfiles.SeniorPeer,
            InterviewPrepModes.FullLoop => InterviewPrepInterviewerProfiles.HiringManager,
            _ => InterviewPrepInterviewerProfiles.HiringManager
        };
    }

    private static InterviewPrepAiSessionState BuildEphemeralSessionState(string mode) =>
        new(
            ResolveInterviewerProfile(null, mode),
            JsonSerializer.Serialize(BuildAgenda(mode, ResolveInterviewerProfile(null, mode)), JsonOptions),
            "opening",
            null);

    private static InterviewPrepAiSessionState BuildSessionState(InterviewPrepSessionEntity session) =>
        new(
            session.InterviewerProfile,
            string.IsNullOrWhiteSpace(session.AgendaJson) ? "[]" : session.AgendaJson,
            string.IsNullOrWhiteSpace(session.CurrentAgendaStep) ? "opening" : session.CurrentAgendaStep,
            session.InterviewerMemoryJson);

    private static IReadOnlyList<InterviewPrepAgendaStep> BuildAgenda(
        string mode,
        string interviewerProfile)
    {
        var opening = new InterviewPrepAgendaStep(
            "opening",
            "Opening",
            "Set context, establish interview tone, and ask the first natural question.");
        var close = new InterviewPrepAgendaStep(
            "candidate_questions",
            "Candidate questions",
            "Invite concise candidate questions or close the simulated round naturally.");
        var debrief = new InterviewPrepAgendaStep(
            "debrief",
            "Debrief",
            "Summarize performance across the whole agenda with concrete next-practice advice.");

        IReadOnlyList<InterviewPrepAgendaStep> steps = mode switch
        {
            InterviewPrepModes.Screening =>
            [
                opening,
                new("motivation_fit", "Motivation and fit", "Probe role motivation, company interest, and career direction."),
                new("cv_walkthrough", "CV walkthrough", "Ask for a concise walkthrough grounded in the Structured CV."),
                close,
                debrief
            ],
            InterviewPrepModes.Behavioral =>
            [
                opening,
                new("behavior_story", "Behavioral story", "Ask for one real story with situation, action, and result."),
                new("evidence_probe", "Evidence probe", "Probe ownership, trade-offs, conflict, or learning without looping."),
                close,
                debrief
            ],
            InterviewPrepModes.ProblemSolving =>
            [
                opening,
                new("case_setup", "Scenario setup", "Present a realistic problem for the inferred profession."),
                new("approach_tradeoffs", "Approach and trade-offs", "Probe clarifying questions, prioritization, and decision criteria."),
                close,
                debrief
            ],
            InterviewPrepModes.ProcessSystems =>
            [
                opening,
                new("process_map", "Process map", "Ask how work flows end to end in the candidate's profession."),
                new("failure_modes", "Failure modes", "Probe handoffs, bottlenecks, quality checks, and improvements."),
                close,
                debrief
            ],
            InterviewPrepModes.LanguagePractice =>
            [
                opening,
                new("phrasing_practice", "Phrasing practice", "Ask a realistic interview question and focus on clarity and natural wording."),
                new("rephrase_probe", "Rephrase probe", "Ask for a tighter or more natural version without changing facts."),
                close,
                debrief
            ],
            InterviewPrepModes.FullLoop =>
            [
                opening,
                new("motivation_fit", "Motivation and fit", "Start like a recruiter with motivation and role fit."),
                new("behavior_story", "Behavioral story", "Probe one concrete past situation."),
                new("role_depth", "Role depth", "Probe role/domain judgment using CV and job evidence."),
                new("scenario_probe", "Scenario probe", "Ask a practical scenario or case question."),
                close,
                debrief
            ],
            _ =>
            [
                opening,
                new("role_depth", "Role depth", "Probe role/domain judgment using CV and job evidence."),
                new("evidence_probe", "Evidence probe", "Ask one focused follow-up to test evidence and ownership."),
                close,
                debrief
            ]
        };

        if (interviewerProfile == InterviewPrepInterviewerProfiles.BarRaiser
            && !steps.Any((step) => step.Id == "challenge_claims"))
        {
            var mutable = steps.ToList();
            mutable.Insert(
                Math.Max(1, mutable.Count - 2),
                new InterviewPrepAgendaStep(
                    "challenge_claims",
                    "Challenge claims",
                    "Professionally challenge weak evidence or unsupported claims."));
            return mutable;
        }

        return steps;
    }

    private static string ResolveNextAgendaStep(
        string agendaJson,
        string currentAgendaStep,
        InterviewPrepTurnStateDto? turnState,
        string phase)
    {
        if (string.Equals(phase, InterviewPrepPhases.Debrief, StringComparison.Ordinal))
        {
            return "debrief";
        }

        var candidate = NullIfWhiteSpace(turnState?.NextAgendaStep)
            ?? NullIfWhiteSpace(turnState?.AgendaStep)
            ?? NullIfWhiteSpace(currentAgendaStep)
            ?? "opening";

        var steps = DeserializeOptional<List<InterviewPrepAgendaStep>>(agendaJson) ?? [];
        if (steps.Count == 0 || steps.Any((step) => step.Id == candidate))
        {
            return candidate;
        }

        return currentAgendaStep;
    }

    private async Task<InterviewPrepAiTurnResult> GenerateTurnWithDuplicateGateAsync(
        InterviewPrepAiTurnRequest aiRequest,
        IReadOnlyList<InterviewPrepPriorTurnDto>? fullPriorTurns,
        CancellationToken cancellationToken)
    {
        var maxRetries = Math.Clamp(interviewPrepAiOptions.Value.MaxCoachDuplicateRetries, 0, 2);
        var priorCoachTexts = CollectCoachInterviewTexts(fullPriorTurns);
        var recentAnsweredCoachPrompts = CollectRecentAnsweredCoachPrompts(
            fullPriorTurns,
            aiRequest.UserMessage);
        var effectiveRequest = IsUserEchoingLastCoachPrompt(fullPriorTurns, aiRequest.UserMessage)
            ? aiRequest with { CorrectiveNudge = CombineCorrectiveNudges(aiRequest.CorrectiveNudge, UserEchoedCoachQuestionNudge) }
            : aiRequest;

        var result = await interviewPrepAiClient.GenerateTurnAsync(effectiveRequest, cancellationToken);

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            if (IsExactDuplicateCoachInterviewMessage(result, priorCoachTexts))
            {
                logger.LogInformation(
                    "Interview Prep coachMessage exact duplicate detected; silent regenerate {Attempt}/{MaxRetries}.",
                    attempt + 1,
                    maxRetries);

                result = await interviewPrepAiClient.GenerateTurnAsync(
                    effectiveRequest with
                    {
                        CorrectiveNudge = CombineCorrectiveNudges(
                            effectiveRequest.CorrectiveNudge,
                            CoachDuplicateRetryNudge)
                    },
                    cancellationToken);

                continue;
            }

            if (IsAnsweredQuestionStall(result, recentAnsweredCoachPrompts))
            {
                logger.LogInformation(
                    "Interview Prep answered-question stall detected; silent regenerate {Attempt}/{MaxRetries}.",
                    attempt + 1,
                    maxRetries);

                result = await interviewPrepAiClient.GenerateTurnAsync(
                    effectiveRequest with
                    {
                        CorrectiveNudge = CombineCorrectiveNudges(
                            effectiveRequest.CorrectiveNudge,
                            AnsweredQuestionStallRetryNudge)
                    },
                    cancellationToken);

                continue;
            }

            return result;
        }

        if (IsExactDuplicateCoachInterviewMessage(result, priorCoachTexts))
        {
            logger.LogWarning(
                "Interview Prep coachMessage still an exact duplicate after {MaxRetries} regenerat(e/es); accepting last result.",
                maxRetries);
        }
        else if (IsAnsweredQuestionStall(result, recentAnsweredCoachPrompts))
        {
            logger.LogWarning(
                "Interview Prep coachMessage still appears to re-ask a recently answered prompt after {MaxRetries} regenerat(e/es); accepting last result.",
                maxRetries);
        }

        return result;
    }

    private static bool IsUserEchoingLastCoachPrompt(
        IReadOnlyList<InterviewPrepPriorTurnDto>? priorTurns,
        string currentUserMessage)
    {
        if (priorTurns is null || priorTurns.Count == 0 || string.IsNullOrWhiteSpace(currentUserMessage))
        {
            return false;
        }

        var lastCoachPrompt = priorTurns
            .LastOrDefault((turn) =>
                string.Equals(turn.Role, InterviewPrepTurnRoles.Coach, StringComparison.Ordinal)
                && string.Equals(
                    string.IsNullOrWhiteSpace(turn.Phase) ? InterviewPrepPhases.Interview : turn.Phase,
                    InterviewPrepPhases.Interview,
                    StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(turn.Text));

        if (lastCoachPrompt is null)
        {
            return false;
        }

        if (string.Equals(
                NormalizeForDuplicateCompare(currentUserMessage),
                NormalizeForDuplicateCompare(lastCoachPrompt.Text),
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var userTokens = TokenizeForStallCompare(currentUserMessage);
        var coachTokens = TokenizeForStallCompare(lastCoachPrompt.Text);
        return userTokens.Count >= MinTokensForStallCompare
            && coachTokens.Count >= MinTokensForStallCompare
            && HasHighLexicalOverlap(userTokens, coachTokens);
    }

    private static string CombineCorrectiveNudges(string? existing, string next)
    {
        if (string.IsNullOrWhiteSpace(existing))
        {
            return next;
        }

        return existing.Trim() + "\n\n" + next;
    }

    private static List<string> CollectRecentAnsweredCoachPrompts(
        IReadOnlyList<InterviewPrepPriorTurnDto>? priorTurns,
        string currentUserMessage)
    {
        var timeline = new List<InterviewPrepPriorTurnDto>();
        if (priorTurns is not null)
        {
            timeline.AddRange(priorTurns);
        }

        if (!string.IsNullOrWhiteSpace(currentUserMessage))
        {
            timeline.Add(new InterviewPrepPriorTurnDto(
                InterviewPrepTurnRoles.User,
                currentUserMessage,
                InterviewPrepPhases.Interview));
        }

        var prompts = new List<string>();
        for (var index = timeline.Count - 2; index >= 0; index--)
        {
            if (prompts.Count >= MaxRecentAnsweredCoachPrompts)
            {
                break;
            }

            var coachTurn = timeline[index];
            var userTurn = timeline[index + 1];

            if (!string.Equals(coachTurn.Role, InterviewPrepTurnRoles.Coach, StringComparison.Ordinal)
                || !string.Equals(userTurn.Role, InterviewPrepTurnRoles.User, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(coachTurn.Text)
                || string.IsNullOrWhiteSpace(userTurn.Text))
            {
                continue;
            }

            var coachPhase = string.IsNullOrWhiteSpace(coachTurn.Phase)
                ? InterviewPrepPhases.Interview
                : coachTurn.Phase;

            if (!string.Equals(coachPhase, InterviewPrepPhases.Interview, StringComparison.Ordinal))
            {
                continue;
            }

            prompts.Add(coachTurn.Text);
        }

        return prompts;
    }

    private static bool IsAnsweredQuestionStall(
        InterviewPrepAiTurnResult result,
        IReadOnlyList<string> recentAnsweredCoachPrompts)
    {
        if (recentAnsweredCoachPrompts.Count == 0)
        {
            return false;
        }

        if (!string.Equals(result.Phase, InterviewPrepPhases.Interview, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var generatedTokens = TokenizeForStallCompare(result.CoachMessage);
        if (generatedTokens.Count == 0)
        {
            return false;
        }

        for (var index = 0; index < recentAnsweredCoachPrompts.Count; index++)
        {
            var prompt = recentAnsweredCoachPrompts[index];
            var promptTokens = TokenizeForStallCompare(prompt);
            if (index == 0 && IsImmediateAnsweredPromptRepeat(result.CoachMessage, prompt, generatedTokens, promptTokens))
            {
                return true;
            }

            if (generatedTokens.Count < MinTokensForStallCompare
                || promptTokens.Count < MinTokensForStallCompare)
            {
                continue;
            }

            if (HasHighLexicalOverlap(generatedTokens, promptTokens))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsImmediateAnsweredPromptRepeat(
        string coachMessage,
        string latestAnsweredCoachPrompt,
        HashSet<string> generatedTokens,
        HashSet<string> promptTokens)
    {
        var normalizedCoachMessage = NormalizeForDuplicateCompare(coachMessage);
        var normalizedPrompt = NormalizeForDuplicateCompare(latestAnsweredCoachPrompt);
        if (normalizedCoachMessage.Length == 0 || normalizedPrompt.Length == 0)
        {
            return false;
        }

        if (normalizedCoachMessage.Contains(normalizedPrompt, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return generatedTokens.Count >= MinTokensForImmediatePromptRepeatCompare
            && promptTokens.Count >= MinTokensForImmediatePromptRepeatCompare
            && HasImmediatePromptRepeatOverlap(generatedTokens, promptTokens);
    }

    private static List<string> CollectCoachInterviewTexts(
        IReadOnlyList<InterviewPrepPriorTurnDto>? priorTurns)
    {
        var texts = new List<string>();
        if (priorTurns is null || priorTurns.Count == 0)
        {
            return texts;
        }

        foreach (var turn in priorTurns)
        {
            if (!string.Equals(turn.Role, InterviewPrepTurnRoles.Coach, StringComparison.Ordinal))
            {
                continue;
            }

            var phase = string.IsNullOrWhiteSpace(turn.Phase)
                ? InterviewPrepPhases.Interview
                : turn.Phase;

            if (!string.Equals(phase, InterviewPrepPhases.Interview, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(turn.Text))
            {
                continue;
            }

            texts.Add(turn.Text);
        }

        return texts;
    }

    private static bool IsExactDuplicateCoachInterviewMessage(
        InterviewPrepAiTurnResult result,
        IReadOnlyList<string> priorCoachTexts)
    {
        if (priorCoachTexts.Count == 0)
        {
            return false;
        }

        if (!string.Equals(result.Phase, InterviewPrepPhases.Interview, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var normalized = NormalizeForDuplicateCompare(result.CoachMessage);
        if (normalized.Length == 0)
        {
            return false;
        }

        foreach (var prior in priorCoachTexts)
        {
            if (string.Equals(
                    normalized,
                    NormalizeForDuplicateCompare(prior),
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> TokenizeForStallCompare(string text)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
        {
            return tokens;
        }

        var builder = new StringBuilder();
        foreach (var ch in text)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                continue;
            }

            AddStallCompareToken(tokens, builder);
        }

        AddStallCompareToken(tokens, builder);
        return tokens;
    }

    private static void AddStallCompareToken(
        HashSet<string> tokens,
        StringBuilder builder)
    {
        if (builder.Length < MinSignificantTokenLength)
        {
            builder.Clear();
            return;
        }

        var token = builder.ToString();
        builder.Clear();

        tokens.Add(token);
    }

    private static bool HasHighLexicalOverlap(
        HashSet<string> generatedTokens,
        HashSet<string> answeredPromptTokens)
    {
        var shared = 0;
        foreach (var token in generatedTokens)
        {
            if (answeredPromptTokens.Contains(token))
            {
                shared++;
            }
        }

        if (shared < MinSharedTokensForStall)
        {
            return false;
        }

        var union = generatedTokens.Count + answeredPromptTokens.Count - shared;
        var jaccard = union == 0 ? 0 : (double)shared / union;
        var containment = (double)shared / Math.Min(generatedTokens.Count, answeredPromptTokens.Count);

        return jaccard >= AnsweredPromptJaccardThreshold
            || containment >= AnsweredPromptContainmentThreshold;
    }

    private static bool HasImmediatePromptRepeatOverlap(
        HashSet<string> generatedTokens,
        HashSet<string> answeredPromptTokens)
    {
        var shared = 0;
        foreach (var token in generatedTokens)
        {
            if (answeredPromptTokens.Contains(token))
            {
                shared++;
            }
        }

        if (shared < MinSharedTokensForImmediatePromptRepeat)
        {
            return false;
        }

        var union = generatedTokens.Count + answeredPromptTokens.Count - shared;
        var jaccard = union == 0 ? 0 : (double)shared / union;
        var containment = (double)shared / Math.Min(generatedTokens.Count, answeredPromptTokens.Count);

        return jaccard >= ImmediatePromptRepeatJaccardThreshold
            || containment >= ImmediatePromptRepeatContainmentThreshold;
    }

    private static string NormalizeForDuplicateCompare(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(trimmed.Length);
        var previousWasWhitespace = false;
        foreach (var ch in trimmed)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }
            }
            else
            {
                builder.Append(ch);
                previousWasWhitespace = false;
            }
        }

        return builder.ToString();
    }

    private static string CapUserMessage(string? userMessage, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            throw new InvalidOperationException("userMessage is required.");
        }

        var trimmed = userMessage.Trim();
        if (trimmed.Length <= maxChars)
        {
            return trimmed;
        }

        return trimmed[..maxChars];
    }

    private static IReadOnlyList<InterviewPrepPriorTurn> NormalizePriorTurns(
        IReadOnlyList<InterviewPrepPriorTurnDto>? priorTurns,
        int maxPriorTurns,
        int maxPriorTurnChars)
    {
        if (priorTurns is null || priorTurns.Count == 0)
        {
            return [];
        }

        var normalized = new List<InterviewPrepPriorTurn>(priorTurns.Count);

        foreach (var turn in priorTurns)
        {
            if (string.IsNullOrWhiteSpace(turn.Role)
                || (turn.Role != InterviewPrepTurnRoles.User && turn.Role != InterviewPrepTurnRoles.Coach))
            {
                throw new InvalidOperationException("priorTurns.role must be user or coach.");
            }

            if (string.IsNullOrWhiteSpace(turn.Text))
            {
                throw new InvalidOperationException("priorTurns.text is required.");
            }

            var phase = string.IsNullOrWhiteSpace(turn.Phase)
                ? InterviewPrepPhases.Interview
                : turn.Phase;

            if (phase != InterviewPrepPhases.Interview && phase != InterviewPrepPhases.Debrief)
            {
                throw new InvalidOperationException("priorTurns.phase must be interview or debrief.");
            }

            var text = turn.Text.Trim();
            if (text.Length > maxPriorTurnChars)
            {
                text = text[..maxPriorTurnChars];
            }

            normalized.Add(new InterviewPrepPriorTurn(turn.Role, text, phase));
        }

        if (normalized.Count <= maxPriorTurns)
        {
            return normalized;
        }

        // Truncate oldest turns.
        return normalized.Skip(normalized.Count - maxPriorTurns).ToArray();
    }

    private static InterviewPrepTurnResponseDto MapResponse(
        InterviewPrepAiTurnResult result,
        Guid? sessionId)
    {
        InterviewPrepScorecardDto? scorecard = null;
        if (result.Scorecard is { } aiScorecard)
        {
            scorecard = new InterviewPrepScorecardDto(
                aiScorecard.Overall,
                aiScorecard.Summary,
                aiScorecard.Dimensions
                    .Select((dimension) => new InterviewPrepScorecardDimensionDto(
                        dimension.Id,
                        dimension.Score,
                        dimension.Note))
                    .ToArray());
        }

        return new InterviewPrepTurnResponseDto(
            result.Phase,
            new InterviewPrepInferenceDto(
                result.Inference.Role,
                result.Inference.Seniority,
                result.Inference.InterviewStyle,
                result.Inference.IsTechnicalContext),
            result.CoachMessage,
            scorecard,
            result.FollowUps ?? [],
            result.DebriefBullets ?? [],
            result.ModelAnswer,
            sessionId,
            MapTurnState(result.TurnState));
    }

    private static InterviewPrepTurnStateDto? MapTurnState(InterviewPrepAiTurnState? turnState)
    {
        if (turnState is null)
        {
            return null;
        }

        return new InterviewPrepTurnStateDto(
            turnState.InterviewMove,
            turnState.QuestionType,
            turnState.PressureLevel,
            turnState.InterviewerIntent,
            turnState.AgendaStep,
            turnState.NextAgendaStep,
            turnState.MemorySummary,
            turnState.ListeningNotes ?? []);
    }

    private static InterviewPrepSessionSummaryDto MapSessionSummary(InterviewPrepSessionEntity entity) =>
        new(
            entity.Id,
            entity.Mode,
            entity.LanguageMix,
            entity.HiringMarket,
            string.IsNullOrWhiteSpace(entity.InterviewerProfile)
                ? InterviewPrepInterviewerProfiles.HiringManager
                : entity.InterviewerProfile,
            string.IsNullOrWhiteSpace(entity.CurrentAgendaStep) ? "opening" : entity.CurrentAgendaStep,
            entity.LatestInterviewMove,
            entity.ScrapeResultId,
            entity.JobTitle,
            entity.CompanyName,
            entity.Status,
            entity.Phase,
            entity.LatestOverallScore,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.CompletedAt);

    private static InterviewPrepSessionDetailDto MapSessionDetail(InterviewPrepSessionEntity entity) =>
        new(
            entity.Id,
            entity.Mode,
            entity.LanguageMix,
            entity.HiringMarket,
            string.IsNullOrWhiteSpace(entity.InterviewerProfile)
                ? InterviewPrepInterviewerProfiles.HiringManager
                : entity.InterviewerProfile,
            string.IsNullOrWhiteSpace(entity.CurrentAgendaStep) ? "opening" : entity.CurrentAgendaStep,
            entity.LatestInterviewMove,
            entity.ScrapeResultId,
            entity.JobTitle,
            entity.CompanyName,
            entity.Status,
            entity.Phase,
            entity.LatestOverallScore,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.CompletedAt,
            entity.Messages
                .OrderBy((message) => message.Sequence)
                .Select(MapMessage)
                .ToArray());

    private static InterviewPrepSessionMessageDto MapMessage(InterviewPrepSessionMessageEntity message) =>
        new(
            message.Id,
            message.Sequence,
            message.Role,
            message.Text,
            message.Phase,
            DeserializeOptional<InterviewPrepScorecardDto>(message.ScorecardJson),
            DeserializeStringList(message.FollowUpsJson),
            DeserializeStringList(message.DebriefBulletsJson),
            message.ModelAnswer,
            DeserializeOptional<InterviewPrepInferenceDto>(message.InferenceJson),
            DeserializeOptional<InterviewPrepTurnStateDto>(message.TurnStateJson),
            message.CreatedAt);

    private static string? SerializeOptional<T>(T? value) where T : class =>
        value is null ? null : JsonSerializer.Serialize(value, JsonOptions);

    private static T? DeserializeOptional<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private static IReadOnlyList<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
