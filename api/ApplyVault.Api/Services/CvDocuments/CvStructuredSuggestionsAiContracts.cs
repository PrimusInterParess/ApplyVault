using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public interface ICvStructuredSuggestionsAiClient
{
    Task<CvImprovementSuggestionsDto> GenerateAsync(
        CvStructuredDocumentDto current,
        IReadOnlyList<Guid>? focusSectionIds = null,
        int maxSuggestions = 6,
        CancellationToken cancellationToken = default);
}

internal sealed record CvStructuredSuggestionsAiResponse(
    IReadOnlyList<CvStructuredSuggestionsAiSuggestion> Suggestions);

internal sealed record CvStructuredSuggestionsAiSuggestion(
    string? Id,
    string Title,
    string Rationale,
    string SuggestedInstruction,
    string? SectionId,
    string? EntryId,
    string Category,
    string Impact);
