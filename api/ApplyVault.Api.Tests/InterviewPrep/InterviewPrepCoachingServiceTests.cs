using ApplyVault.Api.Data;
using ApplyVault.Api.Infrastructure;
using ApplyVault.Api.Models;
using ApplyVault.Api.Models.InterviewPrep;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services;
using ApplyVault.Api.Services.CvSectionCatalog;
using ApplyVault.Api.Services.InterviewPrep;
using ApplyVault.Api.Services.InterviewPrep.Adapters;
using ApplyVault.Api.Services.InterviewPrep.Ai;
using ApplyVault.Api.Services.InterviewPrep.Ai.Prompts;
using ApplyVault.Api.Services.InterviewPrep.Catalogs;
using ApplyVault.Api.Services.InterviewPrep.Coaching;
using ApplyVault.Api.Services.InterviewPrep.Domain;
using ApplyVault.Api.Services.InterviewPrep.FullLoop;
using ApplyVault.Api.Services.InterviewPrep.Planning;
using ApplyVault.Api.Services.InterviewPrep.Reporting;
using ApplyVault.Api.Services.InterviewPrep.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApplyVault.Api.Tests.InterviewPrep;

public sealed class InterviewPrepCoachingServiceTests
{
    [Fact]
    public async Task GuidedCoaching_review_and_retry_preserves_original_turn()
    {
        await using var db = CreateDbContext();
        var user = await InterviewPrepSessionServiceTestsHelpers.SeedUserWithCvAsync(db);
        var sessionService = InterviewPrepSessionServiceTestsHelpers.CreateSessionService(db);
        var coaching = InterviewPrepSessionServiceTestsHelpers.CreateCoachingService(db);

        var created = await sessionService.CreateAsync(
            user,
            GuidedCoachingCreate());
        await sessionService.PrepareAsync(user, created.Id, ifMatch: null);
        await sessionService.StartAsync(user, created.Id, ifMatch: null);

        var submit = await sessionService.SubmitTurnAsync(
            user,
            created.Id,
            new InterviewPrepSubmitTurnRequest(
                "coach-1",
                "Short answer without much detail."),
            ifMatch: null);

        var originalText = submit.CandidateTurn.Text;
        var review = await coaching.RequestReviewAsync(user, created.Id, submit.CandidateTurn.Id);
        Assert.Equal("reviewed", review.Status);
        Assert.Equal(originalText, review.OriginalAnswerText);
        Assert.NotEmpty(review.CoachingTips);

        var longRetry =
            "In Q3 I led a cross-team migration that cut latency by thirty percent. I owned the roadmap, ran weekly stakeholder reviews, and measured success with p95 SLO dashboards over two quarters.";
        var retry = await coaching.SubmitRetryAsync(
            user,
            created.Id,
            submit.CandidateTurn.Id,
            new InterviewPrepSubmitAnswerRetryRequest(longRetry));

        Assert.Equal("compared", retry.Status);
        Assert.True(retry.Improved);
        Assert.Equal(originalText, retry.OriginalAnswerText);
        Assert.Equal(longRetry, retry.RevisedAnswerText);

        var turnAfter = await db.InterviewPrepTurns.AsNoTracking()
            .FirstAsync((turn) => turn.Id == submit.CandidateTurn.Id);
        Assert.Equal(originalText, turnAfter.Text);
        Assert.DoesNotContain("AssessmentJson", System.Text.Json.JsonSerializer.Serialize(review));
    }

