using ApplyVault.Api.Data;
using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public interface ICvStructuredSummaryProposeService
{
    Task<CvSummaryProposalDto> ProposeAsync(
        AppUserEntity user,
        ProposeCvSummaryRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CvStructuredSummaryProposeService(
    ICvStructuredDocumentService structuredDocumentService,
    ICvStructuredSummaryProposeAiClient proposeAiClient) : ICvStructuredSummaryProposeService
{
    private const int MaxChangeBullets = 5;
    private const int MaxBulletLength = 200;

    public async Task<CvSummaryProposalDto> ProposeAsync(
        AppUserEntity user,
        ProposeCvSummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        var current = await structuredDocumentService.GetStructuredAsync(user, cancellationToken)
            ?? throw new KeyNotFoundException("Structured CV content was not found.");

        if (current.Sections.Count == 0)
        {
            throw new InvalidOperationException(
                "Import or create structured CV sections before asking AI to propose a summary.");
        }

        var summarySection = ResolveSummarySection(current);
        var currentSummaryText = ExtractCurrentSummaryText(summarySection);
        var instructions = string.IsNullOrWhiteSpace(request.Instructions)
            ? null
            : request.Instructions.Trim();

        var aiResult = await proposeAiClient.ProposeAsync(
            current,
            instructions,
            user.DisplayName,
            user.Email,
            cancellationToken);

        var proposedSummaryText = aiResult.ProposedSummaryText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(proposedSummaryText))
        {
            throw new InvalidOperationException("AI returned an empty proposed summary.");
        }

        return new CvSummaryProposalDto(
            current.DocumentId,
            summarySection?.Id,
            currentSummaryText,
            proposedSummaryText,
            NormalizeChangeBullets(aiResult.ChangeBullets));
    }

    private static CvStructuredSectionDto? ResolveSummarySection(CvStructuredDocumentDto current) =>
        current.Sections
            .Where((section) =>
                string.Equals(section.SectionType, CvSectionTypes.Summary, StringComparison.OrdinalIgnoreCase))
            .OrderBy((section) => section.SortOrder)
            .FirstOrDefault();

    private static string ExtractCurrentSummaryText(CvStructuredSectionDto? summarySection)
    {
        if (summarySection is null)
        {
            return string.Empty;
        }

        var entry = summarySection.Entries
            .OrderBy((item) => item.SortOrder)
            .FirstOrDefault();

        return entry?.Summary?.Trim() ?? string.Empty;
    }

    private static IReadOnlyList<string> NormalizeChangeBullets(IReadOnlyList<string>? changeBullets) =>
        (changeBullets ?? [])
            .Where((bullet) => !string.IsNullOrWhiteSpace(bullet))
            .Select((bullet) => Truncate(bullet.Trim(), MaxBulletLength))
            .Take(MaxChangeBullets)
            .ToArray();

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
