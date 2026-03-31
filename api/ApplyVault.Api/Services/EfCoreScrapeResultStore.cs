using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Services;

public sealed class EfCoreScrapeResultStore(ApplyVaultDbContext dbContext) : IScrapeResultStore
{
    public IReadOnlyCollection<SavedScrapeResult> GetAll()
    {
        return dbContext
            .ScrapeResults
            .AsNoTracking()
            .Where((result) => !result.IsDeleted)
            .Include((result) => result.HiringManagerContacts)
            .OrderBy((result) => result.SavedAt)
            .Select(MapToSavedResult)
            .ToArray();
    }

    public SavedScrapeResult? GetById(Guid id)
    {
        var entity = dbContext
            .ScrapeResults
            .AsNoTracking()
            .Include((result) => result.HiringManagerContacts)
            .SingleOrDefault((result) => result.Id == id && !result.IsDeleted);

        return entity is null ? null : MapToSavedResult(entity);
    }

    public SavedScrapeResult Save(ScrapeResultDto result)
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

        dbContext.ScrapeResults.Add(entity);
        dbContext.SaveChanges();

        return MapToSavedResult(entity);
    }

    public SavedScrapeResult? SetRejected(Guid id, bool isRejected)
    {
        var entity = dbContext
            .ScrapeResults
            .Include((result) => result.HiringManagerContacts)
            .SingleOrDefault((result) => result.Id == id && !result.IsDeleted);

        if (entity is null)
        {
            return null;
        }

        entity.IsRejected = isRejected;
        dbContext.SaveChanges();

        return MapToSavedResult(entity);
    }

    public SavedScrapeResult? UpdateDescription(Guid id, string description)
    {
        var entity = dbContext
            .ScrapeResults
            .Include((result) => result.HiringManagerContacts)
            .SingleOrDefault((result) => result.Id == id && !result.IsDeleted);

        if (entity is null)
        {
            return null;
        }

        entity.JobDescription = description;
        dbContext.SaveChanges();

        return MapToSavedResult(entity);
    }

    public bool Delete(Guid id)
    {
        var entity = dbContext
            .ScrapeResults
            .SingleOrDefault((result) => result.Id == id && !result.IsDeleted);

        if (entity is null)
        {
            return false;
        }

        entity.IsDeleted = true;
        dbContext.SaveChanges();

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

        return new SavedScrapeResult(entity.Id, entity.SavedAt, entity.IsRejected, payload);
    }
}
