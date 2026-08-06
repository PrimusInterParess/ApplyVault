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
using ApplyVault.Api.Services.InterviewPrep.Domain;
using ApplyVault.Api.Services.InterviewPrep.FullLoop;
using ApplyVault.Api.Services.InterviewPrep.Planning;
using ApplyVault.Api.Services.InterviewPrep.Reporting;
using ApplyVault.Api.Services.InterviewPrep.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Tests.InterviewPrep;

public sealed class InterviewPrepSessionServiceTests
{
    [Fact]
    public async Task Prepare_then_start_persists_snapshots_opening_and_first_question()
    {
        await using var db = CreateDbContext();
        var user = await SeedUserWithCvAsync(db);
        var service = CreateService(db);

        var created = await service.CreateAsync(
            user,
            new InterviewPrepCreateSessionRequest(
                InterviewPrepMode.ScreeningAndMotivation,
                InterviewPrepPersona.Recruiter,
                InterviewPrepLanguage.English,
                InterviewPrepMarket.General,
                InterviewPrepExperienceType.RealisticSimulation,
                InterviewPrepInteractionType.Text));

        var prepared = await service.PrepareAsync(user, created.Id, ifMatch: null);
        Assert.Equal("ready", prepared.Status);
        Assert.True(prepared.HasCvSnapshot);
        Assert.NotNull(prepared.Brief);
        Assert.NotNull(prepared.Plan);

        var started = await service.StartAsync(user, created.Id, ifMatch: null);
        Assert.Equal("inProgress", started.Status);
        var interviewerTurns = started.Turns.Where((turn) => turn.Role == "interviewer").ToArray();
        Assert.True(interviewerTurns.Length >= 2);
        Assert.Contains(interviewerTurns, (turn) =>
            string.Equals(turn.Text, started.Turns.First((t) => t.Role == "interviewer").Text, StringComparison.Ordinal));
        Assert.DoesNotContain(JsonSerializerSafe(started), "AssessmentJson", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(JsonSerializerSafe(started), "systemInstruction", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Adaptive_submit_persists_evidence_and_advances_with_idempotency()
    {
        await using var db = CreateDbContext();
        var user = await SeedUserWithCvAsync(db);
        var service = CreateService(db);

        var created = await service.CreateAsync(user, DefaultCreate());
        await service.PrepareAsync(user, created.Id, ifMatch: null);
        var started = await service.StartAsync(user, created.Id, ifMatch: null);

        var answer =
            "In my last role I owned delivery end-to-end, measured success with latency SLOs, and coordinated with stakeholders weekly.";
        var first = await service.SubmitTurnAsync(
            user,
            created.Id,
            new InterviewPrepSubmitTurnRequest("client-1", answer),
            ifMatch: null);

        Assert.False(first.InterviewComplete);
        Assert.NotNull(first.NextInterviewerTurn);
        Assert.True(await db.InterviewPrepEvidenceItems.AnyAsync((item) => item.SessionId == created.Id));
        Assert.True(await db.InterviewPrepQuestionAttempts.AnyAsync((attempt) =>
            attempt.SessionId == created.Id && attempt.AssessmentStatus == "complete"));
        Assert.True(await db.InterviewPrepCompetencyCoverages.AnyAsync((coverage) =>
            coverage.SessionId == created.Id && coverage.AttemptCount > 0));

        var replay = await service.SubmitTurnAsync(
            user,
            created.Id,
            new InterviewPrepSubmitTurnRequest("client-1", answer),
            ifMatch: null);
        Assert.Equal(first.CandidateTurn.Id, replay.CandidateTurn.Id);
        Assert.Equal(first.NextInterviewerTurn!.Id, replay.NextInterviewerTurn!.Id);
    }

    [Fact]
    public async Task Adaptive_session_can_reach_completing_with_missing_evidence()
    {
        await using var db = CreateDbContext();
        var user = await SeedUserWithCvAsync(db);
        var service = CreateService(db, loopGuard =>
        {
            loopGuard.MaxSessionTurns = 40;
            loopGuard.MaxNoProgressStreak = 2;
        });

        var created = await service.CreateAsync(user, DefaultCreate());
        var prepared = await service.PrepareAsync(user, created.Id, ifMatch: null);
        Assert.NotNull(prepared.Plan);
        var started = await service.StartAsync(user, created.Id, ifMatch: null);
        Assert.Equal("inProgress", started.Status);

        InterviewPrepTurnSubmitResponseDto? last = null;
        for (var i = 0; i < 12; i++)
        {
            last = await service.SubmitTurnAsync(
                user,
                created.Id,
                new InterviewPrepSubmitTurnRequest($"turn-{i}", $"short"),
                ifMatch: null);
            if (last.InterviewComplete)
            {
                break;
            }
        }

        Assert.NotNull(last);
        Assert.True(last!.InterviewComplete);
        Assert.True(
            last.Session.Status is "completing" or "completed",
            last.Session.Status);
        Assert.Contains(
            last.Session.Stages,
            (stage) => stage.Status is "candidateQuestions" or "closing" or "completed");
    }

    [Fact]
    public async Task Answer_survives_when_assessment_uses_safe_fallback()
    {
        await using var db = CreateDbContext();
        var user = await SeedUserWithCvAsync(db);
        var fake = new FakeDeterministicInterviewPrepAiProvider
        {
            ForceInvalidJsonOnceFor = InterviewPrepAiOperation.AssessAnswer
        };
        var service = CreateService(db, configureLoopGuard: null, fake);

        var created = await service.CreateAsync(user, DefaultCreate());
        await service.PrepareAsync(user, created.Id, ifMatch: null);
        await service.StartAsync(user, created.Id, ifMatch: null);

        var result = await service.SubmitTurnAsync(
            user,
            created.Id,
            new InterviewPrepSubmitTurnRequest(
                "survive-1",
                "I led a migration that cut costs by twenty percent over two quarters with clear ownership."),
            ifMatch: null);

        Assert.Equal(
            "I led a migration that cut costs by twenty percent over two quarters with clear ownership.",
            result.CandidateTurn.Text);
        Assert.True(await db.InterviewPrepTurns.AnyAsync((turn) =>
            turn.SessionId == created.Id
            && turn.ClientTurnId == "survive-1"));
    }

    private static InterviewPrepCreateSessionRequest DefaultCreate() =>
        new(
            InterviewPrepMode.ScreeningAndMotivation,
            InterviewPrepPersona.Recruiter,
            InterviewPrepLanguage.English,
            InterviewPrepMarket.General,
            InterviewPrepExperienceType.RealisticSimulation,
            InterviewPrepInteractionType.Text);

    private static InterviewPrepSessionService CreateService(
        ApplyVaultDbContext db,
        Action<InterviewPrepLoopGuardOptions>? configureLoopGuard = null,
        FakeDeterministicInterviewPrepAiProvider? fake = null)
    {
        var catalog = CvSectionCatalogProvider.LoadFromDefaultPath();
        var structured = new CvStructuredDocumentService(db, catalog);
        fake ??= new FakeDeterministicInterviewPrepAiProvider();
        var gateway = new InterviewPrepAiGateway(
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
        var modeCatalog = new InterviewPrepModeCatalog();
        var personaCatalog = new InterviewPrepPersonaCatalog();
        var contextBuilder = new InterviewContextBuilder(gateway, modeCatalog, personaCatalog);
        var interviewOptions = new InterviewPrepOptions();
        configureLoopGuard?.Invoke(interviewOptions.LoopGuard);
        var options = Microsoft.Extensions.Options.Options.Create(interviewOptions);
        var caseCatalog = new InterviewPrepCaseCatalog();
        var caseRuntime = new InterviewPrepCaseRuntime();
        var planner = new InterviewPlanner(
            gateway,
            new InterviewPrepCompetencyCatalog(),
            modeCatalog,
            personaCatalog,
            caseCatalog,
            options);
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
            options);
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

    private static ApplyVaultDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplyVaultDbContext>()
            .UseInMemoryDatabase($"interview-prep-{Guid.NewGuid():N}")
            .Options;
        return new ApplyVaultDbContext(options);
    }

    private static async Task<AppUserEntity> SeedUserWithCvAsync(ApplyVaultDbContext db)
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

    private static string JsonSerializerSafe(object value) =>
        System.Text.Json.JsonSerializer.Serialize(value);
}
