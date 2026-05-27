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

        var updated = await updateAiClient.UpdateAsync(current, request.Instructions.Trim(), cancellationToken);

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
}
