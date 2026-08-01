using ApplyVault.Api.Data;
using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

public interface ICvStructuredUpdateProposeService
{
    Task<CvUpdateProposalDto> ProposeAsync(
        AppUserEntity user,
        UpdateCvStructuredWithAiRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CvStructuredUpdateProposeService(
    ICvStructuredDocumentService structuredDocumentService,
    ICvStructuredUpdateAiClient updateAiClient) : ICvStructuredUpdateProposeService
{
    private const int MaxChangeBullets = 5;
    private const int MaxBulletLength = 200;

    public async Task<CvUpdateProposalDto> ProposeAsync(
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

        var aiResult = await updateAiClient.UpdateAsync(
            current,
            request.Instructions.Trim(),
            focusSectionIds,
            cancellationToken);

        if (aiResult.Document.Sections.Count == 0)
        {
            throw new InvalidOperationException("AI did not return any structured CV sections.");
        }

        var proposedSections = ToProposedSections(aiResult.Document);
        var changeBullets = NormalizeChangeBullets(
            aiResult.ChangeBullets,
            current,
            proposedSections,
            focusSectionIds);

        return new CvUpdateProposalDto(
            current.DocumentId,
            focusSectionIds ?? [],
            changeBullets,
            proposedSections);
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

    private static IReadOnlyList<CvStructuredSectionDto> ToProposedSections(
        SaveCvStructuredDocumentRequest document) =>
        document.Sections
            .Select((section, sectionIndex) => new CvStructuredSectionDto(
                section.Id ?? Guid.NewGuid(),
                section.Heading,
                section.SectionType,
                sectionIndex,
                section.Entries
                    .Select((entry, entryIndex) => new CvStructuredEntryDto(
                        entry.Id ?? Guid.NewGuid(),
                        entry.Title,
                        entry.Subtitle,
                        entry.DateRange,
                        entry.Summary,
                        entry.Bullets,
                        entry.TechStack,
                        new Dictionary<string, object?>(),
                        entry.Source,
                        entry.SourceSummaryId,
                        entryIndex))
                    .ToArray()))
            .ToArray();

    private static IReadOnlyList<string> NormalizeChangeBullets(
        IReadOnlyList<string> aiBullets,
        CvStructuredDocumentDto current,
        IReadOnlyList<CvStructuredSectionDto> proposedSections,
        IReadOnlyList<Guid>? focusSectionIds)
    {
        var bullets = aiBullets
            .Where((bullet) => !string.IsNullOrWhiteSpace(bullet))
            .Select((bullet) => Truncate(bullet.Trim(), MaxBulletLength))
            .Take(MaxChangeBullets)
            .ToArray();

        if (bullets.Length > 0)
        {
            return bullets;
        }

        return DeriveFallbackBullets(current, proposedSections, focusSectionIds);
    }

    private static IReadOnlyList<string> DeriveFallbackBullets(
        CvStructuredDocumentDto current,
        IReadOnlyList<CvStructuredSectionDto> proposedSections,
        IReadOnlyList<Guid>? focusSectionIds)
    {
        if (focusSectionIds is { Count: > 0 })
        {
            var byId = current.Sections.ToDictionary((section) => section.Id);
            return focusSectionIds
                .Select((id) =>
                    byId.TryGetValue(id, out var section)
                        ? Truncate($"Updated {section.Heading}.", MaxBulletLength)
                        : "Updated selected section.")
                .Take(MaxChangeBullets)
                .ToArray();
        }

        if (proposedSections.Count > 0)
        {
            return proposedSections
                .Take(MaxChangeBullets)
                .Select((section) => Truncate($"Updated {section.Heading}.", MaxBulletLength))
                .ToArray();
        }

        return ["Updated CV sections per your instructions."];
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
