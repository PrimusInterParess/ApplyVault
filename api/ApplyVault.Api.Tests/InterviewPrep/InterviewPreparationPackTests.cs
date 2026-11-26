using ApplyVault.Api.Data;
using ApplyVault.Api.Models.InterviewPrep;
using ApplyVault.Api.Services.InterviewPrep;
using ApplyVault.Api.Services.InterviewPrep.Adapters;
using ApplyVault.Api.Services.InterviewPrep.Ai;
using ApplyVault.Api.Services.InterviewPrep.Domain;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Tests.InterviewPrep;

public sealed class InterviewPreparationPackTests
{
    [Fact]
    public void General_preparation_requires_a_role_and_level()
    {
        Assert.Throws<InterviewPrepValidationException>(() => InterviewPreparationPackValidation.Normalize(new()));
        Assert.Throws<InterviewPrepValidationException>(() => InterviewPreparationPackValidation.Normalize(new(TargetRole: "Designer")));
        var input = InterviewPreparationPackValidation.Normalize(new(TargetRole: " Designer ", TargetLevel: " Mid-level "));
        Assert.Equal("Designer", input.TargetRole);
        Assert.Equal("Mid-level", input.TargetLevel);
        Assert.Equal(14, input.PreparationDays);
        Assert.Equal(60, input.MinutesPerDay);
    }

    [Fact]
    public void Forty_day_preparation_and_forty_study_sessions_are_supported()
    {
        var input = InterviewPreparationPackValidation.Normalize(Input() with { PreparationDays = 40 });
        var content = Content() with { StudyPlan = Enumerable.Range(1, 40)
            .Select(day => new PreparationStudySession(day, 30, "Recall, practice, check, and explain.", ["foundations"])).ToArray() };
        InterviewPreparationPackValidation.Content(content, input);
        Assert.Throws<InterviewPrepValidationException>(() => InterviewPreparationPackValidation.Normalize(input with { PreparationDays = 91 }));
    }

    [Fact]
    public void Job_target_can_be_inferred_but_time_and_language_are_validated()
    {
        var input = new PreparationPackInput(ScrapeResultId: Guid.NewGuid());
        InterviewPreparationPackValidation.Normalize(input);
        Assert.Throws<InterviewPrepValidationException>(() => InterviewPreparationPackValidation.Normalize(input with { PreparationDays = 0 }));
        Assert.Throws<InterviewPrepValidationException>(() => InterviewPreparationPackValidation.Normalize(input with { MinutesPerDay = 241 }));
        Assert.Throws<InterviewPrepValidationException>(() => InterviewPreparationPackValidation.Normalize(input with { Language = "unknown" }));
    }

    [Fact]
    public void Invalid_prerequisites_links_and_overbooked_sessions_are_rejected()
    {
        var input = Input();
        var content = Content();
        InterviewPreparationPackValidation.Content(content, input);
        Assert.Throws<InterviewPrepAiUnavailableException>(() => InterviewPreparationPackValidation.Content(content with
        {
            Documents = [content.Documents[0] with { PrerequisiteIds = ["missing"] }]
        }, input));
        Assert.Throws<InterviewPrepAiUnavailableException>(() => InterviewPreparationPackValidation.Content(content with
        {
            Documents = [content.Documents[0] with { Markdown = content.Documents[0].Markdown + " [Missing](missing.md)" }]
        }, input));
        Assert.Throws<InterviewPrepAiUnavailableException>(() => InterviewPreparationPackValidation.Content(content with
        {
            StudyPlan = [new(1, input.MinutesPerDay + 1, "Too much work", ["foundations"])]
        }, input));
        Assert.Throws<InterviewPrepAiUnavailableException>(() => InterviewPreparationPackValidation.Content(content with
        {
            StudyPlan = [content.StudyPlan[0], content.StudyPlan[0]]
        }, input));
    }

