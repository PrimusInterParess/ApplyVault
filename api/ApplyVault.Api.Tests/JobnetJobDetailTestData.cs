using ApplyVault.Api.Models;
using ApplyVault.Api.Services.Jobnet;

namespace ApplyVault.Api.Tests;

internal static class JobnetJobDetailTestData
{
    public static JobnetJobDetailResponse ToDetailResponse(
        JobnetMappedJobDetail mapped,
        JobnetDescriptionSource source = JobnetDescriptionSource.NativeDetail,
        JobnetDescriptionQuality quality = JobnetDescriptionQuality.Full,
        string? excerpt = null,
        string? qualityReason = null)
    {
        return new JobnetJobDetailResponse(
            mapped.Id,
            mapped.Title,
            mapped.Employer,
            mapped.Location,
            mapped.PublicationDate,
            mapped.SourceUrl,
            quality == JobnetDescriptionQuality.Full ? mapped.Description : null,
            mapped.ApplicationUrl,
            mapped.ContractType,
            mapped.WorkHours,
            mapped.WorkInDenmark,
            JobnetDescriptionQualityValues.ToApiValue(source),
            JobnetDescriptionQualityValues.ToApiValue(quality),
            excerpt,
            qualityReason);
    }

    public const string ScrapedExternalDescription =
        "Developer — Responsive\n\n\n\n\n\n\n\n\n\n\n\n\n\n\nResponsive\n\n\nPrinciples\n\nInspiration\n\nServices\n\nAbout us\n\nReferences";
}
