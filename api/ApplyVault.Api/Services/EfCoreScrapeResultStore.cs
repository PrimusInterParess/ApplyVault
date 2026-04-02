using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Services;

public sealed class EfCoreScrapeResultStore(ApplyVaultDbContext dbContext) : IScrapeResultStore
{
    public async Task<IReadOnlyCollection<SavedScrapeResult>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await dbContext
            .ScrapeResults
            .AsNoTracking()
            .Where((result) => !result.IsDeleted)
            .Include((result) => result.HiringManagerContacts)
            .OrderBy((result) => result.SavedAt)
            .ToArrayAsync(cancellationToken);

        return entities.Select(MapToSavedResult).ToArray();
    }

    public async Task<SavedScrapeResult?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext
            .ScrapeResults
            .AsNoTracking()
            .Include((result) => result.HiringManagerContacts)
            .SingleOrDefaultAsync((result) => result.Id == id && !result.IsDeleted, cancellationToken);

        return entity is null ? null : MapToSavedResult(entity);
    }

    public async Task<SavedScrapeResult> SaveAsync(
        ScrapeResultDto result,
        CancellationToken cancellationToken = default)
    {
        var entity = new ScrapeResultEntity
        {
            Id = Guid.NewGuid(),
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
        bool isRejected,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext
            .ScrapeResults
            .Include((result) => result.HiringManagerContacts)
            .SingleOrDefaultAsync((result) => result.Id == id && !result.IsDeleted, cancellationToken);

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
        string description,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext
            .ScrapeResults
            .Include((result) => result.HiringManagerContacts)
            .SingleOrDefaultAsync((result) => result.Id == id && !result.IsDeleted, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        entity.JobDescription = description;
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToSavedResult(entity);
    }

    public async Task<SavedScrapeResult?> UpdateInterviewDateAsync(
        Guid id,
        DateOnly? interviewDate,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext
            .ScrapeResults
            .Include((result) => result.HiringManagerContacts)
            .SingleOrDefaultAsync((result) => result.Id == id && !result.IsDeleted, cancellationToken);

        if (entity is null)
        {
            return null;
        }

        entity.InterviewDate = interviewDate;
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapToSavedResult(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext
            .ScrapeResults
            .SingleOrDefaultAsync((result) => result.Id == id && !result.IsDeleted, cancellationToken);

        if (entity is null)
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

        return new SavedScrapeResult(entity.Id, entity.SavedAt, entity.IsRejected, entity.InterviewDate, payload);
    }
}
