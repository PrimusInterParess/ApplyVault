using ApplyVault.Api.Data;
using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public interface ICvStructuredUpdateService
{
    Task<CvStructuredDocumentDto> UpdateWithAiAsync(
        AppUserEntity user,
        UpdateCvStructuredWithAiRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CvStructuredUpdateService(
    ICvStructuredDocumentService structuredDocumentService,
    ICvStructuredUpdateAiClient updateAiClient) : ICvStructuredUpdateService
{
    public async Task<CvStructuredDocumentDto> UpdateWithAiAsync(
        AppUserEntity user,
        UpdateCvStructuredWithAiRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Instructions))
        {
            throw new InvalidOperationException("Describe what to update before asking AI to revise your CV.");
        }

        var current = await structuredDocumentService.GetStructuredAsync(user, cancellationToken)
            ?? throw new KeyNotFoundException("Structured CV content was not found.");

        if (current.Sections.Count == 0)
        {
            throw new InvalidOperationException("Import or create structured CV sections before asking AI to update them.");
        }

        var focusSectionIds = ResolveFocusSectionIds(current, request.SectionIds);

        var updated = await updateAiClient.UpdateAsync(
            current,
            request.Instructions.Trim(),
            focusSectionIds,
            cancellationToken);

        if (updated.Sections.Count == 0)
        {
            throw new InvalidOperationException("AI did not return any structured CV sections.");
        }

        return await structuredDocumentService.SaveStructuredAsync(
            user,
            updated,
            markImported: false,
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

            if (resolved.Contains(sectionId))
            {
                continue;
            }

            resolved.Add(sectionId);
        }

        return resolved;
    }
}
