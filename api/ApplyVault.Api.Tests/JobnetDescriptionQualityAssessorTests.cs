using ApplyVault.Api.Services.Jobnet;

namespace ApplyVault.Api.Tests;

public sealed class JobnetDescriptionQualityAssessorTests
{
    private readonly JobnetDescriptionQualityAssessor _assessor = new();

    [Fact]
    public void Assess_ReturnsFull_ForNativeStructuredHtml()
    {
        var html = "<p><strong>We are hiring</strong></p><p>Build APIs with C# across distributed systems and ship production features every week.</p>";

        var result = _assessor.Assess(new JobnetDescriptionAssessmentRequest(
            html,
            "Backend Developer",
            "Contoso A/S",
            "b2b58b21-1353-47c7-afdb-5bb1ff15fd5a",
            JobnetDescriptionSource.NativeDetail));

        Assert.Equal(JobnetDescriptionQuality.Full, result.Quality);
        Assert.Equal(html, result.Description);
        Assert.Null(result.Excerpt);
        Assert.Null(result.QualityReason);
    }

    [Fact]
    public void Assess_ReturnsPreviewOnly_ForScrapedExternalListing()
    {
        var result = _assessor.Assess(new JobnetDescriptionAssessmentRequest(
            JobnetJobDetailTestData.ScrapedExternalDescription,
            "Developer — Responsive",
            "RESPONSIVE A/S",
            "E10990623",
            JobnetDescriptionSource.SearchFallback));

        Assert.Equal(JobnetDescriptionQuality.PreviewOnly, result.Quality);
        Assert.Null(result.Description);
        Assert.False(string.IsNullOrWhiteSpace(result.Excerpt));
        Assert.False(string.IsNullOrWhiteSpace(result.QualityReason));
    }
}
