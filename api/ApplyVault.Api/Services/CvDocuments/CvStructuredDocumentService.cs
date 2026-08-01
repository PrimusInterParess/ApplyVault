using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services.CvSectionCatalog;
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

public sealed class CvStructuredDocumentService(
    ApplyVaultDbContext dbContext,
    ICvSectionCatalog sectionCatalog) : ICvStructuredDocumentService
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
        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var document = await dbContext.UserCvDocuments
            .SingleOrDefaultAsync((entry) => entry.UserId == user.Id, cancellationToken)
            ?? throw new InvalidOperationException("Create or upload a CV before saving structured content.");

        var existingSections = await dbContext.UserCvSections
            .Where((section) => section.UserCvDocumentId == document.Id)
            .Include((section) => section.Entries)
            .ToArrayAsync(cancellationToken);

        if (existingSections.Length > 0)
        {
            dbContext.UserCvEntries.RemoveRange(existingSections.SelectMany((section) => section.Entries));
            dbContext.UserCvSections.RemoveRange(existingSections);

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var utcNow = DateTimeOffset.UtcNow;

        foreach (var sectionWrite in request.Sections.OrderBy((section) => section.SortOrder))
        {
            var sectionType = sectionCatalog.Normalize(sectionWrite.SectionType);
            var sectionEntity = new UserCvSectionEntity
            {
                Id = sectionWrite.Id ?? Guid.NewGuid(),
                UserId = user.Id,
                UserCvDocumentId = document.Id,
                Heading = sectionWrite.Heading.Trim(),
                SectionType = sectionType,
                SortOrder = sectionWrite.SortOrder
            };

            dbContext.UserCvSections.Add(sectionEntity);

            foreach (var entryWrite in sectionWrite.Entries.OrderBy((entry) => entry.SortOrder))
            {
                var fields = CvEntryFieldsCodec.FromWriteDto(sectionCatalog, sectionType, entryWrite);
                var projected = CvEntryFieldsCodec.ToWriteDto(
                    sectionCatalog,
                    sectionType,
                    fields,
                    entryWrite);

                dbContext.UserCvEntries.Add(new UserCvEntryEntity
                {
                    Id = entryWrite.Id ?? Guid.NewGuid(),
                    UserId = user.Id,
                    SectionId = sectionEntity.Id,
                    Title = ClampRequired(projected.Title, 256),
                    Subtitle = ClampOptional(projected.Subtitle, 512),
                    DateRange = ClampOptional(projected.DateRange, 128),
                    Summary = projected.Summary?.Trim() ?? string.Empty,
                    BulletsJson = CvStructuredJson.SerializeBullets(projected.Bullets),
                    TechStack = ClampRequired(projected.TechStack, 512),
                    FieldsJson = CvEntryFieldsCodec.SerializeFields(fields),
                    Source = ClampRequired(
                        string.IsNullOrWhiteSpace(entryWrite.Source)
                            ? CvEntrySources.Manual
                            : entryWrite.Source,
                        32),
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
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return (await GetStructuredAsync(user, cancellationToken))!;
    }

    internal CvStructuredDocumentDto MapDocument(UserCvDocumentEntity document) =>
        new(
            document.Id,
            document.StructuredImportedAt,
            document.Sections
                .OrderBy((section) => section.SortOrder)
                .Select(MapSection)
                .ToArray());

    private CvStructuredSectionDto MapSection(UserCvSectionEntity section) =>
        new(
            section.Id,
            section.Heading,
            section.SectionType,
            section.SortOrder,
            section.Entries
                .OrderBy((entry) => entry.SortOrder)
                .Select((entry) => MapEntry(section.SectionType, entry))
                .ToArray());

    private CvStructuredEntryDto MapEntry(string sectionType, UserCvEntryEntity entry)
    {
        var fields = CvEntryFieldsCodec.DeserializeFields(entry.FieldsJson);
        var readDto = CvEntryFieldsCodec.ToReadDto(
            sectionCatalog,
            sectionType,
            entry.Id,
            entry.FieldsJson,
            entry.Title,
            entry.Subtitle,
            entry.DateRange,
            entry.Summary,
            entry.BulletsJson,
            entry.TechStack,
            entry.Source,
            entry.SourceSummaryId,
            entry.SortOrder);

        return readDto;
    }

    private static string ClampRequired(string? value, int maxLength)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string? ClampOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
