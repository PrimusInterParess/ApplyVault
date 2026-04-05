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
            entity.HasIndex((result) => result.UserId);
            entity.HasOne((result) => result.User)
                .WithMany((user) => user.ScrapeResults)
                .HasForeignKey((result) => result.UserId)
                .OnDelete(DeleteBehavior.SetNull);
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
    }
}
