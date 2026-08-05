using ApplyVault.Api.Data;
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

namespace ApplyVault.Api.Tests.InterviewPrep;

public sealed class InterviewPrepReportingTests
{
    [Fact]
    public async Task Report_maps_evidence_to_trace_and_excludes_private_fields()
    {
        await using var db = CreateDbContext();
        var user = await SeedUserWithCvAsync(db);
        var (sessionService, reporting) = CreateServices(db);

        var sessionId = await RunAdaptiveToCompletingAsync(sessionService, user);
        var report = await reporting.GetReportAsync(user, sessionId);

        Assert.Contains("not an employer hiring decision", report.Disclaimer, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(report.EvidenceTrace);
        Assert.All(report.EvidenceTrace, (trace) =>
            Assert.False(string.IsNullOrWhiteSpace(trace.EvidenceQuote)));
        Assert.NotEmpty(report.PracticeRecommendations);

        var json = System.Text.Json.JsonSerializer.Serialize(report);
        Assert.DoesNotContain("AssessmentJson", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("systemInstruction", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LoopGuard", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rubric", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Missing_evidence_is_not_listed_as_development_weakness()
    {
        await using var db = CreateDbContext();
        var user = await SeedUserWithCvAsync(db);
        var (sessionService, reporting) = CreateServices(db, loopGuard =>
        {
            loopGuard.MaxSessionTurns = 40;
            loopGuard.MaxNoProgressStreak = 1;
        });

        var sessionId = await RunAdaptiveToCompletingAsync(sessionService, user, answer: "short");
        var report = await reporting.GetReportAsync(user, sessionId);

        Assert.Contains(report.MissingEvidence, (entry) => entry.IsUnknownNotWeakness);
        foreach (var missing in report.MissingEvidence.Where((entry) => entry.IsUnknownNotWeakness))
        {
            Assert.DoesNotContain(report.DevelopmentAreas, (area) =>
                area.Contains(missing.Signal, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task Report_does_not_invent_experience_without_candidate_turns()
    {
        await using var db = CreateDbContext();
        var user = await SeedUserWithCvAsync(db);
        var (sessionService, reporting) = CreateServices(db);

        var created = await sessionService.CreateAsync(user, DefaultCreate());
        await sessionService.PrepareAsync(user, created.Id, ifMatch: null);
        await sessionService.StartAsync(user, created.Id, ifMatch: null);
        await sessionService.CompleteAsync(user, created.Id, ifMatch: null);

        var report = await reporting.GetReportAsync(user, created.Id);
        Assert.Contains(report.MissingEvidence, (entry) =>
            entry.Signal == "session:no_candidate_turns");
        Assert.Empty(report.EvidenceTrace);
        Assert.DoesNotContain(report.Strengths, (strength) =>
            strength.Contains("Demonstrated", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Competencies_endpoint_returns_safe_summary_without_private_assessment()
    {
        await using var db = CreateDbContext();
        var user = await SeedUserWithCvAsync(db);
        var (sessionService, reporting) = CreateServices(db);
        var sessionId = await RunAdaptiveToCompletingAsync(sessionService, user);

        var results = await reporting.GetCompetenciesAsync(user, sessionId);
        Assert.NotEmpty(results.Competencies);

        var json = System.Text.Json.JsonSerializer.Serialize(results);
        Assert.DoesNotContain("AssessmentJson", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("score", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Transcript_returns_ordered_candidate_facing_turns_only()
    {
        await using var db = CreateDbContext();
        var user = await SeedUserWithCvAsync(db);
        var (sessionService, reporting) = CreateServices(db);
        var sessionId = await RunAdaptiveToCompletingAsync(sessionService, user);

        var transcript = await reporting.GetTranscriptAsync(user, sessionId);
        Assert.True(transcript.Turns.Count > 0);
        Assert.All(transcript.Turns, (turn) =>
            Assert.True(turn.Role is "interviewer" or "candidate"));
        Assert.Equal(transcript.Turns.OrderBy((turn) => turn.Sequence), transcript.Turns);
    }

    private static async Task<Guid> RunAdaptiveToCompletingAsync(
        InterviewPrepSessionService sessionService,
        AppUserEntity user,
        string? answer = null)
    {
        answer ??=
            "I owned delivery end-to-end, measured success with latency SLOs, and coordinated with stakeholders weekly.";
        var created = await sessionService.CreateAsync(user, DefaultCreate());
        await sessionService.PrepareAsync(user, created.Id, ifMatch: null);
        await sessionService.StartAsync(user, created.Id, ifMatch: null);

        for (var i = 0; i < 12; i++)
        {
            var result = await sessionService.SubmitTurnAsync(
                user,
                created.Id,
                new InterviewPrepSubmitTurnRequest($"turn-{i}", answer),
                ifMatch: null);
            if (result.InterviewComplete)
            {
                break;
            }
        }

        await sessionService.CompleteAsync(user, created.Id, ifMatch: null);
        return created.Id;
    }

    private static (InterviewPrepSessionService Session, InterviewPrepReportingService Reporting) CreateServices(
        ApplyVaultDbContext db,
        Action<InterviewPrepLoopGuardOptions>? configureLoopGuard = null)
    {
        var catalog = CvSectionCatalogProvider.LoadFromDefaultPath();
        var structured = new CvStructuredDocumentService(db, catalog);
        var fake = new FakeDeterministicInterviewPrepAiProvider();
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
            Microsoft.Extensions.Options.Options.Create(new GoogleAiOptions { Enabled = false }));
        var modeCatalog = new InterviewPrepModeCatalog();
        var personaCatalog = new InterviewPrepPersonaCatalog();
        var contextBuilder = new InterviewContextBuilder(gateway, modeCatalog, personaCatalog);
        var interviewOptions = new InterviewPrepOptions();
        configureLoopGuard?.Invoke(interviewOptions.LoopGuard);
        var options = Microsoft.Extensions.Options.Options.Create(interviewOptions);
        var competencyCatalog = new InterviewPrepCompetencyCatalog();
        var caseCatalog = new InterviewPrepCaseCatalog();
        var caseRuntime = new InterviewPrepCaseRuntime();
        var planner = new InterviewPlanner(gateway, competencyCatalog, modeCatalog, personaCatalog, caseCatalog, options);
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
            options);
        var reporting = new InterviewPrepReportingService(db, gateway, competencyCatalog, contextBuilder);
        var session = new InterviewPrepSessionService(
            db,
            new InterviewPrepCandidateContextAdapter(structured, catalog),
            new InterviewPrepJobContextAdapter(new EfCoreScrapeResultStore(db)),
            questionBank,
            contextBuilder,
            planner,
            adaptive,
            reporting,
            fullLoop);
        return (session, reporting);
    }

    private static InterviewPrepCreateSessionRequest DefaultCreate() =>
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
            .UseInMemoryDatabase($"interview-prep-report-{Guid.NewGuid():N}")
            .Options;
        return new ApplyVaultDbContext(options);
    }

    private static async Task<AppUserEntity> SeedUserWithCvAsync(ApplyVaultDbContext db)
    {
        var user = new AppUserEntity
        {
            Id = Guid.NewGuid(),
            SupabaseUserId = Guid.NewGuid().ToString("N"),
            Email = "prep-report@test.local",
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
}
