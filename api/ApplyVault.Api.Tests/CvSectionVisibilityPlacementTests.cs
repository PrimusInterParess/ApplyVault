using System.Text.Json;
using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using ApplyVault.Api.Services.CvSectionCatalog;
using ApplyVault.Api.Services.HtmlExport;
using ApplyVault.Api.Services.InterviewPrep;
using ApplyVault.Api.Services.InterviewPrep.Adapters;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Tests;

public sealed class CvSectionVisibilityPlacementTests
{
    private static readonly ICvSectionCatalog Catalog = CvSectionCatalogProvider.LoadFromDefaultPath();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Normalize_ForcesContactIncluded_AndIncludesMissingSlots()
    {
        var contactId = Guid.NewGuid();
        var skillsId = Guid.NewGuid();

        var normalized = FixedSectionLayout.Normalize(
            [
                WriteSection(contactId, "Contact", CvSectionTypes.Contact, 0, "Ada", included: false),
                WriteSection(skillsId, "Skills", CvSectionTypes.Skills, 3, "C#", included: false)
            ],
            Catalog);

        Assert.True(Slot(normalized, CvSectionTypes.Contact).IncludedOnTemplate);
        Assert.False(Slot(normalized, CvSectionTypes.Skills).IncludedOnTemplate);
        Assert.All(
            normalized.Where((section) => section.Id is null),
            (section) => Assert.True(section.IncludedOnTemplate));
        Assert.True(FixedSectionLayout.IsCanonical(ToRead(normalized), Catalog));
    }

    [Fact]
    public void IsCanonical_StaysTrueWhenOnlyVisibilityChanges()
    {
        var sections = Catalog.FixedLayout
            .Select((slot, index) => new CvStructuredSectionDto(
                Guid.NewGuid(),
                slot.Heading,
                slot.SectionType,
                index,
                [],
                slot.SectionType != CvSectionTypes.Experience))
            .ToArray();

        Assert.True(FixedSectionLayout.IsCanonical(sections, Catalog));

        var healed = FixedSectionLayout.Normalize(sections, Catalog);
        Assert.False(Slot(healed, CvSectionTypes.Experience).IncludedOnTemplate);
        Assert.True(Slot(healed, CvSectionTypes.Contact).IncludedOnTemplate);
    }

    [Fact]
    public void MissingJson_AndNullWrite_MeanIncluded()
    {
        var experienceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var skillsId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var json = """
            {
              "sections": [
                {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "heading": "Experience",
                  "sectionType": "Experience",
                  "sortOrder": 2,
                  "entries": []
                },
                {
                  "id": "22222222-2222-2222-2222-222222222222",
                  "heading": "Skills",
                  "sectionType": "Skills",
                  "sortOrder": 3,
                  "entries": [],
                  "includedOnTemplate": null
                }
              ]
            }
            """;

        var request = JsonSerializer.Deserialize<SaveCvStructuredDocumentRequest>(json, JsonOptions);
        Assert.NotNull(request);
        Assert.Null(request.Sections.Single((section) => section.Id == experienceId).IncludedOnTemplate);
        Assert.Null(request.Sections.Single((section) => section.Id == skillsId).IncludedOnTemplate);

        var normalized = FixedSectionLayout.Normalize(request.Sections, Catalog);

        Assert.True(Slot(normalized, CvSectionTypes.Experience).IncludedOnTemplate);
        Assert.True(Slot(normalized, CvSectionTypes.Skills).IncludedOnTemplate);

        var explicitNull = FixedSectionLayout.Normalize(
            [WriteSection(experienceId, "Experience", CvSectionTypes.Experience, 2, "Acme", included: null)],
            Catalog);
        Assert.True(Slot(explicitNull, CvSectionTypes.Experience).IncludedOnTemplate);
    }

