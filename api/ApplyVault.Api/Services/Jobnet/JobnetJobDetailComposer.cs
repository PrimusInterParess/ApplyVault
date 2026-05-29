using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services.Jobnet;

internal interface IJobnetJobDetailComposer
{
    Task<JobnetJobDetailResponse?> ComposeAsync(string id, CancellationToken cancellationToken);
}

internal sealed class JobnetJobDetailComposer(
    JobnetJobDetailResolver resolver,
    IJobDescriptionQualityAssessor qualityAssessor,
    IOptions<JobnetIntegrationOptions> options) : IJobnetJobDetailComposer
{
    public async Task<JobnetJobDetailResponse?> ComposeAsync(string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var raw = await resolver.FetchAsync(id.Trim(), cancellationToken);

        if (raw is null)
        {
            return null;
        }

        if (options.Value.WorkInDenmarkOnly && !raw.Mapped.WorkInDenmark)
        {
            return null;
        }

        var presentation = qualityAssessor.Assess(new JobnetDescriptionAssessmentRequest(
            raw.Mapped.Description,
            raw.Mapped.Title,
            raw.Mapped.Employer,
            raw.Mapped.Id,
            raw.Source));

        return ToResponse(raw, presentation);
    }

    private static JobnetJobDetailResponse ToResponse(
        JobnetRawDetail raw,
        JobnetDescriptionPresentation presentation)
    {
        var mapped = raw.Mapped;

        return new JobnetJobDetailResponse(
            mapped.Id,
            mapped.Title,
            mapped.Employer,
            mapped.Location,
            mapped.PublicationDate,
            mapped.SourceUrl,
            presentation.Description,
            mapped.ApplicationUrl,
            mapped.ContractType,
            mapped.WorkHours,
            mapped.WorkInDenmark,
            JobnetDescriptionQualityValues.ToApiValue(raw.Source),
            JobnetDescriptionQualityValues.ToApiValue(presentation.Quality),
            presentation.Excerpt,
            presentation.QualityReason);
    }
}
