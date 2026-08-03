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
        var instructions = request.Instructions.Trim();
        var modelInput = GoogleAiCvStructuredUpdateClient.BuildPayloadForModel(current, focusSectionIds);

        var aiResult = await updateAiClient.UpdateAsync(
            modelInput,
            instructions,
            focusSectionIds,
            cancellationToken);

        if (aiResult.Document.Sections.Count == 0)
        {
            throw new InvalidOperationException("AI did not return any structured CV sections.");
        }

        var proposedSections = RestrictToFocusSections(
            RestoreDroppedEntryFields(
                current,
                AlignFocusSectionIds(current, ToProposedSections(aiResult.Document), focusSectionIds)),
            focusSectionIds);

        EnsureProposalHasChanges(current, proposedSections, focusSectionIds, instructions);

        // "What changed" from real text diffs only — ignore model claims.
        var changeBullets = DeriveChangeBullets(current, proposedSections, focusSectionIds);

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

    internal static IReadOnlyList<CvStructuredSectionDto> RestrictToFocusSections(
        IReadOnlyList<CvStructuredSectionDto> proposedSections,
        IReadOnlyList<Guid>? focusSectionIds)
    {
        if (focusSectionIds is null || focusSectionIds.Count == 0)
        {
            return proposedSections;
        }

        var byId = proposedSections
            .GroupBy((section) => section.Id)
            .ToDictionary((group) => group.Key, (group) => group.First());

        return focusSectionIds
            .Where(byId.ContainsKey)
            .Select((id) => byId[id])
            .ToArray();
    }

    /// <summary>
    /// If the model remints focus section ids, map them back by type/heading.
    /// </summary>
    internal static IReadOnlyList<CvStructuredSectionDto> AlignFocusSectionIds(
        CvStructuredDocumentDto current,
        IReadOnlyList<CvStructuredSectionDto> proposedSections,
        IReadOnlyList<Guid>? focusSectionIds)
    {
        if (focusSectionIds is null || focusSectionIds.Count == 0)
        {
            return proposedSections;
        }

        var proposed = proposedSections.ToList();
        var claimed = new HashSet<int>();
        var currentById = current.Sections.ToDictionary((section) => section.Id);

        foreach (var focusId in focusSectionIds)
        {
            if (!currentById.TryGetValue(focusId, out var currentSection))
            {
                continue;
            }

            var exact = proposed.FindIndex((section) => section.Id == focusId);
            if (exact >= 0)
            {
                claimed.Add(exact);
                continue;
            }

            var match = -1;
            for (var i = 0; i < proposed.Count; i++)
            {
                if (claimed.Contains(i))
                {
                    continue;
                }

                if (proposed[i].SectionType.Equals(currentSection.SectionType, StringComparison.OrdinalIgnoreCase)
                    && proposed[i].Heading.Trim().Equals(currentSection.Heading.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    match = i;
                    break;
                }
            }

            if (match < 0)
            {
                for (var i = 0; i < proposed.Count; i++)
                {
                    if (claimed.Contains(i))
                    {
                        continue;
                    }

                    if (proposed[i].SectionType.Equals(
                            currentSection.SectionType,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        match = i;
                        break;
                    }
                }
            }

            if (match < 0)
            {
                continue;
            }

            claimed.Add(match);
            proposed[match] = proposed[match] with { Id = focusId };
        }

        return proposed;
    }

    /// <summary>
    /// Model often blanks dateRange while rewriting — restore so we don't fake a "change".
    /// </summary>
    internal static IReadOnlyList<CvStructuredSectionDto> RestoreDroppedEntryFields(
        CvStructuredDocumentDto current,
        IReadOnlyList<CvStructuredSectionDto> proposedSections)
    {
        var currentById = current.Sections.ToDictionary((section) => section.Id);

        return proposedSections
            .Select((proposed) =>
            {
                if (!currentById.TryGetValue(proposed.Id, out var currentSection))
                {
                    return proposed;
                }

                var currentEntriesById = currentSection.Entries.ToDictionary((entry) => entry.Id);
                var entries = proposed.Entries
                    .Select((entry) =>
                    {
                        if (!currentEntriesById.TryGetValue(entry.Id, out var currentEntry))
                        {
                            return entry;
                        }

                        return entry with
                        {
                            DateRange = string.IsNullOrWhiteSpace(entry.DateRange)
                                ? currentEntry.DateRange
                                : entry.DateRange,
                            Subtitle = string.IsNullOrWhiteSpace(entry.Subtitle)
                                ? currentEntry.Subtitle
                                : entry.Subtitle
                        };
                    })
                    .ToArray();

                return proposed with { Entries = entries };
            })
            .ToArray();
    }

    internal static void EnsureProposalHasChanges(
        CvStructuredDocumentDto current,
        IReadOnlyList<CvStructuredSectionDto> proposedSections,
        IReadOnlyList<Guid>? focusSectionIds,
        string? instructions = null)
    {
        if (HasContentChanges(current, proposedSections, focusSectionIds))
        {
            return;
        }

        var needle = instructions?.Trim() ?? string.Empty;
        if (needle.Length >= 24)
        {
            var sections = focusSectionIds is { Count: > 0 }
                ? current.Sections.Where((section) => focusSectionIds.Contains(section.Id))
                : current.Sections;

            foreach (var section in sections)
            {
                foreach (var entry in section.Entries)
                {
                    var hit = (!string.IsNullOrWhiteSpace(entry.Summary)
                               && entry.Summary.Contains(needle, StringComparison.OrdinalIgnoreCase))
                              || entry.Bullets.Any((bullet) =>
                                  bullet.Contains(needle, StringComparison.OrdinalIgnoreCase));
                    if (!hit)
                    {
                        continue;
                    }

                    var where = string.IsNullOrWhiteSpace(entry.Subtitle)
                        ? section.Heading
                        : $"{section.Heading} — {entry.Subtitle.Trim()}";
                    throw new InvalidOperationException(
                        $"That text is already in your CV ({where}). "
                        + "Say what to change, e.g. move/remove it to another employer.");
                }
            }
        }

        throw new InvalidOperationException(
            focusSectionIds is { Count: > 0 }
                ? "AI returned no changes to the selected section. Try clearer instructions or try again."
                : "AI returned no changes to your CV. Try clearer instructions or try again.");
    }

    private static bool HasContentChanges(
        CvStructuredDocumentDto current,
        IReadOnlyList<CvStructuredSectionDto> proposedSections,
        IReadOnlyList<Guid>? focusSectionIds)
    {
        var currentById = current.Sections.ToDictionary((section) => section.Id);
        var proposedById = proposedSections
            .GroupBy((section) => section.Id)
            .ToDictionary((group) => group.Key, (group) => group.First());

        if (focusSectionIds is { Count: > 0 })
        {
            if (focusSectionIds.Any((id) => !proposedById.ContainsKey(id)))
            {
                throw new InvalidOperationException("AI did not return the selected CV section. Try again.");
            }

            return focusSectionIds.Any((id) =>
                proposedById.TryGetValue(id, out var proposed)
                && currentById.TryGetValue(id, out var currentSection)
                && !CvStructuredSectionContentComparer.Equals(currentSection, proposed));
        }

        return proposedSections.Any((proposed) =>
            !currentById.TryGetValue(proposed.Id, out var currentSection)
            || !CvStructuredSectionContentComparer.Equals(currentSection, proposed));
    }

    private static IReadOnlyList<string> DeriveChangeBullets(
        CvStructuredDocumentDto current,
        IReadOnlyList<CvStructuredSectionDto> proposedSections,
        IReadOnlyList<Guid>? focusSectionIds)
    {
        var currentById = current.Sections.ToDictionary((section) => section.Id);
        var bullets = new List<string>();

        foreach (var proposed in proposedSections)
        {
            if (focusSectionIds is { Count: > 0 } && !focusSectionIds.Contains(proposed.Id))
            {
                continue;
            }

            if (!currentById.TryGetValue(proposed.Id, out var currentSection))
            {
                bullets.Add(Truncate($"Added {proposed.Heading}.", MaxBulletLength));
                continue;
            }

            if (CvStructuredSectionContentComparer.Equals(currentSection, proposed))
            {
                continue;
            }

            bullets.AddRange(DescribeEntryDeltas(currentSection, proposed));
            if (bullets.Count >= MaxChangeBullets)
            {
                break;
            }
        }

        if (bullets.Count == 0)
        {
            return ["Updated CV sections per your instructions."];
        }

        return bullets.Take(MaxChangeBullets).ToArray();
    }

    private static IEnumerable<string> DescribeEntryDeltas(
        CvStructuredSectionDto current,
        CvStructuredSectionDto proposed)
    {
        var currentById = current.Entries.ToDictionary((entry) => entry.Id);
        var lines = new List<string>();

        foreach (var proposedEntry in proposed.Entries.OrderBy((entry) => entry.SortOrder))
        {
            var label = EntryLabel(proposedEntry);

            if (!currentById.TryGetValue(proposedEntry.Id, out var currentEntry))
            {
                lines.Add(Truncate($"Added entry under {proposed.Heading}: {label}.", MaxBulletLength));
                continue;
            }

            var currentBullets = currentEntry.Bullets
                .Select((bullet) => bullet.Trim())
                .Where((bullet) => bullet.Length > 0)
                .ToList();
            var proposedBullets = proposedEntry.Bullets
                .Select((bullet) => bullet.Trim())
                .Where((bullet) => bullet.Length > 0)
                .ToList();

            var removed = currentBullets.Except(proposedBullets, StringComparer.Ordinal).ToList();
            var added = proposedBullets.Except(currentBullets, StringComparer.Ordinal).ToList();

            foreach (var next in added.ToArray())
            {
                var prior = removed.FirstOrDefault((bullet) => IsRelatedBullet(bullet, next));
                if (prior is null)
                {
                    continue;
                }

                lines.Add(Truncate($"Rewrote under {label}: {Snippet(next)}", MaxBulletLength));
                removed.Remove(prior);
                added.Remove(next);
            }

            foreach (var gone in removed)
            {
                lines.Add(Truncate($"Removed under {label}: {Snippet(gone)}", MaxBulletLength));
            }

            foreach (var next in added)
            {
                lines.Add(Truncate($"Added under {label}: {Snippet(next)}", MaxBulletLength));
            }

            if (!string.Equals(
                    currentEntry.Summary?.Trim() ?? string.Empty,
                    proposedEntry.Summary?.Trim() ?? string.Empty,
                    StringComparison.Ordinal))
            {
                lines.Add(Truncate($"Updated summary under {label}.", MaxBulletLength));
            }
        }

        return lines.Count > 0 ? lines : [Truncate($"Updated {proposed.Heading}.", MaxBulletLength)];
    }

    private static string EntryLabel(CvStructuredEntryDto entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Subtitle))
        {
            return entry.Subtitle.Trim();
        }

        if (!string.IsNullOrWhiteSpace(entry.Title))
        {
            return entry.Title.Trim();
        }

        return "entry";
    }

    private static bool IsRelatedBullet(string left, string right)
    {
        var a = left.Trim();
        var b = right.Trim();
        if (a.Length == 0 || b.Length == 0)
        {
            return false;
        }

        var prefixLen = Math.Min(24, Math.Min(a.Length, b.Length));
        return a.StartsWith(b[..prefixLen], StringComparison.OrdinalIgnoreCase)
               || b.StartsWith(a[..prefixLen], StringComparison.OrdinalIgnoreCase);
    }

    private static string Snippet(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= 90 ? trimmed : trimmed[..87] + "…";
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