    [Fact]
    public void BlankAndImport_StartIncluded()
    {
        var blank = FixedSectionLayout.CreateBlank(Catalog);
        Assert.All(blank, (section) => Assert.True(section.IncludedOnTemplate));

        var (contactOnly, _) = ContactOnlyCvParser.Parse(
            ["Jane Doe", "jane@example.com"],
            Catalog);
        Assert.All(contactOnly, (section) => Assert.True(section.IncludedOnTemplate));

        var heuristic = CvStructuredImportHeuristic.Parse(
        [
            new CvPdfRawSection(
                "Profile",
                "summary",
                0,
                "Experienced software engineer focused on backend systems.")
        ]);
        Assert.All(heuristic, (section) => Assert.NotEqual(false, section.IncludedOnTemplate));

        var replacementImport = FixedSectionLayout.Normalize(
            [
                new CvStructuredSectionWriteDto(
                    null,
                    "Experience",
                    CvSectionTypes.Experience,
                    0,
                    [
                        new CvStructuredEntryWriteDto(
                            null,
                            "Engineer",
                            null,
                            null,
                            "Built services.",
                            [],
                            string.Empty,
                            CvEntrySources.Import,
                            null,
                            0)
                    ])
            ],
            Catalog);
        Assert.All(replacementImport, (section) => Assert.True(section.IncludedOnTemplate));
        Assert.All(
            FixedSectionLayout.Normalize(heuristic, Catalog),
            (section) => Assert.True(section.IncludedOnTemplate));
    }

    [Fact]
    public void MergeAssistUpdate_PreservesOmittedFlagWhenAiRewritesTheSection()
    {
        var summaryId = Guid.NewGuid();
        var experienceId = Guid.NewGuid();
        var current = new CvStructuredDocumentDto(
            Guid.NewGuid(),
            DateTimeOffset.UnixEpoch,
            [
                ReadSection(summaryId, "Summary", CvSectionTypes.Summary, 1, "Old summary", included: false),
                ReadSection(experienceId, "Experience", CvSectionTypes.Experience, 2, "Acme", included: false)
            ]);
        var aiResult = new SaveCvStructuredDocumentRequest(
        [
            WriteSection(summaryId, "Summary", CvSectionTypes.Summary, 1, "New summary", included: true)
        ]);

        var merged = CvStructuredUpdateNormalizer.MergeAssistUpdate(
            current,
            aiResult,
            focusSectionIds: null,
            Catalog);

        var summary = Slot(merged.Sections, CvSectionTypes.Summary);
        var experience = Slot(merged.Sections, CvSectionTypes.Experience);
        Assert.False(summary.IncludedOnTemplate);
        Assert.Equal("New summary", Assert.Single(summary.Entries).Summary);
        Assert.False(experience.IncludedOnTemplate);
        Assert.Equal("Acme", Assert.Single(experience.Entries).Summary);
    }

