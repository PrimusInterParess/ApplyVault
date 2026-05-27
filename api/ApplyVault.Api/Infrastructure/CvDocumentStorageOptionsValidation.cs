using ApplyVault.Api.Options;

namespace ApplyVault.Api.Infrastructure;

internal static class CvDocumentStorageOptionsValidation
{
    public static bool Validate(CvDocumentStorageOptions options)
    {
        if (string.Equals(options.Provider, CvDocumentStorageOptions.ProviderAzureBlob, StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(options.AzureBlob.ConnectionString)
                && !string.IsNullOrWhiteSpace(options.AzureBlob.ContainerName);
        }

        return !string.IsNullOrWhiteSpace(options.Local.RootPath);
    }
}
