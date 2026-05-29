using ApplyVault.Api.Services.Jobnet;

namespace ApplyVault.Api.Tests;

public sealed class JobDescriptionHeuristicRulesTests
{
    [Fact]
    public void ShouldUsePreviewOnly_ForScrapedExternalLayout()
    {
        var request = new JobnetDescriptionAssessmentRequest(
            JobnetJobDetailTestData.ScrapedExternalDescription,
            "Developer — Responsive",
            "RESPONSIVE A/S",
            "E10990623",
            JobnetDescriptionSource.SearchFallback);

        Assert.True(JobDescriptionHeuristicRules.ShouldUsePreviewOnly(request));
    }

    [Fact]
    public void ShouldUsePreviewOnly_IsFalse_ForNativeStructuredHtml()
    {
        var request = new JobnetDescriptionAssessmentRequest(
            "<p><strong>We are hiring</strong></p><p>Build APIs with C# across distributed systems and ship production features every week.</p>",
            "Backend Developer",
            "Contoso A/S",
            "b2b58b21-1353-47c7-afdb-5bb1ff15fd5a",
            JobnetDescriptionSource.NativeDetail);

        Assert.False(JobDescriptionHeuristicRules.ShouldUsePreviewOnly(request));
    }
}