    [Fact]
    public async Task ComputeCvFingerprint_UnchangedWhenOnlyIncludedOnTemplateChanges()
    {
        var documentId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var sectionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var entryId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var importedAt = new DateTimeOffset(2026, 10, 19, 6, 0, 0, TimeSpan.Zero);
        var user = new AppUserEntity { Id = Guid.NewGuid(), SupabaseUserId = "fingerprint-user" };

        var included = await CaptureAsync(user, documentId, importedAt, sectionId, entryId, includedOnTemplate: true);
        var omitted = await CaptureAsync(user, documentId, importedAt, sectionId, entryId, includedOnTemplate: false);

        Assert.Equal(included.SnapshotJson, omitted.SnapshotJson);
        Assert.DoesNotContain("includedOnTemplate", included.SnapshotJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Kept for interview prep", included.SnapshotJson, StringComparison.Ordinal);
        Assert.Contains(Catalog.Version.ToString(), included.SnapshotJson, StringComparison.Ordinal);
        Assert.Equal(
            InterviewPrepStudyBriefService.ComputeCvFingerprint(included),
            InterviewPrepStudyBriefService.ComputeCvFingerprint(omitted));

        var edited = await CaptureAsync(
            user,
            documentId,
            importedAt,
            sectionId,
            entryId,
            includedOnTemplate: false,
            summary: "Edited entry");
        Assert.NotEqual(
            InterviewPrepStudyBriefService.ComputeCvFingerprint(omitted),
            InterviewPrepStudyBriefService.ComputeCvFingerprint(edited));
    }

    [Fact]
    public void FromStructuredDocument_DropsOmittedNonContactSections_BeforeContentFilter()
    {
        var document = new CvStructuredDocumentDto(
            Guid.NewGuid(),
            null,
            [
                ReadSection(
                    Guid.NewGuid(),
                    "Contact",
                    CvSectionTypes.Contact,
                    0,
                    "Ada Lovelace",
                    included: false,
                    title: "Name",
                    summary: string.Empty,
                    subtitle: "Ada Lovelace"),
                ReadSection(Guid.NewGuid(), "Experience", CvSectionTypes.Experience, 2, "Built things", included: true),
                ReadSection(Guid.NewGuid(), "Skills", CvSectionTypes.Skills, 3, "C#", included: false),
                new CvStructuredSectionDto(
                    Guid.NewGuid(),
                    "Education",
                    CvSectionTypes.Education,
                    4,
                    [],
                    true)
            ]);

        var request = CvExportMapping.FromStructuredDocument(document, null, null);
        var types = request.Sections.Select((section) => section.SectionType).ToArray();

        Assert.Contains(CvSectionTypes.Contact, types);
        Assert.Contains(CvSectionTypes.Experience, types);
        Assert.Contains(CvSectionTypes.Education, types);
        Assert.DoesNotContain(CvSectionTypes.Skills, types);

        var html = CvExportHtmlMapper.ApplyTemplate("{{HeaderHtml}}{{MainHtml}}", request);
        Assert.Contains("Experience", html, StringComparison.Ordinal);
        Assert.Contains("Ada Lovelace", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Skills", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Education", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveStructured_PersistsOmission_AndTreatsNullAsIncluded()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        SeedDocument(dbContext, user);
        await dbContext.SaveChangesAsync();
        var service = new CvStructuredDocumentService(dbContext, Catalog);
        var experienceId = Guid.NewGuid();

        var omitted = await service.SaveStructuredAsync(
            user,
            new SaveCvStructuredDocumentRequest(
            [
                WriteSection(experienceId, "Experience", CvSectionTypes.Experience, 2, "Acme", included: false),
                WriteSection(Guid.NewGuid(), "Contact", CvSectionTypes.Contact, 0, "Ada", included: false, title: "Name")
            ]),
            markImported: false);

        Assert.False(omitted.Sections.Single((section) => section.Id == experienceId).IncludedOnTemplate);
        Assert.True(omitted.Sections.Single((section) => section.SectionType == CvSectionTypes.Contact).IncludedOnTemplate);
        Assert.All(
            omitted.Sections.Where((section) =>
                section.SectionType is not CvSectionTypes.Experience and not CvSectionTypes.Contact),
            (section) => Assert.True(section.IncludedOnTemplate));

        var included = await service.SaveStructuredAsync(
            user,
            new SaveCvStructuredDocumentRequest(
            [
                WriteSection(experienceId, "Experience", CvSectionTypes.Experience, 2, "Acme", included: null)
            ]),
            markImported: false);

        Assert.True(included.Sections.Single((section) => section.Id == experienceId).IncludedOnTemplate);
    }

    [Fact]
    public async Task GetStructured_HealPreservesOmission()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var sectionId = Guid.NewGuid();
        var utcNow = DateTimeOffset.UtcNow;
        dbContext.Users.Add(user);
        dbContext.UserCvDocuments.Add(new UserCvDocumentEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OriginalFileName = "cv.pdf",
            ContentType = "application/pdf",
            StorageKey = "cv.pdf",
            BaseStorageKey = "cv.pdf",
            FileSizeBytes = 128,
            OriginalFileSizeBytes = 128,
            UploadedAt = utcNow,
            UpdatedAt = utcNow,
            StructuredImportedAt = utcNow,
            Sections =
            [
                new UserCvSectionEntity
                {
                    Id = sectionId,
                    UserId = user.Id,
                    Heading = "Work History",
                    SectionType = CvSectionTypes.Experience,
                    SortOrder = 0,
                    IncludedOnTemplate = false,
                    Entries =
                    [
                        new UserCvEntryEntity
                        {
                            Id = Guid.NewGuid(),
                            UserId = user.Id,
                            Title = "Engineer",
                            Summary = "Built services.",
                            BulletsJson = "[]",
                            TechStack = string.Empty,
                            Source = CvEntrySources.Manual,
                            SortOrder = 0
                        }
                    ]
                }
            ]
        });
        await dbContext.SaveChangesAsync();

        var service = new CvStructuredDocumentService(dbContext, Catalog);
        var healed = await service.GetStructuredAsync(user);

        Assert.NotNull(healed);
        var experience = healed.Sections.Single((section) => section.Id == sectionId);
        Assert.Equal("Experience", experience.Heading);
        Assert.False(experience.IncludedOnTemplate);
        Assert.True(healed.Sections.Single((section) => section.SectionType == CvSectionTypes.Summary).IncludedOnTemplate);
        Assert.True(FixedSectionLayout.IsCanonical(healed.Sections, Catalog));
    }

