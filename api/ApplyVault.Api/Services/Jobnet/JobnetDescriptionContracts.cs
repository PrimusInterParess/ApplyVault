namespace ApplyVault.Api.Services.Jobnet;

internal enum JobnetDescriptionSource
{
    NativeDetail,
    SearchFallback
}

internal enum JobnetDescriptionQuality
{
    Full,
    PreviewOnly
}

internal sealed record JobnetMappedJobDetail(
    string Id,
    string? Title,
    string? Employer,
    string? Location,
    string? PublicationDate,
    string? SourceUrl,
    string? Description,
    string? ApplicationUrl,
    string? ContractType,
    string? WorkHours,
    bool WorkInDenmark);

internal sealed record JobnetRawDetail(
    JobnetDescriptionSource Source,
    JobnetMappedJobDetail Mapped);

internal sealed record JobnetDescriptionAssessmentRequest(
    string? Description,
    string? Title,
    string? Employer,
    string Id,
    JobnetDescriptionSource Source);

internal sealed record JobnetDescriptionPresentation(
    JobnetDescriptionQuality Quality,
    string? Description,
    string? Excerpt,
    string? QualityReason);

internal interface IJobDescriptionQualityAssessor
{
    JobnetDescriptionPresentation Assess(JobnetDescriptionAssessmentRequest request);
}
