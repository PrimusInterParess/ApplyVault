using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Services;

public interface ICvStructuredImportService
{
    Task<CvStructuredImportPreviewDto> PreviewImportAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default);

    Task<CvStructuredDocumentDto> ConfirmImportAsync(
        AppUserEntity user,
        SaveCvStructuredDocumentRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CvStructuredImportService(
    ApplyVaultDbContext dbContext,
    ICvDocumentStorage cvDocumentStorage,
    ICvPdfFullTextExtractor fullTextExtractor,
    ICvStructuredImportAiClient importAiClient,
    ICvStructuredDocumentService structuredDocumentService,
    IOptions<GoogleAiOptions> googleAiOptions) : ICvStructuredImportService
{
    public async Task<CvStructuredImportPreviewDto> PreviewImportAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default)
    {
        var document = await dbContext.UserCvDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync((entry) => entry.UserId == user.Id, cancellationToken)
            ?? throw new InvalidOperationException("Upload a CV PDF before importing content.");

        var baseStorageKey = document.BaseStorageKey ?? document.StorageKey;

        await using var baseStream = await cvDocumentStorage.OpenReadAsync(baseStorageKey, cancellationToken);

        if (baseStream.CanSeek)
        {
            baseStream.Position = 0;
        }

        var rawSections = fullTextExtractor.ExtractSections(baseStream);

        if (rawSections.Count == 0)
        {
            throw new InvalidOperationException("No readable text was found in the uploaded CV PDF.");
        }

        if (googleAiOptions.Value.Enabled)
        {
            try
            {
                var aiInput = rawSections
                    .Select((section) => new CvImportSectionInput(section.Heading, section.NormalizedKey, section.Text))
                    .ToArray();

                var aiResult = await importAiClient.ParseAsync(aiInput, cancellationToken);

                var aiSections = aiResult.Sections
                    .Select((section, index) => new CvStructuredSectionWriteDto(
                        null,
                        section.Heading,
                        CvSectionTypes.Normalize(section.SectionType),
                        index,
                        section.Entries
                            .Select((entry, entryIndex) => new CvStructuredEntryWriteDto(
                                null,
                                entry.Title,
                                entry.Subtitle,
                                entry.DateRange,
                                entry.Summary,
                                entry.Bullets,
                                entry.TechStack,
                                CvEntrySources.Import,
                                null,
                                entryIndex))
                            .ToArray()))
                    .ToArray();

                if (aiSections.Length > 0)
                {
                    return new CvStructuredImportPreviewDto(aiSections, true, null);
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch
            {
                // Fall back to heuristic parsing when AI fails.
            }
        }

        var heuristicSections = CvStructuredImportHeuristic.Parse(rawSections);

        return new CvStructuredImportPreviewDto(
            heuristicSections,
            false,
            googleAiOptions.Value.Enabled
                ? "AI parsing failed; a basic structure was generated instead. Review before saving."
                : "Google AI is disabled; a basic structure was generated. Enable GoogleAi:Enabled for richer import.");
    }

    public Task<CvStructuredDocumentDto> ConfirmImportAsync(
        AppUserEntity user,
        SaveCvStructuredDocumentRequest request,
        CancellationToken cancellationToken = default) =>
        structuredDocumentService.SaveStructuredAsync(user, request, markImported: true, cancellationToken);
}