    [Fact]
    public void Study_plan_cannot_schedule_a_document_before_its_prerequisites_or_omit_documents()
    {
        var content = Content();
        var advanced = content.Documents[0] with { Id = "advanced", PrerequisiteIds = ["foundations"] };
        var expanded = content with { Documents = [content.Documents[0], advanced] };
        Assert.Throws<InterviewPrepAiUnavailableException>(() => InterviewPreparationPackValidation.Content(expanded, Input()));
        Assert.Throws<InterviewPrepAiUnavailableException>(() => InterviewPreparationPackValidation.Content(expanded with
        {
            StudyPlan = [new(1, 30, "Study the advanced topic", ["advanced"]), content.StudyPlan[0] with { Day = 2 }]
        }, Input()));
        InterviewPreparationPackValidation.Content(expanded with
        {
            StudyPlan = [content.StudyPlan[0], new(2, 30, "Apply the foundation to a new constraint", ["advanced"])]
        }, Input());
    }

    [Fact]
    public async Task Generation_uses_user_selected_projects_and_additional_facts_and_persists_progress()
    {
        await using var fixture = new Fixture();
        var project = fixture.AddProject(fixture.User.Id);
        await fixture.Db.SaveChangesAsync();
        var pack = await fixture.Service.GenerateAsync(fixture.User, Input() with
        {
            ProjectSummaryIds = [project.Id], AdditionalInformation = "I implemented the reporting workflow."
        }, default);
        Assert.Single(fixture.Generator.Sources!.Projects);
        Assert.Equal("I implemented the reporting workflow.", fixture.Generator.Sources.Input.AdditionalInformation);
        Assert.Empty(pack.OutdatedReasons);
        Assert.False(pack.NeedsRegeneration);
        pack = await WriteAllAsync(fixture, pack);
        var progress = PreparationProgress.Empty with { ReadDocumentIds = ["foundations"], ReviewLog = "Need to practice adapting the design." };
        var saved = await fixture.Service.SaveProgressAsync(fixture.User, pack.Id, new(pack.UpdatedAt, progress), default);
        Assert.Equal(progress.ReviewLog, saved.Progress.ReviewLog);
        Assert.Single(saved.Progress.ReadDocumentIds);
        var fetched = await fixture.Service.GetAsync(fixture.User, pack.Id, default);
        Assert.Empty(fetched.OutdatedReasons);
        await Assert.ThrowsAsync<InterviewPrepConflictException>(() => fixture.Service.SaveProgressAsync(fixture.User, pack.Id, new(pack.UpdatedAt, progress), default));
    }

    [Fact]
    public async Task Sources_are_owned_and_changed_project_content_marks_pack_outdated()
    {
        await using var fixture = new Fixture();
        var otherProject = fixture.AddProject(Guid.NewGuid());
        var ownProject = fixture.AddProject(fixture.User.Id);
        await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InterviewPrepValidationException>(() => fixture.Service.GenerateAsync(fixture.User, Input() with { ProjectSummaryIds = [otherProject.Id] }, default));
        var pack = await fixture.Service.GenerateAsync(fixture.User, Input() with { ProjectSummaryIds = [ownProject.Id] }, default);
        ownProject.CvSummary = "Updated project evidence";
        await fixture.Db.SaveChangesAsync();
        Assert.NotEmpty((await fixture.Service.GetAsync(fixture.User, pack.Id, default)).OutdatedReasons);
        var otherUser = new AppUserEntity { Id = Guid.NewGuid(), SupabaseUserId = "other-user" };
        await Assert.ThrowsAsync<InterviewPrepNotFoundException>(() => fixture.Service.GetAsync(otherUser, pack.Id, default));
        Assert.Empty(await fixture.Service.ListAsync(otherUser, default));
        Assert.False(await fixture.Service.DeleteAsync(otherUser, pack.Id, default));
    }

    [Fact]
    public async Task Failed_regeneration_preserves_documents_and_successful_regeneration_resets_progress()
    {
        await using var fixture = new Fixture();
        var pack = await fixture.Service.GenerateAsync(fixture.User, Input(), default);
        pack = await fixture.Service.SaveProgressAsync(fixture.User, pack.Id,
            new(pack.UpdatedAt, PreparationProgress.Empty with { ReviewLog = "Existing work" }), default);
        fixture.Generator.Fail = true;
        await Assert.ThrowsAsync<InterviewPrepAiUnavailableException>(() => fixture.Service.RegenerateAsync(fixture.User, pack.Id, new(pack.UpdatedAt, Input()), default));
        var preserved = await fixture.Service.GetAsync(fixture.User, pack.Id, default);
        Assert.Equal("Existing work", preserved.Progress.ReviewLog);
        Assert.Equal(pack.UpdatedAt, preserved.UpdatedAt);
        fixture.Generator.Fail = false;
        var regenerated = await fixture.Service.RegenerateAsync(fixture.User, pack.Id, new(pack.UpdatedAt, Input()), default);
        Assert.Empty(regenerated.Progress.ReviewLog);
        await Assert.ThrowsAsync<InterviewPrepConflictException>(() => fixture.Service.GenerateAsync(fixture.User, Input(), default));
    }

