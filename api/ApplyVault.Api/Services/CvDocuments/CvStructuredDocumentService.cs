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

    Task<CvStructuredEntryDto> InsertEntryFromSummaryAsync(
        AppUserEntity user,
        Guid sectionId,
        InsertCvEntryFromSummaryRequest request,
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

        await dbContext.UserCvEntries
            .Where((entry) =>
                dbContext.UserCvSections
                    .Where((section) => section.UserCvDocumentId == document.Id)
                    .Select((section) => section.Id)
                    .Contains(entry.SectionId))
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.UserCvSections
            .Where((section) => section.UserCvDocumentId == document.Id)
            .ExecuteDeleteAsync(cancellationToken);

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

    public async Task<CvStructuredEntryDto> InsertEntryFromSummaryAsync(
        AppUserEntity user,
        Guid sectionId,
        InsertCvEntryFromSummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        var section = await dbContext.UserCvSections
            .Include((entry) => entry.Entries)
            .SingleOrDefaultAsync(
                (entry) => entry.Id == sectionId && entry.UserId == user.Id,
                cancellationToken)
            ?? throw new InvalidOperationException("CV section not found.");

        var summary = await dbContext.UserCvProjectSummaries
            .AsNoTracking()
            .SingleOrDefaultAsync(
                (entry) => entry.Id == request.SummaryId && entry.UserId == user.Id,
                cancellationToken)
            ?? throw new InvalidOperationException("Project summary not found.");

        var nextSortOrder = section.Entries.Count == 0
            ? 0
            : section.Entries.Max((entry) => entry.SortOrder) + 1;

        var entry = new UserCvEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SectionId = section.Id,
            Title = summary.CvTitle,
            Subtitle = summary.FullName,
            DateRange = null,
            Summary = summary.CvSummary,
            BulletsJson = summary.CvBullets,
            TechStack = summary.TechStack,
            Source = CvEntrySources.GitHubSummary,
            SourceSummaryId = summary.Id,
            SortOrder = nextSortOrder
        };

        dbContext.UserCvEntries.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);

        return MapEntry(entry);
    }

    internal static CvStructuredDocumentDto MapDocument(UserCvDocumentEntity document) =>
        new(
            document.Id,
            document.StructuredImportedAt,
            document.Sections
                .OrderBy((section) => section.SortOrder)
                .Select(MapSection)
                .ToArray());

    internal static CvStructuredDocumentDto MapPreviewRequest(
        Guid documentId,
        SaveCvStructuredDocumentRequest request) =>
        new(
            documentId,
            null,
            request.Sections
                .OrderBy((section) => section.SortOrder)
                .Select(MapPreviewSection)
                .ToArray());

    private static CvStructuredSectionDto MapPreviewSection(CvStructuredSectionWriteDto section) =>
        new(
            section.Id ?? Guid.NewGuid(),
            section.Heading.Trim(),
            CvSectionTypes.Normalize(section.SectionType),
            section.SortOrder,
            section.Entries
                .OrderBy((entry) => entry.SortOrder)
                .Select(MapPreviewEntry)
                .ToArray());

    private static CvStructuredEntryDto MapPreviewEntry(CvStructuredEntryWriteDto entry) =>
        new(
            entry.Id ?? Guid.NewGuid(),
            entry.Title.Trim(),
            string.IsNullOrWhiteSpace(entry.Subtitle) ? null : entry.Subtitle.Trim(),
            string.IsNullOrWhiteSpace(entry.DateRange) ? null : entry.DateRange.Trim(),
            entry.Summary?.Trim() ?? string.Empty,
            entry.Bullets ?? Array.Empty<string>(),
            entry.TechStack?.Trim() ?? string.Empty,
            string.IsNullOrWhiteSpace(entry.Source) ? CvEntrySources.Manual : entry.Source,
            entry.SourceSummaryId,
            entry.SortOrder);

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
