using ApplyVault.Api.Options;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public sealed class AzureBlobCvDocumentStorage(IOptions<CvDocumentStorageOptions> options) : ICvDocumentStorage
{
    public async Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default)
    {
        var azureOptions = options.Value.AzureBlob;
        var containerClient = new BlobContainerClient(azureOptions.ConnectionString, azureOptions.ContainerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);

        var blobClient = containerClient.GetBlobClient(NormalizeStorageKey(storageKey));
        await blobClient.UploadAsync(content, overwrite: true, cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(storageKey);

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            throw new FileNotFoundException("The CV document file could not be found.", storageKey);
        }

        var response = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return response.Value.Content;
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(storageKey);
        await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
    }

    private BlobClient GetBlobClient(string storageKey)
    {
        var azureOptions = options.Value.AzureBlob;
        var containerClient = new BlobContainerClient(azureOptions.ConnectionString, azureOptions.ContainerName);
        return containerClient.GetBlobClient(NormalizeStorageKey(storageKey));
    }

    private static string NormalizeStorageKey(string storageKey)
    {
        return storageKey.Replace('\\', '/');
    }
}