    [Fact]
    public async Task Old_briefs_remain_visible_and_can_be_upgraded_in_place()
    {
        await using var fixture = new Fixture();
        var old = new InterviewPrepStudyBriefEntity
        {
            Id = Guid.NewGuid(), UserId = fixture.User.Id, Language = "english", Market = "general",
            BodyJson = "{\"topics\":[]}", CvFingerprint = "old", UpdatedAt = DateTimeOffset.UtcNow
        };
        fixture.Db.InterviewPrepStudyBriefs.Add(old);
        await fixture.Db.SaveChangesAsync();
        var legacy = Assert.Single(await fixture.Service.ListAsync(fixture.User, default));
        Assert.True(legacy.NeedsRegeneration);
        Assert.Null(legacy.Content);
        var upgraded = await fixture.Service.RegenerateAsync(fixture.User, old.Id, new(old.UpdatedAt, Input()), default);
        Assert.False(upgraded.NeedsRegeneration);
        Assert.Equal(old.Id, upgraded.Id);
    }

    [Fact]
    public void Independent_recall_or_review_days_do_not_require_a_linked_reading_document()
    {
        var content = Content() with { StudyPlan = [Content().StudyPlan[0],
            new PreparationStudySession(2, 30, "Recall the main decisions without notes, review the log, and prepare mentally.", [])] };
        InterviewPreparationPackValidation.Content(content, Input());
        InterviewPreparationPackValidation.Progress(PreparationProgress.Empty with { CompletedDays = [2] }, content);
    }

    [Fact]
    public void Progress_cannot_refer_to_other_documents_or_days()
    {
        Assert.Throws<InterviewPrepValidationException>(() => InterviewPreparationPackValidation.Progress(
            PreparationProgress.Empty with { ReadDocumentIds = ["missing"] }, Content()));
        Assert.Throws<InterviewPrepValidationException>(() => InterviewPreparationPackValidation.Progress(
            PreparationProgress.Empty with { CompletedDays = [2] }, Content()));
    }

    [Fact]
    public async Task Draft_outlines_can_prepare_questions_later_but_writing_still_requires_discovery()
    {
        await using var fixture = new Fixture();
        fixture.Generator.LeaveQuestionsForLater = true;
        var pack = await fixture.Service.GenerateAsync(fixture.User, Input(), default);
        Assert.Empty(pack.Discovery!.Documents[0].Questions);
        await Assert.ThrowsAsync<InterviewPrepValidationException>(() => fixture.Service.WriteDocumentAsync(fixture.User, pack.Id, "foundations", new(pack.UpdatedAt), default));
        pack = await fixture.Service.PrepareDocumentAsync(fixture.User, pack.Id, "foundations", new(pack.UpdatedAt), default);
        Assert.Single(pack.Discovery!.Documents[0].Questions);
        pack = await AnswerAsync(fixture, pack, "foundations", "unknown", "Please start with the basics.");
        pack = await fixture.Service.WriteDocumentAsync(fixture.User, pack.Id, "foundations", new(pack.UpdatedAt), default);
        Assert.True(pack.Content!.Documents[0].IsGenerated);
    }

