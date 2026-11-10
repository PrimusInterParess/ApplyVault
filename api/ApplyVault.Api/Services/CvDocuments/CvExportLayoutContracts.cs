namespace ApplyVault.Api.Services;

public sealed record CvExportDocumentLayout(
    double MarginPoints,
    string PhotoPlacement,
    double PhotoSizePoints,
    double PhotoTopPoints,
    double LineSpacingPoints);

public sealed record CvExportEntry(
    string Title,
    string? Subtitle,
    string? DateRange,
    string Summary,
    IReadOnlyList<string> Bullets,
    string TechStack);

public sealed record CvExportSection(
    string Heading,
    string SectionType,
    int SortOrder,
    IReadOnlyList<CvExportEntry> Entries);

public sealed record CvExportRenderRequest(
    CvExportDocumentLayout Document,
    IReadOnlyList<CvExportSection> Sections,
    byte[]? ProfilePhotoBytes,
    string? ProfilePhotoContentType);

internal static class CvExportLayoutDefaults
{
    public static CvExportDocumentLayout Document(bool hasProfilePhoto = false)
    {
        _ = hasProfilePhoto;
        return new(
            MarginPoints: 44,
            PhotoPlacement: "topRight",
            PhotoSizePoints: 92,
            PhotoTopPoints: 40,
            LineSpacingPoints: 5);
    }
}
