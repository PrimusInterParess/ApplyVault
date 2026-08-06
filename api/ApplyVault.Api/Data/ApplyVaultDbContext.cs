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
    public DbSet<InterviewPrepSessionEntity> InterviewPrepSessions => Set<InterviewPrepSessionEntity>();
    public DbSet<InterviewPrepStageEntity> InterviewPrepStages => Set<InterviewPrepStageEntity>();
    public DbSet<InterviewPrepTurnEntity> InterviewPrepTurns => Set<InterviewPrepTurnEntity>();
    public DbSet<InterviewPrepEvidenceItemEntity> InterviewPrepEvidenceItems => Set<InterviewPrepEvidenceItemEntity>();
    public DbSet<InterviewPrepCompetencyCoverageEntity> InterviewPrepCompetencyCoverages => Set<InterviewPrepCompetencyCoverageEntity>();
    public DbSet<InterviewPrepQuestionAttemptEntity> InterviewPrepQuestionAttempts => Set<InterviewPrepQuestionAttemptEntity>();
    public DbSet<InterviewPrepAnswerRetryEntity> InterviewPrepAnswerRetries => Set<InterviewPrepAnswerRetryEntity>();
    public DbSet<InterviewPrepStudyBriefEntity> InterviewPrepStudyBriefs => Set<InterviewPrepStudyBriefEntity>();

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
            entity.Property((document) => document.ProfilePhotoStorageKey).HasMaxLength(512);
            entity.Property((document) => document.ProfilePhotoContentType).HasMaxLength(128);
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
            entity.Property((entry) => entry.FieldsJson).HasColumnType("nvarchar(max)");
            entity.Property((entry) => entry.Source).IsRequired().HasMaxLength(32);
            entity.HasIndex((entry) => new { entry.SectionId, entry.SortOrder });
            entity.HasOne((entry) => entry.SourceSummary)
                .WithMany()
                .HasForeignKey((entry) => entry.SourceSummaryId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<InterviewPrepSessionEntity>((entity) =>
        {
            entity.ToTable("InterviewPrepSessions");
            entity.HasKey((session) => session.Id);
            entity.Property((session) => session.Id).ValueGeneratedNever();
            entity.Property((session) => session.Mode).IsRequired().HasMaxLength(64);
            entity.Property((session) => session.Persona).IsRequired().HasMaxLength(64);
            entity.Property((session) => session.Language).IsRequired().HasMaxLength(32);
            entity.Property((session) => session.Market).IsRequired().HasMaxLength(32);
            entity.Property((session) => session.ExperienceType).IsRequired().HasMaxLength(64);
            entity.Property((session) => session.InteractionType).IsRequired().HasMaxLength(32);
            entity.Property((session) => session.Status).IsRequired().HasMaxLength(32);
            entity.Property((session) => session.CvSnapshotJson).HasColumnType("nvarchar(max)");
            entity.Property((session) => session.JobSnapshotJson).HasColumnType("nvarchar(max)");
            entity.Property((session) => session.BriefJson).HasColumnType("nvarchar(max)");
            entity.Property((session) => session.PlanJson).HasColumnType("nvarchar(max)");
            entity.Property((session) => session.ConversationSummary).HasColumnType("nvarchar(max)");
            entity.Property((session) => session.RuntimeStateJson).HasColumnType("nvarchar(max)");
            entity.Property((session) => session.CandidateReportJson).HasColumnType("nvarchar(max)");
            entity.Property((session) => session.StageAssessmentsJson).HasColumnType("nvarchar(max)");
            entity.Property((session) => session.PanelDebriefJson).HasColumnType("nvarchar(max)");
            entity.Property((session) => session.JobTitle).HasMaxLength(512);
            entity.Property((session) => session.CompanyName).HasMaxLength(512);
            entity.Property((session) => session.CatalogVersion).HasMaxLength(32);
            entity.Property((session) => session.IdempotencyKey).HasMaxLength(64);
            entity.Property((session) => session.FailureReason).HasMaxLength(1024);
            entity.Property((session) => session.ConcurrencyStamp).IsRequired();
            entity.HasIndex((session) => new { session.UserId, session.UpdatedAt })
                .IsDescending(false, true);
            entity.HasIndex((session) => new { session.UserId, session.IdempotencyKey })
                .IsUnique()
                .HasFilter("[IdempotencyKey] IS NOT NULL");
            entity.HasOne<AppUserEntity>()
                .WithMany()
                .HasForeignKey((session) => session.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ScrapeResultEntity>()
                .WithMany()
                .HasForeignKey((session) => session.ScrapeResultId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany((session) => session.Stages)
                .WithOne((stage) => stage.Session)
                .HasForeignKey((stage) => stage.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany((session) => session.Turns)
                .WithOne((turn) => turn.Session)
                .HasForeignKey((turn) => turn.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany((session) => session.EvidenceItems)
                .WithOne((item) => item.Session)
                .HasForeignKey((item) => item.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany((session) => session.CompetencyCoverages)
                .WithOne((coverage) => coverage.Session)
                .HasForeignKey((coverage) => coverage.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany((session) => session.QuestionAttempts)
                .WithOne((attempt) => attempt.Session)
                .HasForeignKey((attempt) => attempt.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany((session) => session.AnswerRetries)
                .WithOne((retry) => retry.Session)
                .HasForeignKey((retry) => retry.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InterviewPrepAnswerRetryEntity>((entity) =>
        {
            entity.ToTable("InterviewPrepAnswerRetries");
            entity.HasKey((retry) => retry.Id);
            entity.Property((retry) => retry.Id).ValueGeneratedNever();
            entity.Property((retry) => retry.OriginalAnswerText).IsRequired().HasColumnType("nvarchar(max)");
            entity.Property((retry) => retry.OriginalAssessmentJson).HasColumnType("nvarchar(max)");
            entity.Property((retry) => retry.CoachingFeedbackJson).HasColumnType("nvarchar(max)");
            entity.Property((retry) => retry.RevisedAnswerText).HasColumnType("nvarchar(max)");
            entity.Property((retry) => retry.RevisedAssessmentJson).HasColumnType("nvarchar(max)");
            entity.Property((retry) => retry.ComparisonJson).HasColumnType("nvarchar(max)");
            entity.Property((retry) => retry.Status).IsRequired().HasMaxLength(32);
            entity.HasIndex((retry) => retry.CandidateTurnId).IsUnique();
        });

        modelBuilder.Entity<InterviewPrepStageEntity>((entity) =>
        {
            entity.ToTable("InterviewPrepStages");
            entity.HasKey((stage) => stage.Id);
            entity.Property((stage) => stage.Id).ValueGeneratedNever();
            entity.Property((stage) => stage.StageType).IsRequired().HasMaxLength(64);
            entity.Property((stage) => stage.Status).IsRequired().HasMaxLength(32);
            entity.Property((stage) => stage.PlanJson).HasColumnType("nvarchar(max)");
            entity.HasIndex((stage) => new { stage.SessionId, stage.SortOrder }).IsUnique();
            entity.HasMany((stage) => stage.Turns)
                .WithOne((turn) => turn.Stage)
                .HasForeignKey((turn) => turn.StageId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InterviewPrepTurnEntity>((entity) =>
        {
            entity.ToTable("InterviewPrepTurns");
            entity.HasKey((turn) => turn.Id);
            entity.Property((turn) => turn.Id).ValueGeneratedNever();
            entity.Property((turn) => turn.Role).IsRequired().HasMaxLength(16);
            entity.Property((turn) => turn.Text).IsRequired().HasColumnType("nvarchar(max)");
            entity.Property((turn) => turn.QuestionSignature).HasMaxLength(128);
            entity.Property((turn) => turn.CompetencyTag).HasMaxLength(64);
            entity.Property((turn) => turn.IntentId).HasMaxLength(128);
            entity.Property((turn) => turn.ActionType).HasMaxLength(64);
            entity.Property((turn) => turn.TargetEvidenceKey).HasMaxLength(128);
            entity.Property((turn) => turn.ClientTurnId).HasMaxLength(64);
            entity.Property((turn) => turn.Language).HasMaxLength(32);
            entity.HasIndex((turn) => new { turn.SessionId, turn.Sequence }).IsUnique();
            entity.HasIndex((turn) => new { turn.SessionId, turn.ClientTurnId })
                .IsUnique()
                .HasFilter("[ClientTurnId] IS NOT NULL");
            entity.HasIndex((turn) => new { turn.SessionId, turn.QuestionSignature });
        });

        modelBuilder.Entity<InterviewPrepEvidenceItemEntity>((entity) =>
        {
            entity.ToTable("InterviewPrepEvidenceItems");
            entity.HasKey((item) => item.Id);
            entity.Property((item) => item.Id).ValueGeneratedNever();
            entity.Property((item) => item.CompetencyId).IsRequired().HasMaxLength(64);
            entity.Property((item) => item.Classification).IsRequired().HasMaxLength(32);
            entity.Property((item) => item.Strength).IsRequired().HasMaxLength(32);
            entity.Property((item) => item.Confidence).IsRequired().HasMaxLength(32);
            entity.Property((item) => item.Claim).IsRequired().HasMaxLength(1024);
            entity.Property((item) => item.EvidenceQuote).IsRequired().HasColumnType("nvarchar(max)");
            entity.Property((item) => item.Polarity).IsRequired().HasMaxLength(32);
            entity.HasIndex((item) => new { item.SessionId, item.CompetencyId });
        });

        modelBuilder.Entity<InterviewPrepCompetencyCoverageEntity>((entity) =>
        {
            entity.ToTable("InterviewPrepCompetencyCoverages");
            entity.HasKey((coverage) => coverage.Id);
            entity.Property((coverage) => coverage.Id).ValueGeneratedNever();
            entity.Property((coverage) => coverage.CompetencyId).IsRequired().HasMaxLength(64);
            entity.Property((coverage) => coverage.CoverageState).IsRequired().HasMaxLength(32);
            entity.Property((coverage) => coverage.LastProgressClass).HasMaxLength(32);
            entity.HasIndex((coverage) => new { coverage.SessionId, coverage.CompetencyId }).IsUnique();
        });

        modelBuilder.Entity<InterviewPrepQuestionAttemptEntity>((entity) =>
        {
            entity.ToTable("InterviewPrepQuestionAttempts");
            entity.HasKey((attempt) => attempt.Id);
            entity.Property((attempt) => attempt.Id).ValueGeneratedNever();
            entity.Property((attempt) => attempt.IntentId).HasMaxLength(128);
            entity.Property((attempt) => attempt.CompetencyId).HasMaxLength(64);
            entity.Property((attempt) => attempt.TargetEvidenceKey).HasMaxLength(128);
            entity.Property((attempt) => attempt.ProgressClass).HasMaxLength(32);
            entity.Property((attempt) => attempt.AssessmentJson).HasColumnType("nvarchar(max)");
            entity.Property((attempt) => attempt.AssessmentStatus).IsRequired().HasMaxLength(32);
            entity.HasIndex((attempt) => new { attempt.SessionId, attempt.CandidateTurnId });
        });

        modelBuilder.Entity<InterviewPrepStudyBriefEntity>((entity) =>
        {
            entity.ToTable("InterviewPrepStudyBriefs");
            entity.HasKey((brief) => brief.Id);
            entity.Property((brief) => brief.Id).ValueGeneratedNever();
            entity.Property((brief) => brief.Language).IsRequired().HasMaxLength(32);
            entity.Property((brief) => brief.Market).IsRequired().HasMaxLength(32);
            entity.Property((brief) => brief.FocusNoteSnapshot).HasMaxLength(2000);
            entity.Property((brief) => brief.BodyJson).IsRequired().HasColumnType("nvarchar(max)");
            entity.Property((brief) => brief.CvFingerprint).IsRequired().HasMaxLength(128);
            entity.Property((brief) => brief.JobTitle).HasMaxLength(512);
            entity.Property((brief) => brief.CompanyName).HasMaxLength(512);
            entity.HasIndex((brief) => new { brief.UserId, brief.ScrapeResultId })
                .IsUnique()
                .HasFilter("[ScrapeResultId] IS NOT NULL");
            entity.HasIndex((brief) => brief.UserId)
                .IsUnique()
                .HasFilter("[ScrapeResultId] IS NULL")
                .HasDatabaseName("IX_InterviewPrepStudyBriefs_UserId_CvOnly");
            entity.HasIndex((brief) => new { brief.UserId, brief.GeneratedAt })
                .IsDescending(false, true);
            entity.HasOne<AppUserEntity>()
                .WithMany()
                .HasForeignKey((brief) => brief.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ScrapeResultEntity>()
                .WithMany()
                .HasForeignKey((brief) => brief.ScrapeResultId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
