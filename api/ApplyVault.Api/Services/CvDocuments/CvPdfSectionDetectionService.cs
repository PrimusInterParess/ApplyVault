using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Services;

public interface ICvPdfSectionDetectionService
{
    Task<IReadOnlyList<CvPdfSectionDto>> DetectCurrentDocumentSectionsAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default);
}

public sealed class CvPdfSectionDetectionService(
    ApplyVaultDbContext dbContext,
    ICvDocumentStorage cvDocumentStorage,
    ICvPdfSectionDetector sectionDetector) : ICvPdfSectionDetectionService
{
    public async Task<IReadOnlyList<CvPdfSectionDto>> DetectCurrentDocumentSectionsAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default)
    {
        var document = await dbContext.UserCvDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync((entry) => entry.UserId == user.Id, cancellationToken)
            ?? throw new InvalidOperationException("Upload a CV PDF before detecting sections.");

        var baseStorageKey = document.BaseStorageKey ?? document.StorageKey;

        await using var baseStream = await cvDocumentStorage.OpenReadAsync(baseStorageKey, cancellationToken);
        var sections = sectionDetector.DetectSections(baseStream);

        return sections
            .Select((section) => new CvPdfSectionDto(
                section.HeadingText,
                section.NormalizedKey,
                section.PageIndex,
                section.YPoints))
            .ToArray();
    }
}
