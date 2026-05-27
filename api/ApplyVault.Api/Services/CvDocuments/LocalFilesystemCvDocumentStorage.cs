using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public sealed class LocalFilesystemCvDocumentStorage(IOptions<CvDocumentStorageOptions> options) : ICvDocumentStorage
{
    public Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default)
    {
        var absolutePath = ResolveAbsolutePath(storageKey);
        var directoryPath = Path.GetDirectoryName(absolutePath);

        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        return SaveToPathAsync(absolutePath, content, cancellationToken);
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var absolutePath = ResolveAbsolutePath(storageKey);

        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException("The CV document file could not be found.", absolutePath);
        }

        Stream stream = File.OpenRead(absolutePath);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var absolutePath = ResolveAbsolutePath(storageKey);

        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        return Task.CompletedTask;
    }

    private string ResolveAbsolutePath(string storageKey)
    {
        var rootPath = options.Value.Local.RootPath;

        if (Path.IsPathRooted(rootPath))
        {
            return Path.GetFullPath(Path.Combine(rootPath, storageKey));
        }

        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), rootPath, storageKey));
    }

    private static async Task SaveToPathAsync(string absolutePath, Stream content, CancellationToken cancellationToken)
    {
        await using var destination = File.Create(absolutePath);
        await content.CopyToAsync(destination, cancellationToken);
    }
}
