using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using ApplyVault.Api.Services.CvSectionCatalog;

namespace ApplyVault.Api.Tests;

public sealed class FixedSectionLayoutTests
{
    private static readonly ICvSectionCatalog Catalog = CvSectionCatalogProvider.LoadFromDefaultPath();

    [Fact]
    public void Normalize_MergesSameSlotKeepingFirstIdAndReindexingEntries()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var firstEntryId = Guid.NewGuid();
        var secondEntryId = Guid.NewGuid();

        var normalized = FixedSectionLayout.Normalize(
            [
                Section(
                    firstId,
                    "Jobs",
                    CvSectionTypes.Experience,
                    4,
                    Entry(firstEntryId, "First", 2)),
                Section(
                    secondId,
                    "Experience",
                    CvSectionTypes.Experience,
                    5,
                    Entry(secondEntryId, "Second", 0))
            ],
            Catalog);

        AssertCanonical(normalized);

        var experience = Slot(normalized, CvSectionTypes.Experience);
        Assert.Equal(firstId, experience.Id);
        Assert.Equal("Experience", experience.Heading);
        Assert.Equal(
            [firstEntryId, secondEntryId],
            experience.Entries.Select((entry) => entry.Id).ToArray());
        Assert.Equal([0, 1], experience.Entries.Select((entry) => entry.SortOrder).ToArray());
    }

    [Fact]
    public void Normalize_FoldsCustomAndUnknownIntoLastSlot()
    {
        var customId = Guid.NewGuid();
        var unknownId = Guid.NewGuid();
        var customEntryId = Guid.NewGuid();
        var unknownEntryId = Guid.NewGuid();

        var normalized = FixedSectionLayout.Normalize(
            [
                Section(customId, "Publications", CvSectionTypes.Custom, 0, Entry(customEntryId, "Paper", 0)),
                Section(unknownId, "Volunteer", "NotAType", 1, Entry(unknownEntryId, "Kitchen", 0))
            ],
            Catalog);

        AssertCanonical(normalized);

        var custom = Slot(normalized, CvSectionTypes.Custom);
        Assert.Equal(customId, custom.Id);
        Assert.Equal("Certifications & Languages", custom.Heading);
        Assert.Equal(
            [customEntryId, unknownEntryId],
            custom.Entries.Select((entry) => entry.Id).ToArray());
        Assert.Equal(Catalog.FixedLayout.Count - 1, custom.SortOrder);
    }

    [Fact]
    public void Normalize_CreatesMissingSlots()
    {
        var experienceId = Guid.NewGuid();

        var normalized = FixedSectionLayout.Normalize(
            [
                Section(experienceId, "Experience", CvSectionTypes.Experience, 0, Entry(Guid.NewGuid(), "Acme", 0))
            ],
            Catalog);

        AssertCanonical(normalized);
        Assert.Equal(experienceId, Slot(normalized, CvSectionTypes.Experience).Id);
        Assert.All(
            normalized.Where((section) => section.SectionType != CvSectionTypes.Experience),
            (section) =>
            {
                Assert.Null(section.Id);
                Assert.Empty(section.Entries);
            });
    }

    [Fact]
    public void Normalize_ForcesHeadingAndSortOrderFromCatalog()
    {
        var experienceId = Guid.NewGuid();

        var normalized = FixedSectionLayout.Normalize(
            [
                Section(experienceId, "Work History", CvSectionTypes.Experience, 99, Entry(Guid.NewGuid(), "Acme", 0))
            ],
            Catalog);

        var experience = Slot(normalized, CvSectionTypes.Experience);
        Assert.Equal("Experience", experience.Heading);
        Assert.Equal(ExperienceSlotIndex, experience.SortOrder);
        Assert.Equal(experience.SortOrder, normalized.ToList().IndexOf(experience));
    }

    [Fact]
    public void Normalize_MapsContactByHeadingEvenWhenTypeDiffers()
    {
        var contactId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        var normalized = FixedSectionLayout.Normalize(
            [
                Section(contactId, "Contact", CvSectionTypes.Experience, 2, Entry(entryId, "Email", 0))
            ],
            Catalog);

        AssertCanonical(normalized);

        var contact = Slot(normalized, CvSectionTypes.Contact);
        Assert.Equal(contactId, contact.Id);
        Assert.Equal("Contact", contact.Heading);
        Assert.Equal(entryId, Assert.Single(contact.Entries).Id);
        Assert.Empty(Slot(normalized, CvSectionTypes.Experience).Entries);
    }

    [Fact]
    public void IsCanonical_RequiresExactSlotCountTypeHeadingAndOrder()
    {
        var blank = FixedSectionLayout.CreateBlank(Catalog);
        var canonical = ToRead(blank);

        Assert.True(FixedSectionLayout.IsCanonical(canonical, Catalog));
        Assert.False(FixedSectionLayout.IsCanonical(canonical.Take(6).ToArray(), Catalog));

        var wrongHeading = canonical
            .Select((section, index) =>
                index == ExperienceSlotIndex
                    ? section with { Heading = "Work History" }
                    : section)
            .ToArray();
        Assert.False(FixedSectionLayout.IsCanonical(wrongHeading, Catalog));

        var wrongOrder = canonical
            .Select((section, index) =>
                index == ExperienceSlotIndex
                    ? section with { SortOrder = 99 }
                    : section)
            .ToArray();
        Assert.False(FixedSectionLayout.IsCanonical(wrongOrder, Catalog));
    }

    [Fact]
    public void CreateBlank_SeedsSevenSlotsAndContactChannels()
    {
        var blank = FixedSectionLayout.CreateBlank(Catalog);

        AssertCanonical(blank);

        var contact = Slot(blank, CvSectionTypes.Contact);
        Assert.Equal(
            ["Name", "Email", "Phone", "Location", "LinkedIn", "GitHub", "Website"],
            contact.Entries.Select((entry) => entry.Title).ToArray());
        Assert.Empty(contact.Entries[0].Bullets);
        Assert.All(contact.Entries.Skip(1), (entry) => Assert.Equal([string.Empty], entry.Bullets));
        Assert.All(
            blank.Where((section) => section.SectionType != CvSectionTypes.Contact),
            (section) => Assert.Empty(section.Entries));
    }

    private static int ExperienceSlotIndex =>
        Catalog.FixedLayout.ToList().FindIndex((slot) => slot.SectionType == CvSectionTypes.Experience);

    private static void AssertCanonical(IReadOnlyList<CvStructuredSectionWriteDto> sections)
    {
        Assert.Equal(Catalog.FixedLayout.Count, sections.Count);

        for (var index = 0; index < Catalog.FixedLayout.Count; index++)
        {
            var slot = Catalog.FixedLayout[index];
            var section = sections[index];

            Assert.Equal(index, section.SortOrder);
            Assert.Equal(slot.SectionType, section.SectionType);
            Assert.Equal(slot.Heading, section.Heading);
        }
    }

    private static CvStructuredSectionWriteDto Slot(
        IReadOnlyList<CvStructuredSectionWriteDto> sections,
        string sectionType) =>
        sections.Single((section) => section.SectionType == sectionType);

    private static CvStructuredSectionWriteDto Section(
        Guid id,
        string heading,
        string sectionType,
        int sortOrder,
        params CvStructuredEntryWriteDto[] entries) =>
        new(id, heading, sectionType, sortOrder, entries);

    private static CvStructuredEntryWriteDto Entry(Guid id, string title, int sortOrder) =>
        new(
            id,
            title,
            null,
            null,
            string.Empty,
            [],
            string.Empty,
            CvEntrySources.Manual,
            null,
            sortOrder);

    private static IReadOnlyList<CvStructuredSectionDto> ToRead(
        IReadOnlyList<CvStructuredSectionWriteDto> sections) =>
        sections
            .Select((section) => new CvStructuredSectionDto(
                section.Id ?? Guid.NewGuid(),
                section.Heading,
                section.SectionType,
                section.SortOrder,
                section.Entries
                    .Select((entry) => new CvStructuredEntryDto(
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
                        entry.SortOrder))
                    .ToArray()))
            .ToArray();
}
