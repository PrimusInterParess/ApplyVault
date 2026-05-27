using ApplyVault.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Data;

public sealed class ApplyVaultDbContext(DbContextOptions<ApplyVaultDbContext> options) : DbContext(options)
{
    public DbSet<AppUserEntity> Users => Set<AppUserEntity>();
    public DbSet<ConnectedAccountEntity> ConnectedAccounts => Set<ConnectedAccountEntity>();
    public DbSet<ScrapeResultEntity> ScrapeResults => Set<ScrapeResultEntity>();
    public DbSet<ScrapeResultContactEntity> ScrapeResultContacts => Set<ScrapeResultContactEntity>();
    public DbSet<InterviewEventEntity> InterviewEvents => Set<InterviewEventEntity>();
    public DbSet<CalendarEventLinkEntity> CalendarEventLinks => Set<CalendarEventLinkEntity>();
    public DbSet<UserCvProjectSummaryEntity> UserCvProjectSummaries => Set<UserCvProjectSummaryEntity>();
    public DbSet<UserCvDocumentEntity> UserCvDocuments => Set<UserCvDocumentEntity>();
    public DbSet<UserCvSectionEntity> UserCvSections => Set<UserCvSectionEntity>();
    public DbSet<UserCvEntryEntity> UserCvEntries => Set<UserCvEntryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUserEntity>((entity) =>
        {
            entity.HasKey((user) => user.Id);
            entity.Property((user) => user.SupabaseUserId).IsRequired();
            entity.HasIndex((user) => user.SupabaseUserId).IsUnique();
            entity.HasIndex((user) => user.Email);
        });

