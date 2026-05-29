namespace ApplyVault.Api.Services.Jobnet;

internal sealed class JobnetDescriptionQualityAssessor : IJobDescriptionQualityAssessor
{
    private const string PreviewReason =
        "This listing is imported from an external site and the preview may include unrelated page content.";

    public JobnetDescriptionPresentation Assess(JobnetDescriptionAssessmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return new JobnetDescriptionPresentation(
                JobnetDescriptionQuality.PreviewOnly,
                Description: null,
                Excerpt: null,
                QualityReason: "No description was returned for this listing.");
        }

        if (!JobDescriptionHeuristicRules.ShouldUsePreviewOnly(request))
        {
            return new JobnetDescriptionPresentation(
                JobnetDescriptionQuality.Full,
                Description: request.Description,
                Excerpt: null,
                QualityReason: null);
        }

        return new JobnetDescriptionPresentation(
            JobnetDescriptionQuality.PreviewOnly,
            Description: null,
            Excerpt: JobDescriptionExcerptBuilder.Build(
                request.Description,
                request.Title,
                request.Employer),
            QualityReason: PreviewReason);
    }
}
