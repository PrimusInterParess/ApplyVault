using ApplyVault.Api.Services.CvSectionCatalog;

namespace ApplyVault.Api.Services;

internal static class CvStructuredImportSectionTypeMapping
{
    private static readonly Lazy<ICvSectionCatalog> Catalog = new(CvSectionCatalogProvider.LoadFromDefaultPath);

    public static string MapSectionType(string normalizedKey) =>
        Catalog.Value.MapHeadingAliasToSectionType(normalizedKey);
}