    [Fact]
    public async Task RealisticSimulation_rejects_live_coaching()
    {
        await using var db = CreateDbContext();
        var user = await InterviewPrepSessionServiceTestsHelpers.SeedUserWithCvAsync(db);
        var sessionService = InterviewPrepSessionServiceTestsHelpers.CreateSessionService(db);
        var coaching = InterviewPrepSessionServiceTestsHelpers.CreateCoachingService(db);

        var created = await sessionService.CreateAsync(user, SimulationCreate());
        await sessionService.PrepareAsync(user, created.Id, ifMatch: null);
        await sessionService.StartAsync(user, created.Id, ifMatch: null);

        var submit = await sessionService.SubmitTurnAsync(
            user,
            created.Id,
            new InterviewPrepSubmitTurnRequest("sim-1", "Answer with measurable outcomes and ownership."),
            ifMatch: null);

        var ex = await Assert.ThrowsAsync<InterviewPrepConflictException>(() =>
            coaching.RequestReviewAsync(user, created.Id, submit.CandidateTurn.Id));
        Assert.Equal("interview_prep_coaching_not_allowed_during_simulation", ex.ErrorCode);
    }

    [Fact]
    public async Task RealisticSimulation_allows_post_session_review()
    {
        await using var db = CreateDbContext();
        var user = await InterviewPrepSessionServiceTestsHelpers.SeedUserWithCvAsync(db);
        var sessionService = InterviewPrepSessionServiceTestsHelpers.CreateSessionService(db);
        var coaching = InterviewPrepSessionServiceTestsHelpers.CreateCoachingService(db);

        var created = await sessionService.CreateAsync(user, SimulationCreate());
        await sessionService.PrepareAsync(user, created.Id, ifMatch: null);
        await sessionService.StartAsync(user, created.Id, ifMatch: null);

        var submit = await sessionService.SubmitTurnAsync(
            user,
            created.Id,
            new InterviewPrepSubmitTurnRequest("sim-post-1", "Answer with measurable outcomes and ownership."),
            ifMatch: null);

        await sessionService.CompleteAsync(user, created.Id, ifMatch: null);

        var review = await coaching.RequestReviewAsync(user, created.Id, submit.CandidateTurn.Id);
        Assert.Equal("reviewed", review.Status);
    }

    private static InterviewPrepCreateSessionRequest GuidedCoachingCreate() =>
        new(
            InterviewPrepMode.ScreeningAndMotivation,
            InterviewPrepPersona.Recruiter,
            InterviewPrepLanguage.English,
            InterviewPrepMarket.General,
            InterviewPrepExperienceType.GuidedCoaching,
            InterviewPrepInteractionType.Text);

    private static InterviewPrepCreateSessionRequest SimulationCreate() =>
        new(
            InterviewPrepMode.ScreeningAndMotivation,
            InterviewPrepPersona.Recruiter,
            InterviewPrepLanguage.English,
            InterviewPrepMarket.General,
            InterviewPrepExperienceType.RealisticSimulation,
            InterviewPrepInteractionType.Text);

    private static ApplyVaultDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplyVaultDbContext>()
            .UseInMemoryDatabase($"interview-prep-coaching-{Guid.NewGuid():N}")
            .Options;
        return new ApplyVaultDbContext(options);
    }
}

internal static class InterviewPrepSessionServiceTestsHelpers
{
    internal static InterviewPrepCoachingService CreateCoachingService(ApplyVaultDbContext db)
    {
        var fake = new FakeDeterministicInterviewPrepAiProvider();
        var gateway = CreateGateway(fake);
        return new InterviewPrepCoachingService(db, gateway);
    }

