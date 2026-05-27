using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public interface ICvStructuredExportService
{
    Task<CvDocumentDto> ExportAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default);

    Task<byte[]> PreviewAsync(
        AppUserEntity user,
        SaveCvStructuredDocumentRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CvStructuredExportService(
    ApplyVaultDbContext dbContext,
    ICvDocumentStorage cvDocumentStorage,
    ICvStructuredDocumentService structuredDocumentService,
    IOptions<CvDocumentStorageOptions> storageOptions) : ICvStructuredExportService
{
    public async Task<CvDocumentDto> ExportAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default)
    {
        var structured = await structuredDocumentService.GetStructuredAsync(user, cancellationToken)
            ?? throw new InvalidOperationException("Import or create structured CV content before exporting.");

        if (structured.Sections.Count == 0)
        {
            throw new InvalidOperationException("Add at least one CV section before exporting.");
        }

        var document = await dbContext.UserCvDocuments
            .SingleOrDefaultAsync((entry) => entry.UserId == user.Id, cancellationToken)
            ?? throw new InvalidOperationException("Upload a CV PDF before exporting.");

        var pdfBytes = CvPdfStructuredExportBuilder.Build(structured);
        var maxFileSizeBytes = storageOptions.Value.MaxFileSizeBytes;

        if (pdfBytes.Length > maxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"The exported CV exceeds the {maxFileSizeBytes / (1024 * 1024)} MB limit. Shorten content and try again.");
        }

        if (string.IsNullOrWhiteSpace(document.BaseStorageKey))
        {
            document.BaseStorageKey = document.StorageKey;

            if (document.OriginalFileSizeBytes <= 0)
            {
                document.OriginalFileSizeBytes = document.FileSizeBytes;
            }
        }

        var previousStorageKey = document.StorageKey;
        var exportStorageKey = BuildExportStorageKey(user.Id, document.Id);

        await using (var exportStream = new MemoryStream(pdfBytes))
        {
            await cvDocumentStorage.SaveAsync(exportStorageKey, exportStream, cancellationToken);
        }

        document.StorageKey = exportStorageKey;
        document.FileSizeBytes = pdfBytes.Length;
        document.UpdatedAt = DateTimeOffset.UtcNow;

        if (!string.Equals(previousStorageKey, exportStorageKey, StringComparison.Ordinal)
            && !string.Equals(previousStorageKey, document.BaseStorageKey, StringComparison.Ordinal))
        {
            await cvDocumentStorage.DeleteAsync(previousStorageKey, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return MapDocument(document);
    }

    public async Task<byte[]> PreviewAsync(
        AppUserEntity user,
        SaveCvStructuredDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Sections.Count == 0)
        {
            throw new InvalidOperationException("Add at least one CV section before previewing.");
        }

        var document = await dbContext.UserCvDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync((entry) => entry.UserId == user.Id, cancellationToken)
            ?? throw new InvalidOperationException("Upload a CV PDF before previewing.");

        var structured = CvStructuredDocumentService.MapPreviewRequest(document.Id, request);
        var pdfBytes = CvPdfStructuredExportBuilder.Build(structured);
        var maxFileSizeBytes = storageOptions.Value.MaxFileSizeBytes;

        if (pdfBytes.Length > maxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"The exported CV exceeds the {maxFileSizeBytes / (1024 * 1024)} MB limit. Shorten content and try again.");
        }

        return pdfBytes;
    }

    private static string BuildExportStorageKey(Guid userId, Guid documentId) =>
        $"{userId:D}/{documentId:D}-structured.pdf";

    private static CvDocumentDto MapDocument(UserCvDocumentEntity document)
    {
        var hasExportedPdf = !string.IsNullOrWhiteSpace(document.BaseStorageKey)
            && !string.Equals(document.StorageKey, document.BaseStorageKey, StringComparison.Ordinal);

        var hasStructuredContent = document.StructuredImportedAt is not null
            || document.Sections.Count > 0;

        var originalFileSizeBytes = document.OriginalFileSizeBytes > 0
            ? document.OriginalFileSizeBytes
            : document.FileSizeBytes;

        return new CvDocumentDto(
            document.Id,
            document.OriginalFileName,
            document.ContentType,
            document.FileSizeBytes,
            originalFileSizeBytes,
            document.UploadedAt,
            hasExportedPdf,
            hasStructuredContent,
            document.StructuredImportedAt);
    }
}
