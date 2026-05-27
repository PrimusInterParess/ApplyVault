using ApplyVault.Api.Models;

namespace ApplyVault.Api.IntegrationTests;

internal static class IntegrationTestScrapeFactory
{
    public static ScrapeResultDto Create(string? url = null)
    {
        var jobUrl = url ?? $"https://jobs.example.com/listings/{Guid.NewGuid():N}";
        var extractedAt = DateTimeOffset.UtcNow.ToString("O");

        return new ScrapeResultDto(
            Title: "Backend Developer",
            Url: jobUrl,
            Text: "Build and maintain backend services.",
            TextLength: 36,
            ExtractedAt: extractedAt,
            JobDetails: new JobDetailsDto(
                SourceHostname: "jobs.example.com",
                DetectedPageType: "jobPosting",
                JobTitle: "Backend Developer",
                CompanyName: "Contoso",
                Location: "Remote",
                JobDescription: "Build APIs.",
                PositionSummary: null,
                HiringManagerName: null,
                HiringManagerContacts: []));
    }
}
