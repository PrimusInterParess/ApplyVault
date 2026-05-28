using ApplyVault.Api.Data;
using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public interface ICvStructuredSuggestionsService
{
    Task<CvImprovementSuggestionsDto> GenerateAsync(
        AppUserEntity user,
        GenerateCvImprovementSuggestionsRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CvStructuredSuggestionsService(
    ICvStructuredDocumentService structuredDocumentService,
    ICvStructuredSuggestionsAiClient suggestionsAiClient) : ICvStructuredSuggestionsService
{
    public async Task<CvImprovementSuggestionsDto> GenerateAsync(
        AppUserEntity user,
        GenerateCvImprovementSuggestionsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.MaxSuggestions is < 1 or > 10)
        {
            throw new InvalidOperationException("Request between 1 and 10 CV suggestions.");
        }

        var current = await structuredDocumentService.GetStructuredAsync(user, cancellationToken)
            ?? throw new KeyNotFoundException("Structured CV content was not found.");

        if (current.Sections.Count == 0)
        {
            throw new InvalidOperationException("Import or create structured CV sections before asking AI for suggestions.");
        }

        var focusSectionIds = ResolveFocusSectionIds(current, request.SectionIds);

        return await suggestionsAiClient.GenerateAsync(
            current,
            focusSectionIds,
            request.MaxSuggestions,
            cancellationToken);
    }

    private static IReadOnlyList<Guid>? ResolveFocusSectionIds(
        CvStructuredDocumentDto current,
        IReadOnlyList<Guid>? sectionIds)
    {
        if (sectionIds is null || sectionIds.Count == 0)
        {
            return null;
        }

        var knownSectionIds = current.Sections.Select((section) => section.Id).ToHashSet();
        var resolved = new List<Guid>();

        foreach (var sectionId in sectionIds)
        {
            if (!knownSectionIds.Contains(sectionId))
            {
                throw new InvalidOperationException("One or more selected CV sections were not found.");
            }

            if (!resolved.Contains(sectionId))
            {
                resolved.Add(sectionId);
            }
        }

        return resolved;
    }
}
