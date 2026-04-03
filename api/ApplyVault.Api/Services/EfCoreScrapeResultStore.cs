using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Services;

public sealed class EfCoreScrapeResultStore(ApplyVaultDbContext dbContext) : IScrapeResultStore
{
    private const double LowConfidenceThreshold = 0.7;

    public async Task<IReadOnlyCollection<SavedScrapeResult>> GetAllAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entities = await dbContext
            .ScrapeResults
            .AsNoTracking()
            .Where((result) => !result.IsDeleted && (result.UserId == userId || result.UserId == null))
            .Include((result) => result.HiringManagerContacts)
            .Include((result) => result.InterviewEvent)
            .Include((result) => result.CalendarEventLinks)
            .OrderBy((result) => result.SavedAt)
            .ToArrayAsync(cancellationToken);

        return entities.Select(MapToSavedResult).ToArray();
    }

    public async Task<SavedScrapeResult?> GetByIdAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext
            .ScrapeResults
            .AsNoTracking()
            .Include((result) => result.HiringManagerContacts)
            .Include((result) => result.InterviewEvent)
            .Include((result) => result.CalendarEventLinks)
            .SingleOrDefaultAsync(
                (result) => result.Id == id && !result.IsDeleted && (result.UserId == userId || result.UserId == null),
                cancellationToken);

        return entity is null ? null : MapToSavedResult(entity);
    }

    public async Task<SavedScrapeResult> SaveAsync(
        AssessedScrapeResult result,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var payload = result.Payload;
        var entity = new ScrapeResultEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SavedAt = DateTimeOffset.UtcNow,
            IsRejected = false,
            IsDeleted = false,
            Title = payload.Title,
            Url = payload.Url,
            Text = payload.Text,
            TextLength = payload.TextLength,
            ExtractedAt = payload.ExtractedAt,
            SourceHostname = payload.JobDetails.SourceHostname,
            DetectedPageType = payload.JobDetails.DetectedPageType,
            JobTitle = payload.JobDetails.JobTitle,
            JobTitleConfidence = result.CaptureQuality.JobTitle.Confidence,
            JobTitleReviewReason = result.CaptureQuality.JobTitle.ReviewReason,
            CompanyName = payload.JobDetails.CompanyName,
            CompanyNameConfidence = result.CaptureQuality.CompanyName.Confidence,
            CompanyNameReviewReason = result.CaptureQuality.CompanyName.ReviewReason,
            Location = payload.JobDetails.Location,
            LocationConfidence = result.CaptureQuality.Location.Confidence,
            LocationReviewReason = result.CaptureQuality.Location.ReviewReason,
            JobDescription = payload.JobDetails.JobDescription,
            JobDescriptionConfidence = result.CaptureQuality.JobDescription.Confidence,
            JobDescriptionReviewReason = result.CaptureQuality.JobDescription.ReviewReason,
            PositionSummary = payload.JobDetails.PositionSummary,
            HiringManagerName = payload.JobDetails.HiringManagerName,
            CaptureOverallConfidence = result.CaptureQuality.OverallConfidence,
            CaptureReviewStatus = CaptureReviewStatuses.NotRequired,
            HiringManagerContacts = payload.JobDetails.HiringManagerContacts
                .Select((contact) => new ScrapeResultContactEntity
                {
                    Type = contact.Type,
                    Value = contact.Value,
                    Label = contact.Label
                })
                .ToList()
        };
        UpdateCaptureReviewStatus(entity);

        await dbContext.ScrapeResults.AddAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToSavedResult(entity);
    }

    public async Task<SavedScrapeResult?> UpdateCaptureReviewAsync(
        Guid id,
        Guid userId,
        UpdateScrapeResultCaptureReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext
            .ScrapeResults
            .Include((result) => result.HiringManagerContacts)
            .Include((result) => result.InterviewEvent)
            .Include((result) => result.CalendarEventLinks)
            .SingleOrDefaultAsync(
                (result) => result.Id == id && !result.IsDeleted && (result.UserId == userId || result.UserId == null),
                cancellationToken);

        if (entity is null)
        {
            return null;
        }

        entity.JobTitleOverride = NormalizeOverride(request.JobTitle, entity.JobTitle);
        entity.CompanyNameOverride = NormalizeOverride(request.CompanyName, entity.CompanyName);
        entity.LocationOverride = NormalizeOverride(request.Location, entity.Location);

        if (request.JobDescription is not null)
        {
            entity.JobDescriptionOverride = NormalizeOverride(request.JobDescription, entity.JobDescription);
        }

        UpdateCaptureReviewStatus(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToSavedResult(entity);
    }

    public async Task<SavedScrapeResult?> SetRejectedAsync(
        Guid id,
        Guid userId,
        bool isRejected,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext
            .ScrapeResults
            .Include((result) => result.HiringManagerContacts)
            .Include((result) => result.InterviewEvent)
            .Include((result) => result.CalendarEventLinks)
            .SingleOrDefaultAsync(
                (result) => result.Id == id && !result.IsDeleted && (result.UserId == userId || result.UserId == null),
                cancellationToken);

        if (entity is null)
        {
            return null;
        }

        entity.IsRejected = isRejected;
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToSavedResult(entity);
    }

    public async Task<SavedScrapeResult?> UpdateDescriptionAsync(
        Guid id,
        Guid userId,
        string description,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext
            .ScrapeResults
            .Include((result) => result.HiringManagerContacts)
            .Include((result) => result.InterviewEvent)
            .Include((result) => result.CalendarEventLinks)
            .SingleOrDefaultAsync(
                (result) => result.Id == id && !result.IsDeleted && (result.UserId == userId || result.UserId == null),
                cancellationToken);

        if (entity is null)
        {
            return null;
        }

        entity.JobDescriptionOverride = NormalizeOverride(description, entity.JobDescription);
        UpdateCaptureReviewStatus(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToSavedResult(entity);
    }

    public async Task<SavedScrapeResult?> UpsertInterviewEventAsync(
        Guid id,
        Guid userId,
        UpdateInterviewEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext
            .ScrapeResults
            .Include((result) => result.HiringManagerContacts)
            .Include((result) => result.InterviewEvent)
            .Include((result) => result.CalendarEventLinks)
            .SingleOrDefaultAsync(
                (result) => result.Id == id && !result.IsDeleted && (result.UserId == userId || result.UserId == null),
                cancellationToken);

        if (entity is null)
        {
            return null;
        }

        entity.InterviewDate = DateOnly.FromDateTime(request.StartUtc.UtcDateTime);
        entity.InterviewEvent ??= new InterviewEventEntity
        {
            ScrapeResultId = entity.Id,
            TimeZone = request.TimeZone
        };
        entity.InterviewEvent.StartUtc = request.StartUtc.ToUniversalTime();
        entity.InterviewEvent.EndUtc = request.EndUtc.ToUniversalTime();
        entity.InterviewEvent.TimeZone = request.TimeZone.Trim();
        entity.InterviewEvent.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();
        entity.InterviewEvent.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToSavedResult(entity);
    }

    public async Task<SavedScrapeResult?> ClearInterviewEventAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext
            .ScrapeResults
            .Include((result) => result.HiringManagerContacts)
            .Include((result) => result.InterviewEvent)
            .Include((result) => result.CalendarEventLinks)
            .SingleOrDefaultAsync(
                (result) => result.Id == id && !result.IsDeleted && (result.UserId == userId || result.UserId == null),
                cancellationToken);

        if (entity is null)
        {
            return null;
        }

        entity.InterviewDate = null;

        if (entity.InterviewEvent is not null)
        {
            dbContext.InterviewEvents.Remove(entity.InterviewEvent);
            entity.InterviewEvent = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToSavedResult(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext
            .ScrapeResults
            .SingleOrDefaultAsync((result) => result.Id == id && !result.IsDeleted, cancellationToken);

        if (entity is null || (entity.UserId != userId && entity.UserId is not null))
        {
            return false;
        }

        entity.IsDeleted = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static SavedScrapeResult MapToSavedResult(ScrapeResultEntity entity)
    {
        var effectiveJobTitle = ResolveEffectiveValue(entity.JobTitle, entity.JobTitleOverride);
        var effectiveCompanyName = ResolveEffectiveValue(entity.CompanyName, entity.CompanyNameOverride);
        var effectiveLocation = ResolveEffectiveValue(entity.Location, entity.LocationOverride);
        var effectiveJobDescription = ResolveEffectiveValue(entity.JobDescription, entity.JobDescriptionOverride);
        var payload = new ScrapeResultDto(
            entity.Title,
            entity.Url,
            entity.Text,
            entity.TextLength,
            entity.ExtractedAt,
            new JobDetailsDto(
                entity.SourceHostname,
                entity.DetectedPageType,
                effectiveJobTitle,
                effectiveCompanyName,
                effectiveLocation,
                effectiveJobDescription,
                entity.PositionSummary,
                entity.HiringManagerName,
                entity.HiringManagerContacts
                    .OrderBy((contact) => contact.Id)
                    .Select((contact) => new HiringManagerContactDto(
                        contact.Type,
                        contact.Value,
                        contact.Label))
                    .ToArray()));

        var interviewEvent = entity.InterviewEvent is null
            ? null
            : new InterviewEventDto(
                entity.InterviewEvent.StartUtc,
                entity.InterviewEvent.EndUtc,
                entity.InterviewEvent.TimeZone,
                entity.InterviewEvent.Location,
                entity.InterviewEvent.Notes);

        var calendarEvents = entity.CalendarEventLinks
            .OrderBy((link) => link.CreatedAt)
            .Select((link) => new CalendarEventLinkDto(
                link.Id,
                link.ConnectedAccountId,
                link.Provider,
                link.ExternalEventId,
                link.ExternalEventUrl,
                link.CreatedAt,
                link.UpdatedAt))
            .ToArray();
        var captureQuality = BuildCaptureQuality(entity);

        return new SavedScrapeResult(
            entity.Id,
            entity.SavedAt,
            entity.IsRejected,
            entity.InterviewDate,
            interviewEvent,
            calendarEvents,
            payload,
            captureQuality);
    }

    private static CaptureQualityDto BuildCaptureQuality(ScrapeResultEntity entity)
    {
        return new CaptureQualityDto(
            entity.CaptureReviewStatus,
            string.Equals(entity.CaptureReviewStatus, CaptureReviewStatuses.NeedsReview, StringComparison.Ordinal),
            entity.CaptureOverallConfidence,
            BuildField(
                entity.JobTitle,
                entity.JobTitleOverride,
                entity.JobTitleConfidence,
                entity.JobTitleReviewReason),
            BuildField(
                entity.CompanyName,
                entity.CompanyNameOverride,
                entity.CompanyNameConfidence,
                entity.CompanyNameReviewReason),
            BuildField(
                entity.Location,
                entity.LocationOverride,
                entity.LocationConfidence,
                entity.LocationReviewReason),
            BuildField(
                entity.JobDescription,
                entity.JobDescriptionOverride,
                entity.JobDescriptionConfidence,
                entity.JobDescriptionReviewReason));
    }

    private static CaptureQualityFieldDto BuildField(
        string? originalValue,
        string? userOverrideValue,
        double confidence,
        string? reviewReason)
    {
        var needsReview = IsUnresolvedLowConfidence(confidence, userOverrideValue);
        return new CaptureQualityFieldDto(
            originalValue,
            ResolveEffectiveValue(originalValue, userOverrideValue),
            userOverrideValue,
            confidence,
            needsReview,
            needsReview ? reviewReason : null);
    }

    private static void UpdateCaptureReviewStatus(ScrapeResultEntity entity)
    {
        var hasUnresolvedLowConfidenceField =
            IsUnresolvedLowConfidence(entity.JobTitleConfidence, entity.JobTitleOverride) ||
            IsUnresolvedLowConfidence(entity.CompanyNameConfidence, entity.CompanyNameOverride) ||
            IsUnresolvedLowConfidence(entity.LocationConfidence, entity.LocationOverride) ||
            IsUnresolvedLowConfidence(entity.JobDescriptionConfidence, entity.JobDescriptionOverride);

        if (hasUnresolvedLowConfidenceField)
        {
            entity.CaptureReviewStatus = CaptureReviewStatuses.NeedsReview;
            return;
        }

        entity.CaptureReviewStatus = HasAnyOverride(entity)
            ? CaptureReviewStatuses.Reviewed
            : CaptureReviewStatuses.NotRequired;
    }

    private static bool HasAnyOverride(ScrapeResultEntity entity)
    {
        return entity.JobTitleOverride is not null ||
            entity.CompanyNameOverride is not null ||
            entity.LocationOverride is not null ||
            entity.JobDescriptionOverride is not null;
    }

    private static bool IsUnresolvedLowConfidence(double confidence, string? userOverrideValue)
    {
        return confidence < LowConfidenceThreshold && string.IsNullOrWhiteSpace(userOverrideValue);
    }

    private static string? ResolveEffectiveValue(string? originalValue, string? userOverrideValue)
    {
        return string.IsNullOrWhiteSpace(userOverrideValue) ? originalValue : userOverrideValue;
    }

    private static string? NormalizeOverride(string? newValue, string? originalValue)
    {
        var normalizedOriginal = NormalizeValue(originalValue);
        var normalizedNewValue = NormalizeValue(newValue);

        if (string.Equals(normalizedOriginal, normalizedNewValue, StringComparison.Ordinal))
        {
            return null;
        }

        return normalizedNewValue;
    }

    private static string? NormalizeValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
