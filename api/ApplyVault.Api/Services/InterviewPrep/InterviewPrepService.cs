using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public interface IInterviewPrepService
{
    Task<InterviewPrepTurnResponseDto> CreateTurnAsync(
        AppUserEntity user,
        InterviewPrepTurnRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class InterviewPrepService(
    ICvStructuredDocumentService structuredDocumentService,
    IScrapeResultStore scrapeResultStore,
    IInterviewPrepAiClient interviewPrepAiClient,
    IOptions<InterviewPrepAiOptions> interviewPrepAiOptions) : IInterviewPrepService
{
    public async Task<InterviewPrepTurnResponseDto> CreateTurnAsync(
        AppUserEntity user,
        InterviewPrepTurnRequest request,
        CancellationToken cancellationToken = default)
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

        var structured = await structuredDocumentService.GetStructuredAsync(user, cancellationToken)
            ?? throw new KeyNotFoundException("Structured CV content was not found.");

        if (structured.Sections.Count == 0)
        {
            throw new InvalidOperationException(
                "Import or create structured CV sections before starting Interview Prep.");
        }

        InterviewPrepJobContext? jobContext = null;
        if (request.ScrapeResultId is { } scrapeResultId)
        {
            var scrape = await scrapeResultStore.GetByIdAsync(scrapeResultId, user.Id, cancellationToken)
                ?? throw new KeyNotFoundException("Scrape result was not found.");

            var details = scrape.Payload.JobDetails;
            jobContext = new InterviewPrepJobContext(
                details.CompanyName,
                details.JobTitle,
                details.Location,
                details.PositionSummary,
                details.JobDescription);
        }

        var aiRequest = new InterviewPrepAiTurnRequest(
            structured,
            jobContext,
            mode,
            languageMix,
            userMessage,
            priorTurns,
            hiringMarket);

        var aiResult = await interviewPrepAiClient.GenerateTurnAsync(aiRequest, cancellationToken);
        return MapResponse(aiResult);
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

    private static InterviewPrepTurnResponseDto MapResponse(InterviewPrepAiTurnResult result)
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
            result.DebriefBullets ?? []);
    }
}
