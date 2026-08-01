using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public interface ICvStructuredImportService
{
    Task<CvStructuredImportPreviewDto> PreviewImportAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default);

    Task<CvStructuredImportSummaryDto> ImportAndPersistAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default);

    Task<CvStructuredReimportResultDto> ReimportAndPersistAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default);
}

public sealed class CvStructuredImportService(
    ApplyVaultDbContext dbContext,
    ICvDocumentStorage cvDocumentStorage,
    ICvPdfFullTextExtractor fullTextExtractor,
    ICvPdfProfilePhotoExtractor profilePhotoExtractor,
    ICvStructuredImportAiClient importAiClient,
    ICvStructuredDocumentService structuredDocumentService,
    IOptions<GoogleAiOptions> googleAiOptions,
    IOptions<CvImportAiOptions> importAiOptions) : ICvStructuredImportService
{
    public async Task<CvStructuredImportPreviewDto> PreviewImportAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default)
    {
        var pdfBytes = await ReadCurrentPdfBytesAsync(user, cancellationToken);
        return await BuildPreviewAsync(pdfBytes, cancellationToken);
    }

    public async Task<CvStructuredImportSummaryDto> ImportAndPersistAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pdfBytes = await ReadCurrentPdfBytesAsync(user, cancellationToken);
            var preview = await BuildPreviewAsync(pdfBytes, cancellationToken);

            if (preview.Sections.Count == 0)
            {
                return new CvStructuredImportSummaryDto(
                    false,
                    0,
                    preview.UsedAi,
                    false,
                    "No structured sections could be generated from the uploaded CV PDF.");
            }

            var profilePhotoExtracted = await PersistProfilePhotoAsync(user, pdfBytes, cancellationToken);

            await structuredDocumentService.SaveStructuredAsync(
                user,
                new SaveCvStructuredDocumentRequest(preview.Sections),
                markImported: true,
                cancellationToken);

            return new CvStructuredImportSummaryDto(
                true,
                preview.Sections.Count,
                preview.UsedAi,
                profilePhotoExtracted,
                preview.Notice);
        }
        catch (Exception exception)
        {
            return new CvStructuredImportSummaryDto(
                false,
                0,
                false,
                false,
                exception.Message);
        }
    }

    public async Task<CvStructuredReimportResultDto> ReimportAndPersistAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var preview = await PreviewImportAsync(user, cancellationToken);

            if (preview.Sections.Count == 0)
            {
                return new CvStructuredReimportResultDto(
                    null,
                    new CvStructuredImportSummaryDto(
                        false,
                        0,
                        preview.UsedAi,
                        false,
                        "No structured sections could be generated from the uploaded CV PDF."));
            }

            await structuredDocumentService.SaveStructuredAsync(
                user,
                new SaveCvStructuredDocumentRequest(preview.Sections),
                markImported: true,
                cancellationToken);

            var structured = await structuredDocumentService.GetStructuredAsync(user, cancellationToken)
                ?? throw new InvalidOperationException("Structured CV content could not be loaded after re-import.");

            return new CvStructuredReimportResultDto(
                structured,
                new CvStructuredImportSummaryDto(
                    true,
                    preview.Sections.Count,
                    preview.UsedAi,
                    false,
                    preview.Notice));
        }
        catch (Exception exception)
        {
            return new CvStructuredReimportResultDto(
                null,
                new CvStructuredImportSummaryDto(
                    false,
                    0,
                    false,
                    false,
                    exception.Message));
        }
    }

    private async Task<byte[]> ReadCurrentPdfBytesAsync(AppUserEntity user, CancellationToken cancellationToken)
    {
        var document = await dbContext.UserCvDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync((entry) => entry.UserId == user.Id, cancellationToken)
            ?? throw new InvalidOperationException("Upload a CV PDF before importing content.");

        var baseStorageKey = document.BaseStorageKey ?? document.StorageKey;

        await using var baseStream = await cvDocumentStorage.OpenReadAsync(baseStorageKey, cancellationToken);

        using var memoryStream = new MemoryStream();
        await baseStream.CopyToAsync(memoryStream, cancellationToken);
        return memoryStream.ToArray();
    }

    private Task<CvStructuredImportPreviewDto> BuildPreviewAsync(
        byte[] pdfBytes,
        CancellationToken cancellationToken) =>
        CvPdfImportPipeline.BuildPreviewAsync(
            pdfBytes,
            fullTextExtractor,
            importAiClient,
            googleAiOptions.Value.Enabled,
            importAiOptions.Value,
            cancellationToken);

    private async Task<bool> PersistProfilePhotoAsync(
        AppUserEntity user,
        byte[] pdfBytes,
        CancellationToken cancellationToken)
    {
        var document = await dbContext.UserCvDocuments
            .SingleOrDefaultAsync((entry) => entry.UserId == user.Id, cancellationToken);

        if (document is null)
        {
            return false;
        }

        using var pdfStream = new MemoryStream(pdfBytes);
        var photoResult = profilePhotoExtractor.TryExtractProfilePhoto(pdfStream);
        var previousPhotoKey = document.ProfilePhotoStorageKey;

        if (photoResult is null)
        {
            document.ProfilePhotoStorageKey = null;
            document.ProfilePhotoContentType = null;
            document.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(previousPhotoKey))
            {
                await cvDocumentStorage.DeleteAsync(previousPhotoKey, cancellationToken);
            }

            return false;
        }

        var extension = string.Equals(photoResult.ContentType, "image/png", StringComparison.OrdinalIgnoreCase)
            ? ".png"
            : ".jpg";
        var photoStorageKey = BuildPhotoStorageKey(user.Id, document.Id, extension);

        await using (var photoStream = new MemoryStream(photoResult.ImageBytes))
        {
            await cvDocumentStorage.SaveAsync(photoStorageKey, photoStream, cancellationToken);
        }

        document.ProfilePhotoStorageKey = photoStorageKey;
        document.ProfilePhotoContentType = photoResult.ContentType;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(previousPhotoKey)
            && !string.Equals(previousPhotoKey, photoStorageKey, StringComparison.Ordinal))
        {
            await cvDocumentStorage.DeleteAsync(previousPhotoKey, cancellationToken);
        }

        return true;
    }

    private static string BuildPhotoStorageKey(Guid userId, Guid documentId, string extension) =>
        $"{userId:D}/{documentId:D}-photo{extension}";
}
