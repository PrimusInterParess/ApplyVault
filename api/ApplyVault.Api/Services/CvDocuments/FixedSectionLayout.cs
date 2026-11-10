using ApplyVault.Api.Models;
using ApplyVault.Api.Services.CvSectionCatalog;

namespace ApplyVault.Api.Services;

/// <summary>
/// Projects any list of Structured CV sections onto the fixed, ordered slot layout
/// defined by the section catalog (<c>fixedLayout</c>). Headings and sort order are
/// owned by the catalog; entries are never dropped — sections that map to the same
/// slot are merged in order, and unknown/Custom sections fold into the Custom slot.
/// </summary>
internal static class FixedSectionLayout
{
    private const string ContactHeading = "Contact";

    public static IReadOnlyList<CvStructuredSectionWriteDto> Normalize(
        IReadOnlyList<CvStructuredSectionWriteDto> sections,
        ICvSectionCatalog catalog)
    {
        var slots = catalog.FixedLayout;
        var slotIndexByType = BuildSlotIndex(slots);
        var customSlotIndex = ResolveCustomSlotIndex(slots, slotIndexByType);

        var idBySlot = new Guid?[slots.Count];
        var idClaimedBySlot = new bool[slots.Count];
        var includedBySlot = new bool?[slots.Count];
        var includedClaimedBySlot = new bool[slots.Count];
        var entriesBySlot = new List<CvStructuredEntryWriteDto>[slots.Count];

        for (var index = 0; index < slots.Count; index++)
        {
            entriesBySlot[index] = [];
        }

        foreach (var section in sections.OrderBy((section) => section.SortOrder))
        {
            var slotIndex = ResolveSlotIndex(section.Heading, section.SectionType, slotIndexByType, customSlotIndex);

            if (!idClaimedBySlot[slotIndex])
            {
                idBySlot[slotIndex] = section.Id;
                idClaimedBySlot[slotIndex] = true;
                includedBySlot[slotIndex] = section.IncludedOnTemplate;
                includedClaimedBySlot[slotIndex] = true;
            }

            entriesBySlot[slotIndex].AddRange(section.Entries.OrderBy((entry) => entry.SortOrder));
        }

        var result = new CvStructuredSectionWriteDto[slots.Count];

        for (var index = 0; index < slots.Count; index++)
        {
            var slot = slots[index];
            var included = ResolveIncludedOnTemplate(slot.SectionType, includedClaimedBySlot[index], includedBySlot[index]);

            result[index] = new CvStructuredSectionWriteDto(
                idBySlot[index],
                slot.Heading,
                slot.SectionType,
                index,
                entriesBySlot[index]
                    .Select((entry, entryIndex) => entry with { SortOrder = entryIndex })
                    .ToArray(),
                included);
        }

        return result;
    }

    public static IReadOnlyList<CvStructuredSectionWriteDto> Normalize(
        IReadOnlyList<CvStructuredSectionDto> sections,
        ICvSectionCatalog catalog) =>
        Normalize(sections.Select(ToWriteDto).ToArray(), catalog);

