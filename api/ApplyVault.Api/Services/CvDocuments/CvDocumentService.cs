using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public interface ICvDocumentService
{
    Task<CvDocumentDto?> GetCurrentAsync(AppUserEntity user, CancellationToken cancellationToken = default);

    Task<CvDocumentDto> UploadAsync(
        AppUserEntity user,
        IFormFile file,
        CancellationToken cancellationToken = default);

    Task<CvDocumentContent?> OpenContentAsync(AppUserEntity user, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(AppUserEntity user, CancellationToken cancellationToken = default);
}

public sealed record CvDocumentContent(
    Stream Content,
    string ContentType,
    string FileName);

public sealed class CvDocumentService(
    ApplyVaultDbContext dbContext,
    ICvDocumentStorage cvDocumentStorage,
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

    public async Task<CvDocumentDto> UploadAsync(
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

            existingDocument.OriginalFileName = Path.GetFileName(file.FileName);
            existingDocument.ContentType = PdfContentType;
            existingDocument.StorageKey = storageKey;
            existingDocument.BaseStorageKey = storageKey;
            existingDocument.FileSizeBytes = file.Length;
            existingDocument.UpdatedAt = now;

            keysToDelete.Remove(storageKey);

            foreach (var keyToDelete in keysToDelete)
            {
                await cvDocumentStorage.DeleteAsync(keyToDelete, cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return MapDocument(existingDocument);
    }

    public async Task<CvDocumentContent?> OpenContentAsync(AppUserEntity user, CancellationToken cancellationToken = default)
    {
        var document = await dbContext.UserCvDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync((entry) => entry.UserId == user.Id, cancellationToken);

        if (document is null)
        {
            return null;
        }

        var content = await cvDocumentStorage.OpenReadAsync(document.StorageKey, cancellationToken);

        return new CvDocumentContent(content, document.ContentType, document.OriginalFileName);
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
            hasMergedProjects);
    }
}
