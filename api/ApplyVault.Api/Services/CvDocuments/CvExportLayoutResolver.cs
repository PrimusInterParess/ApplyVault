namespace ApplyVault.Api.Services;

internal sealed record CvExportLayoutDocumentStyle(string? Margin, string? PhotoPlacement, string? PhotoSize);

internal static class CvExportLayoutResolver
{
    public static CvExportDocumentLayout ResolveDocument(CvExportLayoutDocumentStyle? layout, bool hasProfilePhoto) =>
        new(
            MapMargin(layout?.Margin),
            hasProfilePhoto ? "topRight" : MapPhotoPlacement(layout?.PhotoPlacement),
            MapPhotoSize(layout?.PhotoSize),
            PhotoTopPoints: 40,
            LineSpacingPoints: 5);

    public static CvExportSectionLayout ResolveSection(string sectionType, bool isFirstSection) =>
        new(
            sectionType == CvSectionTypes.Summary ? 15 : 13,
            true,
            true,
            isFirstSection ? 0 : 18,
            14);

    private static double MapMargin(string? value) =>
        Normalize(value) switch
        {
            "narrow" => 36,
            "wide" => 50,
            _ => 44
        };

    private static string MapPhotoPlacement(string? value) =>
        Normalize(value) switch
        {
            "topleft" => "topLeft",
            _ => "topRight"
        };

    private static double MapPhotoSize(string? value) =>
        Normalize(value) switch
        {
            "small" => 76,
            "large" => 100,
            _ => 92
        };

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();
}

internal sealed record CvExportSectionLayout(
    double HeadingFontSize,
    bool HeadingBold,
    bool DrawHeadingRule,
    double SpaceBefore,
    double SpaceAfter);

internal static class CvExportLayoutDefaults
{
    public static CvExportDocumentLayout Document(bool hasProfilePhoto = false) =>
        CvExportLayoutResolver.ResolveDocument(null, hasProfilePhoto);
}
