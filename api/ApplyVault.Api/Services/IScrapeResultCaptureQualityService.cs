using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public interface IScrapeResultCaptureQualityService
{
    AssessedScrapeResult Assess(ScrapeResultDto request);
}

public sealed record AssessedScrapeResult(
    ScrapeResultDto Payload,
    ScrapeResultCaptureQualityAssessment CaptureQuality
);

public sealed record ScrapeResultCaptureQualityAssessment(
    double OverallConfidence,
    ScrapeResultFieldAssessment JobTitle,
    ScrapeResultFieldAssessment CompanyName,
    ScrapeResultFieldAssessment Location,
    ScrapeResultFieldAssessment JobDescription
);

public sealed record ScrapeResultFieldAssessment(
    double Confidence,
    string? ReviewReason
);
