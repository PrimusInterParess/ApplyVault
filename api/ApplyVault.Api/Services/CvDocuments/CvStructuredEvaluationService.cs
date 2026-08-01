using ApplyVault.Api.Data;
using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public interface ICvStructuredEvaluationService
{
    Task<CvQualityEvaluationDto> EvaluateAsync(
        AppUserEntity user,
        EvaluateCvQualityRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CvStructuredEvaluationService(
    ICvStructuredDocumentService structuredDocumentService,
    ICvStructuredEvaluationAiClient evaluationAiClient) : ICvStructuredEvaluationService
{
    public async Task<CvQualityEvaluationDto> EvaluateAsync(
        AppUserEntity user,
        EvaluateCvQualityRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.MaxFindings is < 1 or > 20)
        {
            throw new InvalidOperationException("Request between 1 and 20 CV evaluation findings.");
        }

        var current = await structuredDocumentService.GetStructuredAsync(user, cancellationToken)
            ?? throw new KeyNotFoundException("Structured CV content was not found.");

        if (current.Sections.Count == 0)
        {
            throw new InvalidOperationException("Import or create structured CV sections before asking AI for evaluation.");
        }

        return await evaluationAiClient.EvaluateAsync(
            current,
            request.MaxFindings,
            cancellationToken);
    }
}