    internal static InterviewPrepSessionService CreateSessionService(
        ApplyVaultDbContext db,
        FakeDeterministicInterviewPrepAiProvider? fake = null)
    {
        fake ??= new FakeDeterministicInterviewPrepAiProvider();
        var catalog = CvSectionCatalogProvider.LoadFromDefaultPath();
        var structured = new CvStructuredDocumentService(db, catalog);
        var gateway = CreateGateway(fake);
        var modeCatalog = new InterviewPrepModeCatalog();
        var personaCatalog = new InterviewPrepPersonaCatalog();
        var contextBuilder = new InterviewContextBuilder(gateway, modeCatalog, personaCatalog);
        var interviewOptions = Microsoft.Extensions.Options.Options.Create(new InterviewPrepOptions());
        var caseCatalog = new InterviewPrepCaseCatalog();
        var caseRuntime = new InterviewPrepCaseRuntime();
        var planner = new InterviewPlanner(
            gateway,
            new InterviewPrepCompetencyCatalog(),
            modeCatalog,
            personaCatalog,
            caseCatalog,
            interviewOptions);
        var loopGuard = new InterviewLoopGuard();
        var questionBank = new FixedInterviewPrepQuestionBank();
        var fullLoop = new InterviewPrepFullLoopService(db, gateway, planner, questionBank);
        var adaptive = new InterviewPrepAdaptiveRuntime(
            db,
            gateway,
            loopGuard,
            contextBuilder,
            questionBank,
            caseRuntime,
            caseCatalog,
            fullLoop,
            new InterviewPrepDebugTraceContext(),
            new InterviewPrepDebugFileTraceLogger(),
            interviewOptions);
        var reporting = new InterviewPrepReportingService(
            db,
            gateway,
            new InterviewPrepCompetencyCatalog(),
            contextBuilder);

        return new InterviewPrepSessionService(
            db,
            NullLogger<InterviewPrepSessionService>.Instance,
            new InterviewPrepDebugTraceContext(),
            new InterviewPrepDebugFileTraceLogger(),
            new InterviewPrepCandidateContextAdapter(structured, catalog),
            new InterviewPrepJobContextAdapter(new EfCoreScrapeResultStore(db)),
            questionBank,
            contextBuilder,
            planner,
            adaptive,
            reporting,
            fullLoop);
    }

    internal static async Task<AppUserEntity> SeedUserWithCvAsync(ApplyVaultDbContext db)
    {
        var user = new AppUserEntity
        {
            Id = Guid.NewGuid(),
            SupabaseUserId = Guid.NewGuid().ToString("N"),
            Email = "prep@test.local",
            CreatedAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow
        };
        var utcNow = DateTimeOffset.UtcNow;
        var documentId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        db.Users.Add(user);
        db.UserCvDocuments.Add(new UserCvDocumentEntity
        {
            Id = documentId,
            UserId = user.Id,
            OriginalFileName = "cv.pdf",
            ContentType = "application/pdf",
            StorageKey = "cv.pdf",
            BaseStorageKey = "cv.pdf",
            FileSizeBytes = 10,
            OriginalFileSizeBytes = 10,
            UploadedAt = utcNow,
            UpdatedAt = utcNow,
            StructuredImportedAt = utcNow
        });
        db.UserCvSections.Add(new UserCvSectionEntity
        {
            Id = sectionId,
            UserId = user.Id,
            UserCvDocumentId = documentId,
            Heading = "Experience",
            SectionType = CvSectionTypes.Experience,
            SortOrder = 0
        });
        db.UserCvEntries.Add(new UserCvEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SectionId = sectionId,
            Title = "Engineer",
            Summary = "Built systems.",
            BulletsJson = "[]",
            TechStack = string.Empty,
            Source = CvEntrySources.Manual,
            SortOrder = 0
        });
        await db.SaveChangesAsync();
        return user;
    }

    private static InterviewPrepAiGateway CreateGateway(FakeDeterministicInterviewPrepAiProvider fake) =>
        new(
            fake,
            new InterviewPrepPromptRegistry(),
            Microsoft.Extensions.Options.Options.Create(new InterviewPrepAiOptions
            {
                Enabled = true,
                UseFakeProvider = true,
                MaxRetries = 0,
                AllowSafeFallback = true
            }),
            Microsoft.Extensions.Options.Options.Create(new GoogleAiOptions { Enabled = false }),
            NullLogger<InterviewPrepAiGateway>.Instance,
            new InterviewPrepDebugTraceContext(),
            new InterviewPrepDebugFileTraceLogger());
}