        modelBuilder.Entity<ScrapeResultEntity>((entity) =>
        {
            entity.HasKey((result) => result.Id);
            entity.Property((result) => result.IsRejected).HasDefaultValue(false);
            entity.Property((result) => result.LastStatusSource).HasMaxLength(32);
            entity.Property((result) => result.LastStatusKind).HasMaxLength(32);
            entity.Property((result) => result.LastStatusEmailFrom).HasMaxLength(320);
            entity.Property((result) => result.LastStatusEmailSubject).HasMaxLength(512);
            entity.Property((result) => result.InterviewDate).HasColumnType("date");
            entity.Property((result) => result.IsDeleted).HasDefaultValue(false);
            entity.Property((result) => result.Title).IsRequired();
            entity.Property((result) => result.Url).IsRequired();
            entity.Property((result) => result.Text).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property((result) => result.ExtractedAt).IsRequired();
            entity.Property((result) => result.SourceHostname).IsRequired();
            entity.Property((result) => result.DetectedPageType).IsRequired();
            entity.Property((result) => result.JobTitleConfidence).HasDefaultValue(0d);
            entity.Property((result) => result.CompanyNameConfidence).HasDefaultValue(0d);
            entity.Property((result) => result.LocationConfidence).HasDefaultValue(0d);
            entity.Property((result) => result.JobDescriptionConfidence).HasDefaultValue(0d);
            entity.Property((result) => result.CaptureOverallConfidence).HasDefaultValue(0d);
            entity.Property((result) => result.CaptureReviewStatus)
                .IsRequired()
                .HasDefaultValue(CaptureReviewStatuses.NotRequired);
            entity.Property((result) => result.UserId).IsRequired();
            entity.HasIndex((result) => result.UserId);
            entity.HasOne((result) => result.User)
                .WithMany((user) => user.ScrapeResults)
                .HasForeignKey((result) => result.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasMany((result) => result.HiringManagerContacts)
                .WithOne()
                .HasForeignKey((contact) => contact.ScrapeResultId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne((result) => result.InterviewEvent)
                .WithOne((interviewEvent) => interviewEvent.ScrapeResult)
                .HasForeignKey<InterviewEventEntity>((interviewEvent) => interviewEvent.ScrapeResultId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany((result) => result.CalendarEventLinks)
                .WithOne((link) => link.ScrapeResult)
                .HasForeignKey((link) => link.ScrapeResultId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScrapeResultContactEntity>((entity) =>
        {
            entity.HasKey((contact) => contact.Id);
            entity.Property((contact) => contact.Type).IsRequired();
            entity.Property((contact) => contact.Value).IsRequired();
        });

        modelBuilder.Entity<ConnectedAccountEntity>((entity) =>
        {
            entity.HasKey((account) => account.Id);
            entity.Property((account) => account.Provider).IsRequired();
            entity.Property((account) => account.ProviderUserId).IsRequired();
            entity.Property((account) => account.AccessToken).IsRequired();
            entity.Property((account) => account.SyncStatus).HasMaxLength(32);
            entity.Property((account) => account.LastSyncError).HasMaxLength(1024);
            entity.Property((account) => account.LastHistoryId).HasMaxLength(128);
            entity.HasIndex((account) => new { account.UserId, account.Provider, account.ProviderUserId }).IsUnique();
            entity.HasOne((account) => account.User)
                .WithMany((user) => user.ConnectedAccounts)
                .HasForeignKey((account) => account.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany((account) => account.CalendarEventLinks)
                .WithOne((link) => link.ConnectedAccount)
                .HasForeignKey((link) => link.ConnectedAccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InterviewEventEntity>((entity) =>
        {
            entity.HasKey((interviewEvent) => interviewEvent.ScrapeResultId);
            entity.Property((interviewEvent) => interviewEvent.TimeZone).IsRequired();
        });

        modelBuilder.Entity<CalendarEventLinkEntity>((entity) =>
        {
            entity.HasKey((link) => link.Id);
            entity.Property((link) => link.Provider).IsRequired();
            entity.Property((link) => link.ExternalEventId).IsRequired();
            entity.HasIndex((link) => new { link.ConnectedAccountId, link.ExternalEventId }).IsUnique();
            entity.HasIndex((link) => new { link.ScrapeResultId, link.ConnectedAccountId }).IsUnique();
        });

        modelBuilder.Entity<UserCvProjectSummaryEntity>((entity) =>
        {
            entity.HasKey((summary) => summary.Id);
            entity.Property((summary) => summary.FullName).IsRequired().HasMaxLength(512);
            entity.Property((summary) => summary.HtmlUrl).IsRequired().HasMaxLength(512);
            entity.Property((summary) => summary.PrimaryLanguage).HasMaxLength(128);
            entity.Property((summary) => summary.Topics).HasColumnType("nvarchar(max)");
            entity.Property((summary) => summary.CvTitle).IsRequired().HasMaxLength(256);
            entity.Property((summary) => summary.CvSummary).IsRequired().HasColumnType("nvarchar(max)");
            entity.Property((summary) => summary.CvBullets).IsRequired().HasColumnType("nvarchar(max)");
            entity.Property((summary) => summary.TechStack).IsRequired().HasMaxLength(512);
            entity.HasIndex((summary) => new { summary.UserId, summary.ExternalRepoId }).IsUnique();
            entity.HasOne((summary) => summary.User)
                .WithMany((user) => user.CvProjectSummaries)
                .HasForeignKey((summary) => summary.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserCvDocumentEntity>((entity) =>
        {
            entity.HasKey((document) => document.Id);
            entity.Property((document) => document.OriginalFileName).IsRequired().HasMaxLength(260);
            entity.Property((document) => document.ContentType).IsRequired().HasMaxLength(128);
            entity.Property((document) => document.StorageKey).IsRequired().HasMaxLength(512);
            entity.Property((document) => document.BaseStorageKey).HasMaxLength(512);
            entity.HasIndex((document) => document.UserId).IsUnique();
            entity.HasOne((document) => document.User)
                .WithOne((user) => user.CvDocument)
                .HasForeignKey<UserCvDocumentEntity>((document) => document.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany((document) => document.Sections)
                .WithOne((section) => section.Document)
                .HasForeignKey((section) => section.UserCvDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserCvSectionEntity>((entity) =>
        {
            entity.HasKey((section) => section.Id);
            entity.Property((section) => section.Heading).IsRequired().HasMaxLength(256);
            entity.Property((section) => section.SectionType).IsRequired().HasMaxLength(32);
            entity.HasIndex((section) => new { section.UserCvDocumentId, section.SortOrder });
            entity.HasOne((section) => section.Document)
                .WithMany((document) => document.Sections)
                .HasForeignKey((section) => section.UserCvDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany((section) => section.Entries)
                .WithOne((entry) => entry.Section)
                .HasForeignKey((entry) => entry.SectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserCvEntryEntity>((entity) =>
        {
            entity.HasKey((entry) => entry.Id);
            entity.Property((entry) => entry.Title).IsRequired().HasMaxLength(256);
            entity.Property((entry) => entry.Subtitle).HasMaxLength(512);
            entity.Property((entry) => entry.DateRange).HasMaxLength(128);
            entity.Property((entry) => entry.Summary).IsRequired().HasColumnType("nvarchar(max)");
            entity.Property((entry) => entry.BulletsJson).IsRequired().HasColumnType("nvarchar(max)");
            entity.Property((entry) => entry.TechStack).IsRequired().HasMaxLength(512);
            entity.Property((entry) => entry.Source).IsRequired().HasMaxLength(32);
            entity.HasIndex((entry) => new { entry.SectionId, entry.SortOrder });
            entity.HasOne((entry) => entry.SourceSummary)
                .WithMany()
                .HasForeignKey((entry) => entry.SourceSummaryId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