    public static bool IsCanonical(IReadOnlyList<CvStructuredSectionDto> sections, ICvSectionCatalog catalog)
    {
        var slots = catalog.FixedLayout;

        if (sections.Count != slots.Count)
        {
            return false;
        }

        var ordered = sections.OrderBy((section) => section.SortOrder).ToArray();

        for (var index = 0; index < slots.Count; index++)
        {
            var section = ordered[index];
            var slot = slots[index];

            if (section.SortOrder != index
                || !string.Equals(section.SectionType, slot.SectionType, StringComparison.Ordinal)
                || !string.Equals(section.Heading, slot.Heading, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Seven empty slots; Contact carries Name plus empty Email/Phone/Location/LinkedIn/GitHub/Website
    /// channels (aligned with the blank-start contact starter set).
    /// </summary>
    public static IReadOnlyList<CvStructuredSectionWriteDto> CreateBlank(ICvSectionCatalog catalog)
    {
        var slots = catalog.FixedLayout;
        var result = new CvStructuredSectionWriteDto[slots.Count];

        for (var index = 0; index < slots.Count; index++)
        {
            var slot = slots[index];
            IReadOnlyList<CvStructuredEntryWriteDto> entries =
                string.Equals(slot.SectionType, CvSectionTypes.Contact, StringComparison.Ordinal)
                    ? CreateStarterContactEntries()
                    : [];

            result[index] = new CvStructuredSectionWriteDto(
                null,
                slot.Heading,
                slot.SectionType,
                index,
                entries,
                IncludedOnTemplate: true);
        }

        return result;
    }

    public static CvStructuredSectionWriteDto ToWriteDto(CvStructuredSectionDto section) =>
        new(
            section.Id,
            section.Heading,
            section.SectionType,
            section.SortOrder,
            section.Entries
                .OrderBy((entry) => entry.SortOrder)
                .Select((entry, entryIndex) => new CvStructuredEntryWriteDto(
                    entry.Id,
                    entry.Title,
                    entry.Subtitle,
                    entry.DateRange,
                    entry.Summary,
                    entry.Bullets,
                    entry.TechStack,
                    entry.Source,
                    entry.SourceSummaryId,
                    entryIndex))
                .ToArray(),
            section.IncludedOnTemplate);

    private static IReadOnlyList<CvStructuredEntryWriteDto> CreateStarterContactEntries() =>
    [
        StarterContactEntry("Name", 0, bullets: []),
        StarterContactEntry("Email", 1, bullets: [string.Empty]),
        StarterContactEntry("Phone", 2, bullets: [string.Empty]),
        StarterContactEntry("Location", 3, bullets: [string.Empty]),
        StarterContactEntry("LinkedIn", 4, bullets: [string.Empty]),
        StarterContactEntry("GitHub", 5, bullets: [string.Empty]),
        StarterContactEntry("Website", 6, bullets: [string.Empty])
    ];

    private static CvStructuredEntryWriteDto StarterContactEntry(string title, int sortOrder, IReadOnlyList<string> bullets) =>
        new(
            null,
            title,
            null,
            null,
            string.Empty,
            bullets,
            string.Empty,
            CvEntrySources.Manual,
            null,
            sortOrder);

    /// <summary>
    /// Null and true mean included. A slot created because it was missing is included.
    /// Contact cannot be omitted.
    /// </summary>
    private static bool ResolveIncludedOnTemplate(string sectionType, bool claimed, bool? includedOnTemplate)
    {
        var included = !claimed || includedOnTemplate != false;

        return string.Equals(sectionType, CvSectionTypes.Contact, StringComparison.Ordinal) || included;
    }

    private static Dictionary<string, int> BuildSlotIndex(IReadOnlyList<CvFixedSectionSlot> slots)
    {
        var slotIndexByType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < slots.Count; index++)
        {
            slotIndexByType.TryAdd(slots[index].SectionType, index);
        }

        return slotIndexByType;
    }

    private static int ResolveCustomSlotIndex(
        IReadOnlyList<CvFixedSectionSlot> slots,
        Dictionary<string, int> slotIndexByType) =>
        slotIndexByType.TryGetValue(CvSectionTypes.Custom, out var customIndex)
            ? customIndex
            : slots.Count - 1;

    private static int ResolveSlotIndex(
        string? heading,
        string? sectionType,
        Dictionary<string, int> slotIndexByType,
        int customSlotIndex)
    {
        // Export parity (CvExportHtmlMapper.IsContactSection): heading "Contact" is Contact.
        if (string.Equals(heading?.Trim(), ContactHeading, StringComparison.OrdinalIgnoreCase)
            && slotIndexByType.TryGetValue(CvSectionTypes.Contact, out var contactIndex))
        {
            return contactIndex;
        }

        var normalizedType = CvSectionTypes.Normalize(sectionType);

        return slotIndexByType.TryGetValue(normalizedType, out var slotIndex)
            ? slotIndex
            : customSlotIndex;
    }
}