    private static async Task<InterviewPrepCandidateSnapshot> CaptureAsync(
        AppUserEntity user,
        Guid documentId,
        DateTimeOffset importedAt,
        Guid sectionId,
        Guid entryId,
        bool includedOnTemplate,
        string summary = "Kept for interview prep")
    {
        var document = new CvStructuredDocumentDto(
            documentId,
            importedAt,
            [
                ReadSection(
                    sectionId,
                    "Experience",
                    CvSectionTypes.Experience,
                    2,
                    summary,
                    includedOnTemplate,
                    entryId: entryId)
            ]);
        var adapter = new InterviewPrepCandidateContextAdapter(
            new FixedStructuredDocumentService(document),
            Catalog);
        return await adapter.CaptureAsync(user);
    }

    private static CvStructuredSectionWriteDto Slot(
        IReadOnlyList<CvStructuredSectionWriteDto> sections,
        string sectionType) =>
        sections.Single((section) => section.SectionType == sectionType);

    private static IReadOnlyList<CvStructuredSectionDto> ToRead(
        IReadOnlyList<CvStructuredSectionWriteDto> sections) =>
        sections
            .Select((section) => new CvStructuredSectionDto(
                section.Id ?? Guid.NewGuid(),
                section.Heading,
                section.SectionType,
                section.SortOrder,
                [],
                section.IncludedOnTemplate == true))
            .ToArray();

    private static CvStructuredSectionDto ReadSection(
        Guid id,
        string heading,
        string sectionType,
        int sortOrder,
        string marker,
        bool included,
        string? title = null,
        string? summary = null,
        string? subtitle = null,
        Guid? entryId = null) =>
        new(
            id,
            heading,
            sectionType,
            sortOrder,
            [
                new CvStructuredEntryDto(
                    entryId ?? Guid.NewGuid(),
                    title ?? marker,
                    subtitle,
                    null,
                    summary ?? marker,
                    [],
                    string.Empty,
                    new Dictionary<string, object?>(),
                    CvEntrySources.Manual,
                    null,
                    0)
            ],
            included);

    private static CvStructuredSectionWriteDto WriteSection(
        Guid id,
        string heading,
        string sectionType,
        int sortOrder,
        string marker,
        bool? included,
        string? title = null) =>
        new(
            id,
            heading,
            sectionType,
            sortOrder,
            [
                new CvStructuredEntryWriteDto(
                    Guid.NewGuid(),
                    title ?? marker,
                    null,
                    null,
                    marker,
                    [],
                    string.Empty,
                    CvEntrySources.Manual,
                    null,
                    0)
            ],
            included);

    private static void SeedDocument(ApplyVaultDbContext dbContext, AppUserEntity user)
    {
        var utcNow = DateTimeOffset.UtcNow;
        dbContext.Users.Add(user);
        dbContext.UserCvDocuments.Add(new UserCvDocumentEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            OriginalFileName = "cv.pdf",
            ContentType = "application/pdf",
            StorageKey = "cv.pdf",
            BaseStorageKey = "cv.pdf",
            FileSizeBytes = 128,
            OriginalFileSizeBytes = 128,
            UploadedAt = utcNow,
            UpdatedAt = utcNow,
            StructuredImportedAt = utcNow
        });
    }

    private static ApplyVaultDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplyVaultDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplyVaultDbContext(options);
    }

    private static AppUserEntity CreateUser()
    {
        var utcNow = DateTimeOffset.UtcNow;
        return new AppUserEntity
        {
            Id = Guid.NewGuid(),
            SupabaseUserId = Guid.NewGuid().ToString("N"),
            Email = "user@example.com",
            DisplayName = "Test User",
            CreatedAt = utcNow,
            LastSeenAt = utcNow
        };
    }

    private sealed class FixedStructuredDocumentService(CvStructuredDocumentDto document) : ICvStructuredDocumentService
    {
        public Task<CvStructuredDocumentDto?> GetStructuredAsync(
            AppUserEntity user,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CvStructuredDocumentDto?>(document);

        public Task<CvStructuredDocumentDto> SaveStructuredAsync(
            AppUserEntity user,
            SaveCvStructuredDocumentRequest request,
            bool markImported,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
