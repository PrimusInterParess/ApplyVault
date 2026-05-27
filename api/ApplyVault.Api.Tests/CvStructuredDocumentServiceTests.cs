using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
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

        var service = new CvStructuredDocumentService(dbContext);
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
