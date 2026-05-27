using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public interface ICvDocumentService
{
    Task<CvDocumentDto?> GetCurrentAsync(AppUserEntity user, CancellationToken cancellationToken = default);

    Task<CvDocumentUploadResultDto> UploadAsync(
        AppUserEntity user,
        IFormFile file,
        CancellationToken cancellationToken = default);

    Task<CvDocumentContent?> OpenContentAsync(AppUserEntity user, CancellationToken cancellationToken = default);

    Task<CvDocumentContent?> OpenOriginalContentAsync(AppUserEntity user, CancellationToken cancellationToken = default);

    Task<CvDocumentContent?> OpenProfilePhotoAsync(AppUserEntity user, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(AppUserEntity user, CancellationToken cancellationToken = default);
}

public sealed record CvDocumentContent(
    Stream Content,
    string ContentType,
    string FileName);

public sealed class CvDocumentService(
    ApplyVaultDbContext dbContext,
    ICvDocumentStorage cvDocumentStorage,
    ICvStructuredImportService cvStructuredImportService,
    IOptions<CvDocumentStorageOptions> storageOptions) : ICvDocumentService
{
    private const string PdfContentType = "application/pdf";

    public async Task<CvDocumentDto?> GetCurrentAsync(AppUserEntity user, CancellationToken cancellationToken = default)
    {
        var document = await dbContext.UserCvDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync((entry) => entry.UserId == user.Id, cancellationToken);

        return document is null ? null : MapDocument(document);
    }

    public async Task<CvDocumentUploadResultDto> UploadAsync(
        AppUserEntity user,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        ValidateUpload(file);

        var existingDocument = await dbContext.UserCvDocuments
            .SingleOrDefaultAsync((entry) => entry.UserId == user.Id, cancellationToken);

        var documentId = existingDocument?.Id ?? Guid.NewGuid();
        var storageKey = BuildStorageKey(user.Id, documentId);
        var now = DateTimeOffset.UtcNow;
        string? previousPhotoKey = existingDocument?.ProfilePhotoStorageKey;

        await using (var uploadStream = file.OpenReadStream())
        {
            await cvDocumentStorage.SaveAsync(storageKey, uploadStream, cancellationToken);
        }

        if (existingDocument is null)
        {
            existingDocument = new UserCvDocumentEntity
            {
                Id = documentId,
                UserId = user.Id,
                OriginalFileName = Path.GetFileName(file.FileName),
                ContentType = PdfContentType,
                StorageKey = storageKey,
                BaseStorageKey = storageKey,
                FileSizeBytes = file.Length,
                OriginalFileSizeBytes = file.Length,
                UploadedAt = now,
                UpdatedAt = now
            };

            dbContext.UserCvDocuments.Add(existingDocument);
        }
        else
        {
            var keysToDelete = new HashSet<string>(StringComparer.Ordinal)
            {
                existingDocument.StorageKey
            };

            if (!string.IsNullOrWhiteSpace(existingDocument.BaseStorageKey))
            {
                keysToDelete.Add(existingDocument.BaseStorageKey);
            }

            var existingSections = await dbContext.UserCvSections
                .Where((section) => section.UserCvDocumentId == existingDocument.Id)
                .ToArrayAsync(cancellationToken);

            dbContext.UserCvSections.RemoveRange(existingSections);
            existingDocument.StructuredImportedAt = null;
            existingDocument.ProfilePhotoStorageKey = null;
            existingDocument.ProfilePhotoContentType = null;

            existingDocument.OriginalFileName = Path.GetFileName(file.FileName);
            existingDocument.ContentType = PdfContentType;
            existingDocument.StorageKey = storageKey;
            existingDocument.BaseStorageKey = storageKey;
            existingDocument.FileSizeBytes = file.Length;
            existingDocument.OriginalFileSizeBytes = file.Length;
            existingDocument.UpdatedAt = now;

            keysToDelete.Remove(storageKey);

            foreach (var keyToDelete in keysToDelete)
            {
                await cvDocumentStorage.DeleteAsync(keyToDelete, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(previousPhotoKey))
            {
                await cvDocumentStorage.DeleteAsync(previousPhotoKey, cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var importSummary = await cvStructuredImportService.ImportAndPersistAsync(user, cancellationToken);

        await dbContext.Entry(existingDocument).ReloadAsync(cancellationToken);

        return new CvDocumentUploadResultDto(MapDocument(existingDocument), importSummary);
    }

    public Task<CvDocumentContent?> OpenContentAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default) =>
        OpenOriginalContentAsync(user, cancellationToken);

    public async Task<CvDocumentContent?> OpenOriginalContentAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default)
    {
        var document = await dbContext.UserCvDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync((entry) => entry.UserId == user.Id, cancellationToken);

        if (document is null)
        {
            return null;
        }

        var storageKey = document.BaseStorageKey ?? document.StorageKey;
        var content = await cvDocumentStorage.OpenReadAsync(storageKey, cancellationToken);

        return new CvDocumentContent(content, document.ContentType, document.OriginalFileName);
    }

    public async Task<CvDocumentContent?> OpenProfilePhotoAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default)
    {
        var document = await dbContext.UserCvDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync((entry) => entry.UserId == user.Id, cancellationToken);

        if (document is null || string.IsNullOrWhiteSpace(document.ProfilePhotoStorageKey))
        {
            return null;
        }

        var content = await cvDocumentStorage.OpenReadAsync(document.ProfilePhotoStorageKey, cancellationToken);
        var contentType = string.IsNullOrWhiteSpace(document.ProfilePhotoContentType)
            ? "image/jpeg"
            : document.ProfilePhotoContentType;
        var extension = string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";

        return new CvDocumentContent(content, contentType, $"profile-photo{extension}");
    }

    public async Task<bool> DeleteAsync(AppUserEntity user, CancellationToken cancellationToken = default)
    {
        var document = await dbContext.UserCvDocuments
            .SingleOrDefaultAsync((entry) => entry.UserId == user.Id, cancellationToken);

        if (document is null)
        {
            return false;
        }

        var keysToDelete = new HashSet<string>(StringComparer.Ordinal)
        {
            document.StorageKey
        };

        if (!string.IsNullOrWhiteSpace(document.BaseStorageKey))
        {
            keysToDelete.Add(document.BaseStorageKey);
        }

        if (!string.IsNullOrWhiteSpace(document.ProfilePhotoStorageKey))
        {
            keysToDelete.Add(document.ProfilePhotoStorageKey);
        }

        foreach (var keyToDelete in keysToDelete)
        {
            await cvDocumentStorage.DeleteAsync(keyToDelete, cancellationToken);
        }

        dbContext.UserCvDocuments.Remove(document);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private void ValidateUpload(IFormFile file)
    {
        if (file.Length <= 0)
        {
            throw new InvalidOperationException("Upload a PDF file before saving.");
        }

        var maxFileSizeBytes = storageOptions.Value.MaxFileSizeBytes;

        if (file.Length > maxFileSizeBytes)
        {
            throw new InvalidOperationException($"CV files must be {maxFileSizeBytes / (1024 * 1024)} MB or smaller.");
        }

        var extension = Path.GetExtension(file.FileName);

        if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only PDF files are supported.");
        }

        if (!string.Equals(file.ContentType, PdfContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only PDF files are supported.");
        }

        using var stream = file.OpenReadStream();
        Span<byte> header = stackalloc byte[4];
        var bytesRead = stream.Read(header);

        if (bytesRead < 4 || header[0] != (byte)'%' || header[1] != (byte)'P' || header[2] != (byte)'D' || header[3] != (byte)'F')
        {
            throw new InvalidOperationException("The uploaded file is not a valid PDF.");
        }
    }

    private static string BuildStorageKey(Guid userId, Guid documentId)
    {
        return $"{userId:D}/{documentId:D}.pdf";
    }

    internal static CvDocumentDto MapDocument(UserCvDocumentEntity document)
    {
        var hasExportedPdf = !string.IsNullOrWhiteSpace(document.BaseStorageKey)
            && !string.Equals(document.StorageKey, document.BaseStorageKey, StringComparison.Ordinal);

        var hasStructuredContent = document.StructuredImportedAt is not null;

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
            document.StructuredImportedAt,
            !string.IsNullOrWhiteSpace(document.ProfilePhotoStorageKey));
    }
}