    [Fact]
    public async Task Outline_precedes_writing_and_unknown_answers_are_shared_without_inventing_facts()
    {
        await using var fixture = new Fixture();
        fixture.Generator.TwoDocuments = true;
        var pack = await fixture.Service.GenerateAsync(fixture.User, Input(), default);
        Assert.All(pack.Content!.Documents, d => Assert.False(d.IsGenerated));
        Assert.Equal(0, fixture.Generator.WriteCalls);
        await Assert.ThrowsAsync<InterviewPrepValidationException>(() => fixture.Service.WriteDocumentAsync(fixture.User, pack.Id, "foundations", new(pack.UpdatedAt), default));
        pack = await AnswerAsync(fixture, pack, "foundations", "unknown", "I have never applied this.");
        pack = await fixture.Service.WriteDocumentAsync(fixture.User, pack.Id, "foundations", new(pack.UpdatedAt), default);
        Assert.True(pack.Content!.Documents[0].IsGenerated);
        Assert.False(pack.Content.Documents[1].IsGenerated);
        var firstBody = pack.Content.Documents[0].Markdown;
        pack = await AnswerAsync(fixture, pack, "application", "answered", "I implemented the reporting workflow; another person owned deployment.");
        pack = await fixture.Service.WriteDocumentAsync(fixture.User, pack.Id, "application", new(pack.UpdatedAt), default);
        Assert.Equal(firstBody, pack.Content!.Documents[0].Markdown);
        Assert.Equal(2, fixture.Generator.LastContext!.Discovery.Answers.Count);
        Assert.Contains(fixture.Generator.LastContext.Discovery.Answers, a => a.DocumentId == "foundations" && a.Status == "unknown");
        Assert.Contains(fixture.Generator.LastContext.Discovery.Answers, a => a.Text.Contains("another person owned deployment"));
    }

    [Fact]
    public async Task Help_is_read_only_and_draft_or_suggested_wording_never_becomes_a_saved_answer()
    {
        await using var fixture = new Fixture();
        var pack = await fixture.Service.GenerateAsync(fixture.User, Input(), default);
        var question = pack.Discovery!.Documents[0].Questions[0];
        var help = await fixture.Service.HelpAsync(fixture.User, pack.Id, "foundations", question.Id,
            new(pack.UpdatedAt, "I do not know how to start.", "Unconfirmed draft", "Earlier guidance"), default);
        Assert.Contains("guidance", help.Markdown);
        Assert.Equal("Unconfirmed draft", fixture.Generator.LastHelp!.Help.DraftAnswer);
        var fetched = await fixture.Service.GetAsync(fixture.User, pack.Id, default);
        Assert.Empty(fetched.Discovery!.Answers);
        Assert.Equal(pack.UpdatedAt, fetched.UpdatedAt);
        Assert.False(fetched.Content!.Documents[0].IsGenerated);
    }

    [Fact]
    public async Task Individual_rewrite_preserves_other_documents_progress_and_failure_preserves_the_old_body()
    {
        await using var fixture = new Fixture();
        fixture.Generator.TwoDocuments = true;
        var pack = await WriteAllAsync(fixture, await fixture.Service.GenerateAsync(fixture.User, Input(), default));
        var otherBody = pack.Content!.Documents[1].Markdown;
        pack = await fixture.Service.SaveProgressAsync(fixture.User, pack.Id, new(pack.UpdatedAt,
            PreparationProgress.Empty with { ReadDocumentIds = ["foundations", "application"], CompletedDays = [1, 2], ReviewLog = "Keep this log." }), default);
        pack = await fixture.Service.WriteDocumentAsync(fixture.User, pack.Id, "foundations", new(pack.UpdatedAt), default);
        Assert.Equal(["application"], pack.Progress.ReadDocumentIds);
        Assert.Equal([2], pack.Progress.CompletedDays);
        Assert.Equal("Keep this log.", pack.Progress.ReviewLog);
        Assert.Equal(otherBody, pack.Content!.Documents[1].Markdown);
        fixture.Generator.FailWrite = true;
        await Assert.ThrowsAsync<InterviewPrepAiUnavailableException>(() => fixture.Service.WriteDocumentAsync(fixture.User, pack.Id, "foundations", new(pack.UpdatedAt), default));
        var preserved = await fixture.Service.GetAsync(fixture.User, pack.Id, default);
        Assert.Equal(pack.UpdatedAt, preserved.UpdatedAt);
        Assert.Equal(pack.Content.Documents.Select(d => d.Markdown), preserved.Content!.Documents.Select(d => d.Markdown));
    }

    [Fact]
    public async Task Skipping_all_questions_is_allowed_but_stale_revisions_and_foreign_question_ids_are_rejected()
    {
        await using var fixture = new Fixture();
        var pack = await fixture.Service.GenerateAsync(fixture.User, Input(), default);
        await Assert.ThrowsAsync<InterviewPrepValidationException>(() => fixture.Service.SaveAnswersAsync(fixture.User, pack.Id, "foundations",
            new(pack.UpdatedAt, [new("another-document-question", "answered", "Facts")]), default));
        var saved = await AnswerAsync(fixture, pack, "foundations", "skipped", "This must not become a fact.");
        Assert.Empty(Assert.Single(saved.Discovery!.Answers).Text);
        await Assert.ThrowsAsync<InterviewPrepConflictException>(() => fixture.Service.WriteDocumentAsync(fixture.User, pack.Id, "foundations", new(pack.UpdatedAt), default));
        var written = await fixture.Service.WriteDocumentAsync(fixture.User, saved.Id, "foundations", new(saved.UpdatedAt), default);
        Assert.True(written.Content!.Documents[0].IsGenerated);
    }

