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

public sealed record CvExportPolishEntryInput(
    string Title,
    string? Subtitle,
    string? DateRange,
    string Summary,
    IReadOnlyList<string> Bullets,
    string TechStack);

public sealed record CvExportSectionInput(
    string Heading,
    string SectionType,
    int SortOrder,
    IReadOnlyList<CvExportPolishEntryInput> Entries);
