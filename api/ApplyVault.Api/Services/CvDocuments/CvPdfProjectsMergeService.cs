using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public sealed class CvPdfProjectsMergeService(
    ApplyVaultDbContext dbContext,
    ICvDocumentStorage cvDocumentStorage,
    ICvPdfSectionDetector sectionDetector,
    IOptions<CvDocumentStorageOptions> storageOptions) : ICvPdfProjectsMergeService
{
    public async Task<CvDocumentDto> MergeProjectsAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default)
    {
        var document = await dbContext.UserCvDocuments
            .SingleOrDefaultAsync((entry) => entry.UserId == user.Id, cancellationToken)
            ?? throw new InvalidOperationException("Upload a CV PDF before adding project summaries.");

        var summaries = await dbContext.UserCvProjectSummaries
            .AsNoTracking()
            .Where((summary) => summary.UserId == user.Id && summary.IncludeInMerge)
            .OrderBy((summary) => summary.MergeSortOrder)
            .ThenByDescending((summary) => summary.UpdatedAt)
            .ToArrayAsync(cancellationToken);

        if (summaries.Length == 0)
        {
            throw new InvalidOperationException("Save at least one project summary on the Projects page before merging.");
        }

        var baseStorageKey = document.BaseStorageKey ?? document.StorageKey;

        if (string.IsNullOrWhiteSpace(document.BaseStorageKey))
        {
            document.BaseStorageKey = baseStorageKey;
        }

        await using var baseStream = await cvDocumentStorage.OpenReadAsync(baseStorageKey, cancellationToken);

        byte[] mergedBytes;

        try
        {
            if (baseStream.CanSeek)
            {
                baseStream.Position = 0;
            }

            var detectedSections = sectionDetector.DetectSections(baseStream);

            if (baseStream.CanSeek)
            {
                baseStream.Position = 0;
            }

            var sectionPageIndexes = detectedSections
                .GroupBy((section) => section.HeadingText, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    (group) => group.Key,
                    (group) => group.Min((section) => section.PageIndex),
                    StringComparer.OrdinalIgnoreCase);

            var placements = summaries
                .Select((summary) => new CvPdfMergePlacement(
                    summary.MergeSectionHeading,
                    summary.MergeSortOrder,
                    new CvPdfProjectSummaryEntry(
                        summary.CvTitle,
                        summary.CvSummary,
                        DeserializeStringArray(summary.CvBullets),
                        summary.TechStack)))
                .ToArray();

            mergedBytes = CvPdfProjectsMergeBuilder.Merge(baseStream, placements, sectionPageIndexes);
        }
        catch (PdfSharp.Pdf.IO.PdfReaderException)
        {
            throw new InvalidOperationException("The uploaded CV PDF could not be read. Upload a valid PDF and try again.");
        }

        var maxFileSizeBytes = storageOptions.Value.MaxFileSizeBytes;

        if (mergedBytes.Length > maxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"The merged CV exceeds the {maxFileSizeBytes / (1024 * 1024)} MB limit. Remove summaries or upload a shorter CV.");
        }

        var previousStorageKey = document.StorageKey;
        var mergedStorageKey = BuildMergedStorageKey(user.Id, document.Id);

        await using (var mergedStream = new MemoryStream(mergedBytes))
        {
            await cvDocumentStorage.SaveAsync(mergedStorageKey, mergedStream, cancellationToken);
        }

        document.StorageKey = mergedStorageKey;
        document.FileSizeBytes = mergedBytes.Length;
        document.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        if (!string.Equals(previousStorageKey, baseStorageKey, StringComparison.Ordinal)
            && !string.Equals(previousStorageKey, mergedStorageKey, StringComparison.Ordinal))
        {
            await cvDocumentStorage.DeleteAsync(previousStorageKey, cancellationToken);
        }

        return MapDocument(document);
    }

    private static string BuildMergedStorageKey(Guid userId, Guid documentId)
    {
        return $"{userId:D}/{documentId:D}-merged.pdf";
    }

    private static CvDocumentDto MapDocument(UserCvDocumentEntity document)
    {
        var hasMergedProjects = !string.IsNullOrWhiteSpace(document.BaseStorageKey)
            && !string.Equals(document.StorageKey, document.BaseStorageKey, StringComparison.Ordinal);

        return new CvDocumentDto(
            document.Id,
            document.OriginalFileName,
            document.ContentType,
            document.FileSizeBytes,
            document.UploadedAt,
            hasMergedProjects,
            document.StructuredImportedAt is not null,
            document.StructuredImportedAt);
    }

    private static IReadOnlyList<string> DeserializeStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? [];
    }
}
