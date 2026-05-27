namespace ApplyVault.Api.Services;

public sealed record CvPdfProjectSummaryEntry(
    string CvTitle,
    string CvSummary,
    IReadOnlyList<string> CvBullets,
    string TechStack);
