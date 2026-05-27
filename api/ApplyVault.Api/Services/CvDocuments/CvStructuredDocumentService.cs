using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Services;

public interface ICvStructuredDocumentService
{
    Task<CvStructuredDocumentDto?> GetStructuredAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default);

    Task<CvStructuredDocumentDto> SaveStructuredAsync(
        AppUserEntity user,
        SaveCvStructuredDocumentRequest request,
        bool markImported,
        CancellationToken cancellationToken = default);
}

public sealed class CvStructuredDocumentService(ApplyVaultDbContext dbContext) : ICvStructuredDocumentService
{
    public async Task<CvStructuredDocumentDto?> GetStructuredAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default)
    {
        var document = await dbContext.UserCvDocuments
            .AsNoTracking()
            .Include((entry) => entry.Sections.OrderBy((section) => section.SortOrder))
            .ThenInclude((section) => section.Entries.OrderBy((entry) => entry.SortOrder))
            .SingleOrDefaultAsync((entry) => entry.UserId == user.Id, cancellationToken);

        return document is null ? null : MapDocument(document);
    }

    public async Task<CvStructuredDocumentDto> SaveStructuredAsync(
        AppUserEntity user,
        SaveCvStructuredDocumentRequest request,
        bool markImported,
        CancellationToken cancellationToken = default)
    {
        var document = await dbContext.UserCvDocuments
            .SingleOrDefaultAsync((entry) => entry.UserId == user.Id, cancellationToken)
            ?? throw new InvalidOperationException("Upload a CV PDF before saving structured content.");

        var existingSections = await dbContext.UserCvSections
            .Where((section) => section.UserCvDocumentId == document.Id)
            .Include((section) => section.Entries)
            .ToArrayAsync(cancellationToken);

        if (existingSections.Length > 0)
        {
            dbContext.UserCvEntries.RemoveRange(existingSections.SelectMany((section) => section.Entries));
            dbContext.UserCvSections.RemoveRange(existingSections);
        }

        var utcNow = DateTimeOffset.UtcNow;

        foreach (var sectionWrite in request.Sections.OrderBy((section) => section.SortOrder))
        {
            var sectionEntity = new UserCvSectionEntity
            {
                Id = sectionWrite.Id ?? Guid.NewGuid(),
                UserId = user.Id,
                UserCvDocumentId = document.Id,
                Heading = sectionWrite.Heading.Trim(),
                SectionType = CvSectionTypes.Normalize(sectionWrite.SectionType),
                SortOrder = sectionWrite.SortOrder
            };

            dbContext.UserCvSections.Add(sectionEntity);

            foreach (var entryWrite in sectionWrite.Entries.OrderBy((entry) => entry.SortOrder))
            {
                dbContext.UserCvEntries.Add(new UserCvEntryEntity
                {
                    Id = entryWrite.Id ?? Guid.NewGuid(),
                    UserId = user.Id,
                    SectionId = sectionEntity.Id,
                    Title = entryWrite.Title.Trim(),
                    Subtitle = string.IsNullOrWhiteSpace(entryWrite.Subtitle) ? null : entryWrite.Subtitle.Trim(),
                    DateRange = string.IsNullOrWhiteSpace(entryWrite.DateRange) ? null : entryWrite.DateRange.Trim(),
                    Summary = entryWrite.Summary?.Trim() ?? string.Empty,
                    BulletsJson = CvStructuredJson.SerializeBullets(entryWrite.Bullets),
                    TechStack = entryWrite.TechStack?.Trim() ?? string.Empty,
                    Source = string.IsNullOrWhiteSpace(entryWrite.Source)
                        ? CvEntrySources.Manual
                        : entryWrite.Source,
                    SourceSummaryId = entryWrite.SourceSummaryId,
                    SortOrder = entryWrite.SortOrder
                });
            }
        }

        document.StructuredImportedAt = request.Sections.Count > 0
            ? markImported ? utcNow : document.StructuredImportedAt ?? utcNow
            : null;

        document.UpdatedAt = utcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return (await GetStructuredAsync(user, cancellationToken))!;
    }

    internal static CvStructuredDocumentDto MapDocument(UserCvDocumentEntity document) =>
        new(
            document.Id,
            document.StructuredImportedAt,
            document.Sections
                .OrderBy((section) => section.SortOrder)
                .Select(MapSection)
                .ToArray());

    private static CvStructuredSectionDto MapSection(UserCvSectionEntity section) =>
        new(
            section.Id,
            section.Heading,
            section.SectionType,
            section.SortOrder,
            section.Entries
                .OrderBy((entry) => entry.SortOrder)
                .Select(MapEntry)
                .ToArray());

    private static CvStructuredEntryDto MapEntry(UserCvEntryEntity entry) =>
        new(
            entry.Id,
            entry.Title,
            entry.Subtitle,
            entry.DateRange,
            entry.Summary,
            CvStructuredJson.DeserializeBullets(entry.BulletsJson),
            entry.TechStack,
            entry.Source,
            entry.SourceSummaryId,
            entry.SortOrder);
}
