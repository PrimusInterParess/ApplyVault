using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Services;

public sealed class EfCoreScrapeResultStore(ApplyVaultDbContext dbContext) : IScrapeResultStore
{
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
        ScrapeResultDto result,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var entity = new ScrapeResultEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SavedAt = DateTimeOffset.UtcNow,
            IsRejected = false,
            IsDeleted = false,
            Title = result.Title,
            Url = result.Url,
            Text = result.Text,
            TextLength = result.TextLength,
            ExtractedAt = result.ExtractedAt,
            SourceHostname = result.JobDetails.SourceHostname,
            DetectedPageType = result.JobDetails.DetectedPageType,
            JobTitle = result.JobDetails.JobTitle,
            CompanyName = result.JobDetails.CompanyName,
            Location = result.JobDetails.Location,
            JobDescription = result.JobDetails.JobDescription,
            PositionSummary = result.JobDetails.PositionSummary,
            HiringManagerName = result.JobDetails.HiringManagerName,
            HiringManagerContacts = result.JobDetails.HiringManagerContacts
                .Select((contact) => new ScrapeResultContactEntity
                {
                    Type = contact.Type,
                    Value = contact.Value,
                    Label = contact.Label
                })
                .ToList()
        };

        await dbContext.ScrapeResults.AddAsync(entity, cancellationToken);
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

        entity.JobDescription = description;
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
        var payload = new ScrapeResultDto(
            entity.Title,
            entity.Url,
            entity.Text,
            entity.TextLength,
            entity.ExtractedAt,
            new JobDetailsDto(
                entity.SourceHostname,
                entity.DetectedPageType,
                entity.JobTitle,
                entity.CompanyName,
                entity.Location,
                entity.JobDescription,
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

        return new SavedScrapeResult(
            entity.Id,
            entity.SavedAt,
            entity.IsRejected,
            entity.InterviewDate,
            interviewEvent,
            calendarEvents,
            payload);
    }
}
