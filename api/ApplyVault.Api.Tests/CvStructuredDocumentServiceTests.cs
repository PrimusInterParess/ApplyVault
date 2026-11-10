using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using ApplyVault.Api.Services.CvSectionCatalog;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Tests;

public sealed class CvStructuredDocumentServiceTests
{
    [Fact]
    public async Task SaveStructuredAsync_ReplacesSectionsWhilePreservingIds()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var sectionId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
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
            FileSizeBytes = 1024,
            OriginalFileSizeBytes = 1024,
            UploadedAt = utcNow,
            UpdatedAt = utcNow,
            StructuredImportedAt = utcNow,
            Sections =
            [
                new UserCvSectionEntity
                {
                    Id = sectionId,
                    UserId = user.Id,
                    Heading = "Experience",
                    SectionType = CvSectionTypes.Experience,
                    SortOrder = 0,
                    Entries =
                    [
                        new UserCvEntryEntity
                        {
                            Id = entryId,
                            UserId = user.Id,
                            Title = "Software Engineer",
                            Summary = "Built reliable services.",
                            BulletsJson = "[]",
                            TechStack = string.Empty,
                            Source = CvEntrySources.Import,
                            SortOrder = 0
                        }
                    ]
                }
            ]
        });
        await dbContext.SaveChangesAsync();

        var service = new CvStructuredDocumentService(dbContext, CvSectionCatalogProvider.LoadFromDefaultPath());
        var saved = await service.SaveStructuredAsync(
            user,
            new SaveCvStructuredDocumentRequest(
            [
                new CvStructuredSectionWriteDto(
                    sectionId,
                    "Experience",
                    CvSectionTypes.Experience,
                    0,
                    [
                        new CvStructuredEntryWriteDto(
                            entryId,
                            "Senior Software Engineer",
                            null,
                            null,
                            "Built reliable services.",
                            [],
                            string.Empty,
                            CvEntrySources.Manual,
                            null,
                            0)
                    ])
            ]),
            markImported: false);

        var updatedEntry = saved.Sections.Single((section) => section.Id == sectionId)
            .Entries.Single((entry) => entry.Id == entryId);

        Assert.Equal("Senior Software Engineer", updatedEntry.Title);
        Assert.Equal(CvEntrySources.Manual, updatedEntry.Source);
        Assert.Equal(7, saved.Sections.Count);
        Assert.True(FixedSectionLayout.IsCanonical(saved.Sections, CvSectionCatalogProvider.LoadFromDefaultPath()));
    }

    [Fact]
    public async Task SaveStructuredAsync_NormalizesUnknownAndMissingSlots()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var customId = Guid.NewGuid();
        SeedDocument(dbContext, user);
        await dbContext.SaveChangesAsync();

        var service = new CvStructuredDocumentService(dbContext, CvSectionCatalogProvider.LoadFromDefaultPath());
        var saved = await service.SaveStructuredAsync(
            user,
            new SaveCvStructuredDocumentRequest(
            [
                new CvStructuredSectionWriteDto(
                    customId,
                    "Volunteer",
                    "NotAType",
                    4,
                    [
                        new CvStructuredEntryWriteDto(
                            Guid.NewGuid(),
                            "Kitchen",
                            null,
                            null,
                            string.Empty,
                            [],
                            string.Empty,
                            CvEntrySources.Manual,
                            null,
                            0)
                    ])
            ]),
            markImported: false);

        Assert.Equal(7, saved.Sections.Count);
        var custom = saved.Sections.Single((section) => section.SectionType == CvSectionTypes.Custom);
        Assert.Equal(customId, custom.Id);
        Assert.Equal("Certifications & Languages", custom.Heading);
        Assert.Equal("Kitchen", Assert.Single(custom.Entries).Title);
        Assert.True(FixedSectionLayout.IsCanonical(saved.Sections, CvSectionCatalogProvider.LoadFromDefaultPath()));
    }

    [Fact]
    public async Task GetStructuredAsync_HealsNonCanonicalSectionsWithoutRecursing()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        var utcNow = DateTimeOffset.UtcNow;
        var sectionId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        dbContext.Users.Add(user);
        dbContext.UserCvDocuments.Add(new UserCvDocumentEntity
        {
            Id = documentId,
            UserId = user.Id,
            OriginalFileName = "cv.pdf",
            ContentType = "application/pdf",
            StorageKey = "cv.pdf",
            BaseStorageKey = "cv.pdf",
            FileSizeBytes = 1024,
            OriginalFileSizeBytes = 1024,
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
                    Entries =
                    [
                        new UserCvEntryEntity
                        {
                            Id = entryId,
                            UserId = user.Id,
                            Title = "Software Engineer",
                            Summary = "Built reliable services.",
                            BulletsJson = "[]",
                            TechStack = string.Empty,
                            Source = CvEntrySources.Import,
                            SortOrder = 0
                        }
                    ]
                }
            ]
        });
        await dbContext.SaveChangesAsync();

        var service = new CvStructuredDocumentService(dbContext, CvSectionCatalogProvider.LoadFromDefaultPath());
        var healed = await service.GetStructuredAsync(user);

        Assert.NotNull(healed);
        Assert.Equal(7, healed.Sections.Count);
        Assert.True(FixedSectionLayout.IsCanonical(healed.Sections, CvSectionCatalogProvider.LoadFromDefaultPath()));

        var experience = healed.Sections.Single((section) => section.Id == sectionId);
        Assert.Equal("Experience", experience.Heading);
        Assert.Equal("Software Engineer", experience.Entries.Single((entry) => entry.Id == entryId).Title);

        var again = await service.GetStructuredAsync(user);
        Assert.Equal(healed.Sections.Select((section) => section.Id), again!.Sections.Select((section) => section.Id));
    }

    [Fact]
    public async Task GetStructuredAsync_DoesNotInventSlotsWhenDocumentHasNoSections()
    {
        await using var dbContext = CreateDbContext();
        var user = CreateUser();
        SeedDocument(dbContext, user);
        await dbContext.SaveChangesAsync();

        var service = new CvStructuredDocumentService(dbContext, CvSectionCatalogProvider.LoadFromDefaultPath());
        var structured = await service.GetStructuredAsync(user);

        Assert.NotNull(structured);
        Assert.Empty(structured.Sections);
    }

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
            FileSizeBytes = 1024,
            OriginalFileSizeBytes = 1024,
            UploadedAt = utcNow,
            UpdatedAt = utcNow
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
}
