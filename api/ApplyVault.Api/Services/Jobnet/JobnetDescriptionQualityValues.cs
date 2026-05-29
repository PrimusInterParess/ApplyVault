namespace ApplyVault.Api.Services.Jobnet;

internal static class JobnetDescriptionQualityValues
{
    public const string SourceNativeDetail = "nativeDetail";
    public const string SourceSearchFallback = "searchFallback";
    public const string QualityFull = "full";
    public const string QualityPreviewOnly = "previewOnly";

    public static string ToApiValue(JobnetDescriptionSource source) =>
        source switch
        {
            JobnetDescriptionSource.NativeDetail => SourceNativeDetail,
            JobnetDescriptionSource.SearchFallback => SourceSearchFallback,
            _ => SourceNativeDetail
        };

    public static string ToApiValue(JobnetDescriptionQuality quality) =>
        quality switch
        {
            JobnetDescriptionQuality.Full => QualityFull,
            JobnetDescriptionQuality.PreviewOnly => QualityPreviewOnly,
            _ => QualityFull
        };
}
