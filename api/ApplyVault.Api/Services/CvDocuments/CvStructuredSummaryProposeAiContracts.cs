using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public interface ICvStructuredSummaryProposeAiClient
{
    Task<CvSummaryProposeAiResult> ProposeAsync(
        CvStructuredDocumentDto current,
        string? instructions,
        string? appUserDisplayName,
        string? appUserEmail,
        CancellationToken cancellationToken = default);
}

public sealed record CvSummaryProposeAiResult(
    string ProposedSummaryText,
    IReadOnlyList<string> ChangeBullets);

internal sealed record CvStructuredSummaryProposeAiResponse(
    string ProposedSummaryText,
    IReadOnlyList<string>? ChangeBullets);
