namespace ApplyVault.Api.Options;

public sealed class CvDocumentStorageOptions
{
    public const string SectionName = "CvDocumentStorage";

    public const string ProviderLocal = "Local";
    public const string ProviderAzureBlob = "AzureBlob";

    public string Provider { get; set; } = ProviderLocal;

    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;

    public LocalCvDocumentStorageOptions Local { get; set; } = new();

    public AzureBlobCvDocumentStorageOptions AzureBlob { get; set; } = new();
}

public sealed class LocalCvDocumentStorageOptions
{
    public string RootPath { get; set; } = "App_Data/cv-documents";
}

public sealed class AzureBlobCvDocumentStorageOptions
{
    public string ConnectionString { get; set; } = string.Empty;

    public string ContainerName { get; set; } = "cv-documents";
}