    [Fact]
    public async Task Existing_v2_documents_can_get_questions_without_losing_content_or_progress()
    {
        await using var fixture = new Fixture();
        var entity = new InterviewPrepStudyBriefEntity
        {
            Id = Guid.NewGuid(), UserId = fixture.User.Id, Language = "english", Market = "general", CvFingerprint = "old",
            UpdatedAt = DateTimeOffset.UtcNow,
            BodyJson = System.Text.Json.JsonSerializer.Serialize(new StoredPreparationPack(2, Input(), Content(),
                PreparationProgress.Empty with { ReviewLog = "Old review log" }, false), InterviewPreparationPackService.JsonOptions)
        };
        var oldJson = System.Text.Json.Nodes.JsonNode.Parse(entity.BodyJson)!;
        oldJson.AsObject().Remove("discovery");
        foreach (var document in oldJson["content"]!["documents"]!.AsArray())
        {
            document!.AsObject().Remove("isGenerated");
            document.AsObject().Remove("contextRevision");
            document.AsObject().Remove("sourceFingerprint");
        }
        entity.BodyJson = oldJson.ToJsonString();
        fixture.Db.InterviewPrepStudyBriefs.Add(entity); await fixture.Db.SaveChangesAsync();
        var pack = await fixture.Service.PrepareDocumentAsync(fixture.User, entity.Id, "foundations", new(entity.UpdatedAt), default);
        Assert.Equal(Content().Documents[0].Markdown, pack.Content!.Documents[0].Markdown);
        Assert.Equal("Old review log", pack.Progress.ReviewLog);
        Assert.Single(pack.Discovery!.Documents[0].Questions);
    }

    private static async Task<PreparationPackDto> AnswerAsync(Fixture fixture, PreparationPackDto pack, string documentId, string status, string text) =>
        await fixture.Service.SaveAnswersAsync(fixture.User, pack.Id, documentId,
            new(pack.UpdatedAt, pack.Discovery!.Documents.Single(d => d.Id == documentId).Questions.Select(q => new PreparationAnswerInput(q.Id, status, text)).ToArray()), default);

    private static async Task<PreparationPackDto> WriteAllAsync(Fixture fixture, PreparationPackDto pack)
    {
        foreach (var document in pack.Content!.Documents)
        {
            pack = await AnswerAsync(fixture, pack, document.Id, "unknown", "I need help with the foundations.");
            pack = await fixture.Service.WriteDocumentAsync(fixture.User, pack.Id, document.Id, new(pack.UpdatedAt), default);
        }
        return pack;
    }

    internal static PreparationDiscoveryQuestion Question(string id, string kind = "knowledge") => new(id + "-needs", kind,
        "Where does your understanding become uncertain?", "This changes the depth and practice.",
        "Recall one small task. Describe what you can do and where you need help. Unknown or Skip are valid choices.",
        "I can ___. I am unsure about ___.", "Hypothetical example: a learner knows a definition but needs an applied example. This is not your history.");

    internal static PreparationDiscoveryPlan Plan(bool twoDocuments = false)
    {
        var content = Content();
        var documents = new List<PreparationDocumentOutline> { new("foundations", "Foundations", content.Documents[0].Purpose, [], [Question("foundations")]) };
        var sessions = content.StudyPlan.ToList();
        if (twoDocuments)
        {
            documents.Add(new("application", "Application", "Connect a principle to actual experience.", ["foundations"], [Question("application", "experience")]));
            sessions.Add(new(2, 30, "Apply, check, adapt, and record the next weakness.", ["application"]));
        }
        return new(content.Title, content.OverviewMarkdown, content.Unknowns, content.Topics, documents, sessions);
    }

