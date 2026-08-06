using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApplyVault.Api.Data;
using ApplyVault.Api.Models.InterviewPrep;
using ApplyVault.Api.Services.InterviewPrep.Adapters;
using ApplyVault.Api.Services.InterviewPrep.Ai;
using ApplyVault.Api.Services.InterviewPrep.Ai.Contracts;
using ApplyVault.Api.Services.InterviewPrep.Domain;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Services.InterviewPrep;

public interface IInterviewPrepStudyBriefService
{
    Task<InterviewPrepStudyBriefDto> GenerateAsync(
        AppUserEntity user,
        InterviewPrepGenerateStudyBriefRequest request,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepStudyBriefDto> RegenerateAsync(
        AppUserEntity user,
        Guid briefId,
        InterviewPrepRegenerateStudyBriefRequest request,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepStudyBriefListResponseDto> ListAsync(
        AppUserEntity user,
        Guid? scrapeResultId = null,
        bool? cvOnly = null,
        CancellationToken cancellationToken = default);

    Task<InterviewPrepStudyBriefDto> GetAsync(
        AppUserEntity user,
        Guid briefId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        AppUserEntity user,
        Guid briefId,
        CancellationToken cancellationToken = default);
}

public sealed class InterviewPrepStudyBriefService(
    ApplyVaultDbContext dbContext,
    IInterviewPrepCandidateContextAdapter candidateAdapter,
    IInterviewPrepJobContextAdapter jobAdapter,
    IScrapeResultStore scrapeResultStore,
    IInterviewPrepAiGateway aiGateway) : IInterviewPrepStudyBriefService
{
    public const int FocusNoteMaxLength = 2000;

    private const int CvSnapshotMaxChars = 6000;
    private const int JobSnapshotMaxChars = 4000;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<InterviewPrepStudyBriefDto> GenerateAsync(
        AppUserEntity user,
        InterviewPrepGenerateStudyBriefRequest request,
        CancellationToken cancellationToken = default)
    {
        var focusNote = NormalizeFocusNote(request.FocusNote);
        ValidateLanguageMarket(request.Language, request.Market);

        InterviewPrepCandidateSnapshot candidate;
        try
        {
            candidate = await candidateAdapter.CaptureAsync(user, cancellationToken);
        }
        catch (InterviewPrepValidationException)
        {
            throw new InterviewPrepValidationException(
                "Create a Structured CV before generating an interview prep brief.")
            {
                ErrorCode = "interview_prep_brief_cv_required"
            };
        }

        InterviewPrepJobSnapshot? job = null;
        if (request.ScrapeResultId is Guid scrapeResultId)
        {
            try
            {
                job = await jobAdapter.CaptureAsync(user, scrapeResultId, cancellationToken);
            }
            catch (InterviewPrepValidationException)
            {
                throw new InterviewPrepValidationException(
                    "Scrape result was not found for the current user.")
                {
                    ErrorCode = "interview_prep_brief_scrape_not_owned"
                };
            }
        }

        var existing = await FindByBindingAsync(user.Id, request.ScrapeResultId, cancellationToken);
        if (existing is not null)
        {
            throw new InterviewPrepConflictException(
                "An interview prep brief already exists for this binding. Use regenerate.")
            {
                ErrorCode = "interview_prep_brief_exists",
                ExistingBriefId = existing.Id
            };
        }

        var (body, usedAiFallback) = await GenerateBodyViaGatewayAsync(
            candidate,
            job,
            request.Language,
            request.Market,
            focusNote,
            cancellationToken);

        var utcNow = DateTimeOffset.UtcNow;
        var entity = new InterviewPrepStudyBriefEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ScrapeResultId = request.ScrapeResultId,
            Language = InterviewPrepEnumNames.ToWire(request.Language),
            Market = InterviewPrepEnumNames.ToWire(request.Market),
            FocusNoteSnapshot = focusNote,
            BodyJson = JsonSerializer.Serialize(body, SerializerOptions),
            CvFingerprint = ComputeCvFingerprint(candidate),
            CvDocumentId = candidate.CvDocumentId,
            JobTitle = job?.JobTitle,
            CompanyName = job?.CompanyName,
            WasJobBound = job is not null,
            UsedAiFallback = usedAiFallback,
            GeneratedAt = utcNow,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        dbContext.InterviewPrepStudyBriefs.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapDtoAsync(user, entity, cancellationToken);
    }

    public async Task<InterviewPrepStudyBriefDto> RegenerateAsync(
        AppUserEntity user,
        Guid briefId,
        InterviewPrepRegenerateStudyBriefRequest request,
        CancellationToken cancellationToken = default)
    {
        var focusNote = NormalizeFocusNote(request.FocusNote);

        var entity = await LoadOwnedAsync(user.Id, briefId, tracking: true, cancellationToken);

        var language = request.Language
            ?? (InterviewPrepEnumNames.TryParseLanguage(entity.Language, out var storedLanguage)
                ? storedLanguage
                : throw new InterviewPrepValidationException("Stored language is invalid.")
                {
                    ErrorCode = "interview_prep_brief_invalid_language"
                });
        var market = request.Market
            ?? (InterviewPrepEnumNames.TryParseMarket(entity.Market, out var storedMarket)
                ? storedMarket
                : throw new InterviewPrepValidationException("Stored market is invalid.")
                {
                    ErrorCode = "interview_prep_brief_invalid_market"
                });

        if (request.Language is not null)
        {
            ValidateLanguageMarket(request.Language.Value, market);
        }

        if (request.Market is not null)
        {
            ValidateLanguageMarket(language, request.Market.Value);
        }

        InterviewPrepCandidateSnapshot candidate;
        try
        {
            candidate = await candidateAdapter.CaptureAsync(user, cancellationToken);
        }
        catch (InterviewPrepValidationException)
        {
            throw new InterviewPrepValidationException(
                "Create a Structured CV before regenerating an interview prep brief.")
            {
                ErrorCode = "interview_prep_brief_cv_required"
            };
        }

        InterviewPrepJobSnapshot? job = null;
        if (entity.ScrapeResultId is Guid scrapeResultId)
        {
            try
            {
                job = await jobAdapter.CaptureAsync(user, scrapeResultId, cancellationToken);
            }
            catch (InterviewPrepValidationException)
            {
                // Binding retained; regenerate from CV only (U4). Keep prior title/company snapshots.
                job = null;
            }
        }

        var (body, usedAiFallback) = await GenerateBodyViaGatewayAsync(
            candidate,
            job,
            language,
            market,
            focusNote,
            cancellationToken);

        var utcNow = DateTimeOffset.UtcNow;
        entity.Language = InterviewPrepEnumNames.ToWire(language);
        entity.Market = InterviewPrepEnumNames.ToWire(market);
        entity.FocusNoteSnapshot = focusNote;
        entity.BodyJson = JsonSerializer.Serialize(body, SerializerOptions);
        entity.CvFingerprint = ComputeCvFingerprint(candidate);
        entity.CvDocumentId = candidate.CvDocumentId;
        if (job is not null)
        {
            entity.JobTitle = job.JobTitle;
            entity.CompanyName = job.CompanyName;
            entity.WasJobBound = true;
        }
        // If job missing: keep WasJobBound / title / company as previously stored.
        entity.UsedAiFallback = usedAiFallback;
        entity.GeneratedAt = utcNow;
        entity.UpdatedAt = utcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapDtoAsync(user, entity, cancellationToken);
    }

    public async Task<InterviewPrepStudyBriefListResponseDto> ListAsync(
        AppUserEntity user,
        Guid? scrapeResultId = null,
        bool? cvOnly = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<InterviewPrepStudyBriefEntity> query = dbContext.InterviewPrepStudyBriefs
            .AsNoTracking()
            .Where((brief) => brief.UserId == user.Id);

        if (scrapeResultId is Guid filterScrapeId)
        {
            query = query.Where((brief) => brief.ScrapeResultId == filterScrapeId);
        }
        else if (cvOnly == true)
        {
            query = query.Where((brief) => brief.ScrapeResultId == null);
        }

        var entities = await query
            .OrderByDescending((brief) => brief.GeneratedAt)
            .ToListAsync(cancellationToken);

        var items = new List<InterviewPrepStudyBriefDto>(entities.Count);
        foreach (var entity in entities)
        {
            if (!TryDeserializeAndValidateBody(entity.BodyJson, out _))
            {
                // P2 Default A: omit legacy/invalid nested bodies from list.
                continue;
            }

            items.Add(await MapDtoAsync(user, entity, cancellationToken));
        }

        return new InterviewPrepStudyBriefListResponseDto(items);
    }

    public async Task<InterviewPrepStudyBriefDto> GetAsync(
        AppUserEntity user,
        Guid briefId,
        CancellationToken cancellationToken = default)
    {
        var entity = await LoadOwnedAsync(user.Id, briefId, tracking: false, cancellationToken);
        return await MapDtoAsync(user, entity, cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        AppUserEntity user,
        Guid briefId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.InterviewPrepStudyBriefs
            .FirstOrDefaultAsync(
                (brief) => brief.Id == briefId && brief.UserId == user.Id,
                cancellationToken);
        if (entity is null)
        {
            return false;
        }

        dbContext.InterviewPrepStudyBriefs.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<InterviewPrepStudyBriefEntity> LoadOwnedAsync(
        Guid userId,
        Guid briefId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        IQueryable<InterviewPrepStudyBriefEntity> query = tracking
            ? dbContext.InterviewPrepStudyBriefs
            : dbContext.InterviewPrepStudyBriefs.AsNoTracking();

        var entity = await query.FirstOrDefaultAsync(
            (brief) => brief.Id == briefId && brief.UserId == userId,
            cancellationToken);
        return entity ?? throw new InterviewPrepNotFoundException();
    }

    private Task<InterviewPrepStudyBriefEntity?> FindByBindingAsync(
        Guid userId,
        Guid? scrapeResultId,
        CancellationToken cancellationToken)
    {
        if (scrapeResultId is Guid id)
        {
            return dbContext.InterviewPrepStudyBriefs
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    (brief) => brief.UserId == userId && brief.ScrapeResultId == id,
                    cancellationToken);
        }

        return dbContext.InterviewPrepStudyBriefs
            .AsNoTracking()
            .FirstOrDefaultAsync(
                (brief) => brief.UserId == userId && brief.ScrapeResultId == null,
                cancellationToken);
    }

    private async Task<InterviewPrepStudyBriefDto> MapDtoAsync(
        AppUserEntity user,
        InterviewPrepStudyBriefEntity entity,
        CancellationToken cancellationToken)
    {
        var body = DeserializeBody(entity.BodyJson);
        var outdatedReasons = await ComputeOutdatedReasonsAsync(user, entity, cancellationToken);

        return new InterviewPrepStudyBriefDto(
            entity.Id,
            entity.ScrapeResultId,
            entity.JobTitle,
            entity.CompanyName,
            entity.Language,
            entity.Market,
            entity.FocusNoteSnapshot,
            outdatedReasons.Count > 0,
            outdatedReasons,
            entity.GeneratedAt,
            entity.UpdatedAt,
            body.Topics,
            entity.UsedAiFallback);
    }

    private async Task<IReadOnlyList<string>> ComputeOutdatedReasonsAsync(
        AppUserEntity user,
        InterviewPrepStudyBriefEntity entity,
        CancellationToken cancellationToken)
    {
        var reasons = new List<string>(2);

        try
        {
            var candidate = await candidateAdapter.CaptureAsync(user, cancellationToken);
            var currentFingerprint = ComputeCvFingerprint(candidate);
            if (!string.Equals(currentFingerprint, entity.CvFingerprint, StringComparison.Ordinal))
            {
                reasons.Add(InterviewPrepEnumNames.ToWire(InterviewPrepBriefOutdatedReason.StructuredCvChanged));
            }
        }
        catch (InterviewPrepValidationException)
        {
            // Missing CV after generate → treat as CV changed/unavailable for outdated label.
            reasons.Add(InterviewPrepEnumNames.ToWire(InterviewPrepBriefOutdatedReason.StructuredCvChanged));
        }

        if (await IsBoundJobMissingAsync(user, entity, cancellationToken))
        {
            reasons.Add(InterviewPrepEnumNames.ToWire(InterviewPrepBriefOutdatedReason.BoundJobMissing));
        }

        return reasons;
    }

    private async Task<bool> IsBoundJobMissingAsync(
        AppUserEntity user,
        InterviewPrepStudyBriefEntity entity,
        CancellationToken cancellationToken)
    {
        if (entity.ScrapeResultId is Guid scrapeResultId)
        {
            var job = await scrapeResultStore.GetByIdAsync(scrapeResultId, user.Id, cancellationToken);
            return job is null;
        }

        // FK already null: was job-bound if flagged or title/company snapshots remain.
        if (entity.WasJobBound)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(entity.JobTitle)
            || !string.IsNullOrWhiteSpace(entity.CompanyName);
    }

    /// <summary>
    /// Fingerprint = documentId + StructuredImportedAt + catalog version + SHA256 of SnapshotJson.
    /// SnapshotJson must be clock-stable (no CapturedAt) so regenerate + immediate read match.
    /// </summary>
    internal static string ComputeCvFingerprint(InterviewPrepCandidateSnapshot candidate)
    {
        var imported = candidate.StructuredImportedAt?.UtcTicks.ToString() ?? "null";
        var contentKey =
            $"{candidate.CvDocumentId:N}|{imported}|{candidate.CatalogVersion}|{Sha256Hex(candidate.SnapshotJson)}";
        return Sha256Hex(contentKey);
    }

    private static string Sha256Hex(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string? NormalizeFocusNote(string? focusNote)
    {
        if (focusNote is null)
        {
            return null;
        }

        var trimmed = focusNote.Trim();
        if (trimmed.Length == 0)
        {
            throw new InterviewPrepValidationException(
                "focusNote must not be empty when provided.")
            {
                ErrorCode = "interview_prep_brief_invalid_focus_note"
            };
        }

        if (trimmed.Length > FocusNoteMaxLength)
        {
            throw new InterviewPrepValidationException(
                $"focusNote must be at most {FocusNoteMaxLength} characters.")
            {
                ErrorCode = "interview_prep_brief_invalid_focus_note"
            };
        }

        return trimmed;
    }

    private static void ValidateLanguageMarket(InterviewPrepLanguage language, InterviewPrepMarket market)
    {
        if (!Enum.IsDefined(language))
        {
            throw new InterviewPrepValidationException("Unsupported language.")
            {
                ErrorCode = "interview_prep_brief_invalid_language"
            };
        }

        if (!Enum.IsDefined(market))
        {
            throw new InterviewPrepValidationException("Unsupported market.")
            {
                ErrorCode = "interview_prep_brief_invalid_market"
            };
        }
    }

    private static InterviewPrepStudyBriefBodyStorage DeserializeBody(string bodyJson)
    {
        if (!TryDeserializeAndValidateBody(bodyJson, out var body) || body is null)
        {
            throw new InterviewPrepValidationException(
                "Stored brief body is stale or invalid. Regenerate the brief.")
            {
                ErrorCode = "interview_prep_brief_body_stale"
            };
        }

        return body;
    }

    private static bool TryDeserializeAndValidateBody(
        string bodyJson,
        out InterviewPrepStudyBriefBodyStorage? body)
    {
        body = null;
        try
        {
            var parsed = JsonSerializer.Deserialize<InterviewPrepStudyBriefBodyStorage>(
                bodyJson,
                SerializerOptions);
            if (parsed is null)
            {
                return false;
            }

            ValidateBody(parsed, forPersist: false);
            body = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InterviewPrepValidationException)
        {
            return false;
        }
    }

    /// <param name="forPersist">
    /// When true, failures use <c>interview_prep_brief_validation</c> (AI/write path).
    /// When false, callers map failures to <c>interview_prep_brief_body_stale</c> on GET.
    /// </param>
    private static void ValidateBody(InterviewPrepStudyBriefBodyStorage body, bool forPersist)
    {
        var errorCode = forPersist
            ? "interview_prep_brief_validation"
            : "interview_prep_brief_body_stale";

        if (body.Topics is null || body.Topics.Count == 0)
        {
            throw new InterviewPrepValidationException("Brief must include at least one topic.")
            {
                ErrorCode = errorCode
            };
        }

        var seenPriorities = new HashSet<int>();
        foreach (var topic in body.Topics)
        {
            if (string.IsNullOrWhiteSpace(topic.Name))
            {
                throw new InterviewPrepValidationException("Topic name is required.")
                {
                    ErrorCode = errorCode
                };
            }

            if (!InterviewPrepEnumNames.TryParseBriefTopicGap(topic.Gap, out _))
            {
                throw new InterviewPrepValidationException($"Unknown topic gap '{topic.Gap}'.")
                {
                    ErrorCode = errorCode
                };
            }

            if (topic.Priority < 1)
            {
                throw new InterviewPrepValidationException("Topic priority must be >= 1.")
                {
                    ErrorCode = errorCode
                };
            }

            if (!seenPriorities.Add(topic.Priority))
            {
                throw new InterviewPrepValidationException(
                    "Topic priorities must be unique within a brief.")
                {
                    ErrorCode = errorCode
                };
            }

            if (topic.CoverageItems is null || topic.CoverageItems.Count == 0)
            {
                throw new InterviewPrepValidationException(
                    "Each topic must include at least one coverage item.")
                {
                    ErrorCode = errorCode
                };
            }

            if (topic.SampleQuestions is null)
            {
                throw new InterviewPrepValidationException(
                    "Topic sampleQuestions must be an array (may be empty).")
                {
                    ErrorCode = errorCode
                };
            }

            if (topic.TalkingPoints is null)
            {
                throw new InterviewPrepValidationException(
                    "Topic talkingPoints must be an array (may be empty).")
                {
                    ErrorCode = errorCode
                };
            }

            ValidateItems(topic.CoverageItems, "Coverage item", errorCode);
            ValidateItems(topic.SampleQuestions, "Sample question", errorCode);
            ValidateItems(topic.TalkingPoints, "Talking point", errorCode);
        }
    }

    private static void ValidateItems(
        IReadOnlyList<InterviewPrepStudyBriefItemDto> items,
        string label,
        string errorCode)
    {
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Text))
            {
                throw new InterviewPrepValidationException($"{label} text is required.")
                {
                    ErrorCode = errorCode
                };
            }
        }
    }

    /// <summary>
    /// Calls <see cref="IInterviewPrepAiGateway.GenerateInterviewPrepStudyBriefAsync"/>.
    /// No application stub — gateway hard-fail maps to 503 (ai-handoff: safe_fallback null).
    /// Dev uses FakeDeterministicInterviewPrepAiProvider when InterviewPrep:Ai:UseFakeProvider is true.
    /// </summary>
    private async Task<(InterviewPrepStudyBriefBodyStorage Body, bool UsedAiFallback)> GenerateBodyViaGatewayAsync(
        InterviewPrepCandidateSnapshot candidate,
        InterviewPrepJobSnapshot? job,
        InterviewPrepLanguage language,
        InterviewPrepMarket market,
        string? focusNote,
        CancellationToken cancellationToken)
    {
        var aiRequest = new GenerateInterviewPrepStudyBriefRequest(
            Language: InterviewPrepEnumNames.ToWire(language),
            Market: InterviewPrepEnumNames.ToWire(market),
            FocusNote: focusNote,
            CvSnapshot: new InterviewPrepAiDocumentSnapshot(
                Title: "Structured CV snapshot",
                Text: Truncate(candidate.SnapshotJson, CvSnapshotMaxChars)),
            JobSnapshot: job is null
                ? null
                : new InterviewPrepAiDocumentSnapshot(
                    Title: FirstNonEmpty(job.JobTitle, job.CompanyName, "Job snapshot"),
                    Text: Truncate(
                        FirstNonEmpty(job.JobDescription, job.SnapshotJson) ?? string.Empty,
                        JobSnapshotMaxChars)));

        InterviewPrepAiExecutionResult<GenerateInterviewPrepStudyBriefResponse> aiResult;
        try
        {
            aiResult = await aiGateway.GenerateInterviewPrepStudyBriefAsync(aiRequest, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new InterviewPrepAiUnavailableException(
                "Interview Prep brief AI is unavailable.")
            {
                ErrorCode = "interview_prep_brief_ai_unavailable"
            };
        }

        if (!aiResult.Succeeded || aiResult.Value is null)
        {
            var detail = string.IsNullOrWhiteSpace(aiResult.Meta.ErrorMessage)
                ? "Interview Prep brief AI is unavailable."
                : aiResult.Meta.ErrorMessage!;
            throw new InterviewPrepAiUnavailableException(detail)
            {
                ErrorCode = "interview_prep_brief_ai_unavailable"
            };
        }

        var body = MapAiResponse(aiResult.Value);
        ValidateBody(body, forPersist: true);
        // Study-brief gateway has no safe fallback; UsedFallback stays false unless gateway policy changes.
        return (body, aiResult.Meta.UsedFallback);
    }

    private static InterviewPrepStudyBriefBodyStorage MapAiResponse(
        GenerateInterviewPrepStudyBriefResponse response)
    {
        // Expects nested AI topic shape (coverageItems / sampleQuestions / talkingPoints).
        // InterviewPrepAiContracts owned by ai-llm — must match frozen-contracts §7.
        var topics = response.Topics
            .Select((topic) => new InterviewPrepStudyBriefTopicDto(
                topic.Name.Trim(),
                topic.Gap.Trim(),
                topic.Priority,
                string.IsNullOrWhiteSpace(topic.Note) ? null : topic.Note.Trim(),
                MapAiItems(topic.CoverageItems),
                MapAiItems(topic.SampleQuestions),
                MapAiItems(topic.TalkingPoints)))
            .ToArray();

        return new InterviewPrepStudyBriefBodyStorage(topics);
    }

    private static IReadOnlyList<InterviewPrepStudyBriefItemDto> MapAiItems(
        IReadOnlyList<InterviewPrepAiStudyBriefItem>? items)
    {
        if (items is null || items.Count == 0)
        {
            return Array.Empty<InterviewPrepStudyBriefItemDto>();
        }

        return items
            .Select((item) => new InterviewPrepStudyBriefItemDto(
                item.Text.Trim(),
                string.IsNullOrWhiteSpace(item.Note) ? null : item.Note.Trim()))
            .ToArray();
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault((value) => !string.IsNullOrWhiteSpace(value));

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
