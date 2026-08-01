using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public interface ICvStructuredEvaluationAiClient
{
    Task<CvQualityEvaluationDto> EvaluateAsync(
        CvStructuredDocumentDto current,
        int maxFindings = 8,
        CancellationToken cancellationToken = default);
}

internal sealed record CvStructuredEvaluationAiResponse(
    int OverallScore,
    string Summary,
    IReadOnlyList<CvStructuredEvaluationAiDimension> Dimensions,
    IReadOnlyList<CvStructuredEvaluationAiFinding> Findings,
    IReadOnlyList<string>? SelfCheckQuestions);

internal sealed record CvStructuredEvaluationAiDimension(
    string Id,
    int Score,
    string Summary);

internal sealed record CvStructuredEvaluationAiFinding(
    string? Id,
    string Dimension,
    string Severity,
    string Title,
    string Detail,
    string? SectionId,
    string? EntryId);