    internal static PreparationPackInput Input() => new(TargetRole: "Product designer", TargetLevel: "Mid-level");
    internal static PreparationPackContent Content() => new("Role preparation", "Start by recalling the main constraint.", [],
        [new("Foundations", "The role requires reasoning about constraints.", "The CV provides exposure, not proof of mastery.", "foundations")],
        [new("foundations", "Foundations", "Apply a principle under a changed constraint.",
            "Begin with a situation where two requirements compete. Explain the mechanism of your chosen approach, compare an alternative, and identify the failure mode. Close the notes and apply the principle to a different situation. Check the outcome, change a constraint, and record what you could not explain.", [])],
        [new(1, 30, "Recall, study, attempt, check, explain, then record the next weakness.", ["foundations"])]);

    private sealed class Fixture : IAsyncDisposable
    {
        internal readonly ApplyVaultDbContext Db = new(new DbContextOptionsBuilder<ApplyVaultDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        internal readonly AppUserEntity User = new() { Id = Guid.NewGuid(), SupabaseUserId = "pack-user" };
        internal readonly Generator Generator = new();
        internal InterviewPreparationPackService Service { get; }
        internal Fixture() => Service = new(Db, new CandidateAdapter(), new JobAdapter(), Generator);
        internal UserCvProjectSummaryEntity AddProject(Guid userId)
        {
            var project = new UserCvProjectSummaryEntity
            {
                Id = Guid.NewGuid(), UserId = userId, FullName = "sample/project", HtmlUrl = "https://example.com/project",
                CvTitle = "Project", CvSummary = "Project facts", CvBullets = "[]", TechStack = "[]"
            };
            Db.UserCvProjectSummaries.Add(project);
            return project;
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
    private sealed class CandidateAdapter : IInterviewPrepCandidateContextAdapter
    {
        private readonly Guid _documentId = Guid.NewGuid();
        public Task<InterviewPrepCandidateSnapshot> CaptureAsync(AppUserEntity user, CancellationToken cancellationToken = default) =>
            Task.FromResult(new InterviewPrepCandidateSnapshot(_documentId, null, "1", "{\"experience\":\"design\"}", DateTimeOffset.UtcNow));
    }
    private sealed class JobAdapter : IInterviewPrepJobContextAdapter
    {
        public Task<InterviewPrepJobSnapshot?> CaptureAsync(AppUserEntity user, Guid scrapeResultId, CancellationToken cancellationToken = default) =>
            throw new InterviewPrepValidationException("Job is not owned by this user.");
    }
    private sealed class Generator : IInterviewPreparationPackGenerator
    {
        internal PreparationPackSources? Sources;
        internal PreparationDocumentContext? LastContext;
        internal PreparationQuestionHelpContext? LastHelp;
        internal bool Fail;
        internal bool FailWrite;
        internal bool TwoDocuments;
        internal bool LeaveQuestionsForLater;
        internal int WriteCalls;
        public Task<GeneratedPreparationPlan> PlanAsync(PreparationPackSources sources, CancellationToken ct)
        {
            Sources = sources;
            if (Fail) throw new InterviewPrepAiUnavailableException("Provider failed");
            var plan = Plan(TwoDocuments);
            if (LeaveQuestionsForLater) plan = plan with { Documents = plan.Documents.Select(d => d with { Questions = [] }).ToArray() };
            return Task.FromResult(new GeneratedPreparationPlan(plan, false));
        }
        public Task<GeneratedDocumentOutline> PrepareDocumentAsync(PreparationPackSources sources, PreparationDocument document,
            PreparationDiscovery? discovery, CancellationToken ct) => Task.FromResult(new GeneratedDocumentOutline(
                new(document.Id, document.Title, document.Purpose, document.PrerequisiteIds, [Question(document.Id)]), false));
        public Task<GeneratedPreparationDocument> WriteDocumentAsync(PreparationDocumentContext context, CancellationToken ct)
        {
            WriteCalls++; LastContext = context;
            if (FailWrite) throw new InterviewPrepAiUnavailableException("Writing failed");
            return Task.FromResult(new GeneratedPreparationDocument(Content().Documents[0].Markdown, false));
        }
        public Task<PreparationHelpResponse> HelpAsync(PreparationQuestionHelpContext context, CancellationToken ct)
        {
            LastHelp = context;
            return Task.FromResult(new PreparationHelpResponse("Use this guidance and a blank structure; no personal facts are supplied."));
        }
    }
}
