using System.Text.Json;
using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.EntityFrameworkCore;
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
    IOptions<InterviewPrepAiOptions> interviewPrepAiOptions) : IInterviewPrepService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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

        var aiResult = await interviewPrepAiClient.GenerateTurnAsync(
            new InterviewPrepAiTurnRequest(
                structured,
                jobContext,
                mode,
                languageMix,
                userMessage,
                priorTurns,
                hiringMarket),
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

        var aiResult = await interviewPrepAiClient.GenerateTurnAsync(
            new InterviewPrepAiTurnRequest(
                structured,
                jobContext,
                session.Mode,
                session.LanguageMix,
                userMessage,
                priorTurns,
                session.HiringMarket),
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
            CreatedAt = now
        };

        dbContext.InterviewPrepSessionMessages.Add(userEntity);
        dbContext.InterviewPrepSessionMessages.Add(coachEntity);

        session.Phase = response.Phase;
        session.InferenceJson = coachEntity.InferenceJson;
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
            sessionId);
    }

    private static InterviewPrepSessionSummaryDto MapSessionSummary(InterviewPrepSessionEntity entity) =>
        new(
            entity.Id,
            entity.Mode,
            entity.LanguageMix,
            entity.HiringMarket,
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
