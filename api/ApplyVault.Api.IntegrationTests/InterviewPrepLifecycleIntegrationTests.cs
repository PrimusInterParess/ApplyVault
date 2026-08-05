using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Models.InterviewPrep;
using ApplyVault.Api.Services;
using ApplyVault.Api.Services.CvSectionCatalog;
using ApplyVault.Api.Services.InterviewPrep.Catalogs;
using ApplyVault.Api.Services.InterviewPrep.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ApplyVault.Api.IntegrationTests;

public sealed class InterviewPrepLifecycleIntegrationTests(ApplyVaultWebApplicationFactory factory)
    : IClassFixture<ApplyVaultWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public async Task Full_lifecycle_prepare_start_turns_pause_resume_complete()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        await EnsureStructuredCvAsync(client, TestUserTokens.UserASub);

        var createResponse = await client.PostAsJsonAsync(
            "/api/interview-prep/sessions",
            CreateRequest(),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<InterviewPrepSessionSummaryDto>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("created", created!.Status);

        var prepareResponse = await client.PostAsync($"/api/interview-prep/sessions/{created.Id}/prepare", null);
        Assert.Equal(HttpStatusCode.OK, prepareResponse.StatusCode);
        var prepared = await prepareResponse.Content.ReadFromJsonAsync<InterviewPrepSessionDetailDto>(JsonOptions);
        Assert.NotNull(prepared);
        Assert.Equal("ready", prepared!.Status);
        Assert.True(prepared.HasCvSnapshot);
        Assert.NotEmpty(prepared.Stages);
        Assert.NotNull(prepared.Brief);
        Assert.False(string.IsNullOrWhiteSpace(prepared.Brief!.Summary));
        Assert.NotNull(prepared.Plan);
        Assert.NotEmpty(prepared.Plan!.Competencies);
        Assert.NotEmpty(prepared.Plan.QuestionIntents);
        Assert.True(prepared.Plan.Budgets.MaxQuestions > 0);
        Assert.DoesNotContain(
            JsonSerializer.Serialize(prepared.Brief),
            "systemInstruction",
            StringComparison.OrdinalIgnoreCase);

        client.DefaultRequestHeaders.IfMatch.Clear();
        client.DefaultRequestHeaders.IfMatch.ParseAdd(prepared.ETag);

        var startResponse = await client.PostAsync($"/api/interview-prep/sessions/{created.Id}/start", null);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var started = await startResponse.Content.ReadFromJsonAsync<InterviewPrepSessionDetailDto>(JsonOptions);
        Assert.NotNull(started);
        Assert.Equal("inProgress", started!.Status);
        Assert.Contains(started.Turns, (turn) => turn.Role == "interviewer");

        client.DefaultRequestHeaders.IfMatch.Clear();
        client.DefaultRequestHeaders.IfMatch.ParseAdd(started.ETag);

        var pauseResponse = await client.PostAsync($"/api/interview-prep/sessions/{created.Id}/pause", null);
        Assert.Equal(HttpStatusCode.OK, pauseResponse.StatusCode);
        var paused = await pauseResponse.Content.ReadFromJsonAsync<InterviewPrepSessionDetailDto>(JsonOptions);
        Assert.Equal("paused", paused!.Status);

        client.DefaultRequestHeaders.IfMatch.Clear();
        client.DefaultRequestHeaders.IfMatch.ParseAdd(paused.ETag);

        var resumeResponse = await client.PostAsync($"/api/interview-prep/sessions/{created.Id}/resume", null);
        Assert.Equal(HttpStatusCode.OK, resumeResponse.StatusCode);
        var resumed = await resumeResponse.Content.ReadFromJsonAsync<InterviewPrepSessionDetailDto>(JsonOptions);
        Assert.Equal("inProgress", resumed!.Status);

        InterviewPrepSessionDetailDto current = resumed;
        var completedViaTurns = false;
        for (var i = 0; i < 12; i++)
        {
            client.DefaultRequestHeaders.IfMatch.Clear();
            client.DefaultRequestHeaders.IfMatch.ParseAdd(current.ETag);

            var turnResponse = await client.PostAsJsonAsync(
                $"/api/interview-prep/sessions/{created.Id}/turns",
                new InterviewPrepSubmitTurnRequest(
                    $"turn-{i}",
                    $"I owned a concrete outcome with measurable impact in situation {i} working across stakeholders."),
                JsonOptions);

            Assert.Equal(HttpStatusCode.OK, turnResponse.StatusCode);
            var turnResult = await turnResponse.Content.ReadFromJsonAsync<InterviewPrepTurnSubmitResponseDto>(JsonOptions);
            Assert.NotNull(turnResult);
            current = turnResult!.Session;

            if (turnResult.InterviewComplete)
            {
                completedViaTurns = true;
                Assert.Equal("completing", current.Status);
                break;
            }

            Assert.NotNull(turnResult.NextInterviewerTurn);
        }

        Assert.True(completedViaTurns);

        client.DefaultRequestHeaders.IfMatch.Clear();
        client.DefaultRequestHeaders.IfMatch.ParseAdd(current.ETag);
        var completeResponse = await client.PostAsync($"/api/interview-prep/sessions/{created.Id}/complete", null);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        var completed = await completeResponse.Content.ReadFromJsonAsync<InterviewPrepSessionDetailDto>(JsonOptions);
        Assert.Equal("completed", completed!.Status);

        var transcriptResponse = await client.GetAsync($"/api/interview-prep/sessions/{created.Id}/transcript");
        Assert.Equal(HttpStatusCode.OK, transcriptResponse.StatusCode);
        var transcript = await transcriptResponse.Content.ReadFromJsonAsync<InterviewPrepTranscriptDto>(JsonOptions);
        Assert.NotNull(transcript);
        Assert.NotEmpty(transcript!.Turns);

        var reportResponse = await client.GetAsync($"/api/interview-prep/sessions/{created.Id}/report");
        Assert.Equal(HttpStatusCode.OK, reportResponse.StatusCode);
        var report = await reportResponse.Content.ReadFromJsonAsync<InterviewPrepCandidateReportDto>(JsonOptions);
        Assert.NotNull(report);
        Assert.Contains("not an employer hiring decision", report!.Disclaimer, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(report.EvidenceTrace);
        Assert.DoesNotContain(
            await reportResponse.Content.ReadAsStringAsync(),
            "AssessmentJson",
            StringComparison.OrdinalIgnoreCase);

        var competencyResponse = await client.GetAsync($"/api/interview-prep/sessions/{created.Id}/competencies");
        Assert.Equal(HttpStatusCode.OK, competencyResponse.StatusCode);
        var competencies = await competencyResponse.Content.ReadFromJsonAsync<InterviewPrepCompetencyResultsDto>(JsonOptions);
        Assert.NotNull(competencies);
        Assert.NotEmpty(competencies!.Competencies);
    }

    [Fact]
    public async Task Foreign_session_returns_404()
    {
        using var clientA = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        await EnsureStructuredCvAsync(clientA, TestUserTokens.UserASub);

        var createResponse = await clientA.PostAsJsonAsync(
            "/api/interview-prep/sessions",
            CreateRequest(),
            JsonOptions);
        var created = await createResponse.Content.ReadFromJsonAsync<InterviewPrepSessionSummaryDto>(JsonOptions);
        Assert.NotNull(created);

        using var clientB = factory.CreateAuthenticatedClient(TestUserTokens.UserB);
        var getResponse = await clientB.GetAsync($"/api/interview-prep/sessions/{created!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Duplicate_clientTurnId_is_idempotent()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        await EnsureStructuredCvAsync(client, TestUserTokens.UserASub);

        var created = await (await client.PostAsJsonAsync(
            "/api/interview-prep/sessions",
            CreateRequest(),
            JsonOptions)).Content.ReadFromJsonAsync<InterviewPrepSessionSummaryDto>(JsonOptions);

        var prepared = await (await client.PostAsync(
            $"/api/interview-prep/sessions/{created!.Id}/prepare",
            null)).Content.ReadFromJsonAsync<InterviewPrepSessionDetailDto>(JsonOptions);

        client.DefaultRequestHeaders.IfMatch.Clear();
        client.DefaultRequestHeaders.IfMatch.ParseAdd(prepared!.ETag);
        var started = await (await client.PostAsync(
            $"/api/interview-prep/sessions/{created.Id}/start",
            null)).Content.ReadFromJsonAsync<InterviewPrepSessionDetailDto>(JsonOptions);

        client.DefaultRequestHeaders.IfMatch.Clear();
        client.DefaultRequestHeaders.IfMatch.ParseAdd(started!.ETag);

        var first = await client.PostAsJsonAsync(
            $"/api/interview-prep/sessions/{created.Id}/turns",
            new InterviewPrepSubmitTurnRequest("same-turn", "First answer"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstBody = await first.Content.ReadFromJsonAsync<InterviewPrepTurnSubmitResponseDto>(JsonOptions);

        client.DefaultRequestHeaders.IfMatch.Clear();
        client.DefaultRequestHeaders.IfMatch.ParseAdd(firstBody!.Session.ETag);

        var second = await client.PostAsJsonAsync(
            $"/api/interview-prep/sessions/{created.Id}/turns",
            new InterviewPrepSubmitTurnRequest("same-turn", "Different answer"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<InterviewPrepTurnSubmitResponseDto>(JsonOptions);

        Assert.Equal(firstBody.CandidateTurn.Id, secondBody!.CandidateTurn.Id);
        Assert.Equal("First answer", secondBody.CandidateTurn.Text);
    }

    [Fact]
    public async Task Illegal_transition_returns_409()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        await EnsureStructuredCvAsync(client, TestUserTokens.UserASub);

        var created = await (await client.PostAsJsonAsync(
            "/api/interview-prep/sessions",
            CreateRequest(),
            JsonOptions)).Content.ReadFromJsonAsync<InterviewPrepSessionSummaryDto>(JsonOptions);

        var startResponse = await client.PostAsync($"/api/interview-prep/sessions/{created!.Id}/start", null);
        Assert.Equal(HttpStatusCode.Conflict, startResponse.StatusCode);
    }

    [Fact]
    public async Task Expanded_mode_and_senior_peer_prepare_successfully()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        await EnsureStructuredCvAsync(client, TestUserTokens.UserASub);

        var createResponse = await client.PostAsJsonAsync(
            "/api/interview-prep/sessions",
            new InterviewPrepCreateSessionRequest(
                InterviewPrepMode.RoleAndDomainDepth,
                InterviewPrepPersona.SeniorPeer,
                InterviewPrepLanguage.English,
                InterviewPrepMarket.General,
                InterviewPrepExperienceType.RealisticSimulation,
                InterviewPrepInteractionType.Text),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<InterviewPrepSessionSummaryDto>(JsonOptions);
        Assert.Equal("roleAndDomainDepth", created!.Mode);
        Assert.Equal("seniorPeer", created.Persona);

        var prepareResponse = await client.PostAsync(
            $"/api/interview-prep/sessions/{created.Id}/prepare",
            null);
        Assert.Equal(HttpStatusCode.OK, prepareResponse.StatusCode);
        var prepared = await prepareResponse.Content.ReadFromJsonAsync<InterviewPrepSessionDetailDto>(JsonOptions);
        Assert.Equal("ready", prepared!.Status);
        Assert.NotNull(prepared.Plan);
        Assert.Contains(
            prepared.Plan!.Competencies,
            (c) => c.CompetencyId == InterviewPrepCompetencyCatalog.RoleDepth);
    }

    [Fact]
    public async Task Unfinished_mode_returns_400_on_create()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        var response = await client.PostAsJsonAsync(
            "/api/interview-prep/sessions",
            new InterviewPrepCreateSessionRequest(
                InterviewPrepMode.FullLoop,
                InterviewPrepPersona.Recruiter,
                InterviewPrepLanguage.English,
                InterviewPrepMarket.General,
                InterviewPrepExperienceType.RealisticSimulation,
                InterviewPrepInteractionType.Text),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unsupported_config_returns_400()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        var response = await client.PostAsJsonAsync(
            "/api/interview-prep/sessions",
            new
            {
                mode = "technicalDeepDive",
                persona = "recruiter",
                language = "english",
                market = "general",
                experienceType = "realisticSimulation",
                interactionType = "text"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Case_bar_raiser_session_prepares_with_case_attachment()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        await EnsureStructuredCvAsync(client, TestUserTokens.UserASub);

        var createResponse = await client.PostAsJsonAsync(
            "/api/interview-prep/sessions",
            new InterviewPrepCreateSessionRequest(
                InterviewPrepMode.ProblemSolvingCase,
                InterviewPrepPersona.BarRaiser,
                InterviewPrepLanguage.English,
                InterviewPrepMarket.General,
                InterviewPrepExperienceType.RealisticSimulation,
                InterviewPrepInteractionType.Text),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<InterviewPrepSessionSummaryDto>(JsonOptions);
        Assert.NotNull(created);

        var prepareResponse = await client.PostAsync($"/api/interview-prep/sessions/{created!.Id}/prepare", null);
        Assert.Equal(HttpStatusCode.OK, prepareResponse.StatusCode);
        var prepared = await prepareResponse.Content.ReadFromJsonAsync<InterviewPrepSessionDetailDto>(JsonOptions);
        Assert.NotNull(prepared);
        Assert.Equal("problemSolvingCase", prepared!.Mode);
        Assert.Equal("barRaiser", prepared.Persona);
        Assert.NotNull(prepared.Plan);
        Assert.Contains("b2b-saas-churn-spike", prepared.Plan!.PlanSummary, StringComparison.Ordinal);

        client.DefaultRequestHeaders.IfMatch.Clear();
        client.DefaultRequestHeaders.IfMatch.ParseAdd(prepared.ETag);
        var startResponse = await client.PostAsync($"/api/interview-prep/sessions/{created.Id}/start", null);
        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var started = await startResponse.Content.ReadFromJsonAsync<InterviewPrepSessionDetailDto>(JsonOptions);
        Assert.NotNull(started);
        Assert.Contains(started!.Turns, (turn) =>
            turn.Role == "interviewer"
            && turn.Text.Contains("B2B SaaS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Danish_market_session_create_prepare_start_tags_turn_language()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        await EnsureStructuredCvAsync(client, TestUserTokens.UserASub);

        var createResponse = await client.PostAsJsonAsync(
            "/api/interview-prep/sessions",
            new InterviewPrepCreateSessionRequest(
                InterviewPrepMode.ScreeningAndMotivation,
                InterviewPrepPersona.Recruiter,
                InterviewPrepLanguage.Danish,
                InterviewPrepMarket.Danish,
                InterviewPrepExperienceType.RealisticSimulation,
                InterviewPrepInteractionType.Text),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<InterviewPrepSessionSummaryDto>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("danish", created!.Language);
        Assert.Equal("danish", created.Market);

        var prepareResponse = await client.PostAsync($"/api/interview-prep/sessions/{created.Id}/prepare", null);
        Assert.Equal(HttpStatusCode.OK, prepareResponse.StatusCode);

        client.DefaultRequestHeaders.IfMatch.Clear();
        var prepared = await prepareResponse.Content.ReadFromJsonAsync<InterviewPrepSessionDetailDto>(JsonOptions);
        client.DefaultRequestHeaders.IfMatch.ParseAdd(prepared!.ETag);

        var startResponse = await client.PostAsync($"/api/interview-prep/sessions/{created.Id}/start", null);
        var started = await startResponse.Content.ReadFromJsonAsync<InterviewPrepSessionDetailDto>(JsonOptions);
        Assert.NotNull(started);
        Assert.Contains(started!.Turns, (turn) =>
            turn.Role == "interviewer" && turn.Language == "danish");

        client.DefaultRequestHeaders.IfMatch.Clear();
        client.DefaultRequestHeaders.IfMatch.ParseAdd(started.ETag);

        var turnResponse = await client.PostAsJsonAsync(
            $"/api/interview-prep/sessions/{created.Id}/turns",
            new InterviewPrepSubmitTurnRequest("m9-da-1", "Jeg har ledet et team på fem personer."),
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, turnResponse.StatusCode);
        var turnPayload = await turnResponse.Content.ReadFromJsonAsync<InterviewPrepTurnSubmitResponseDto>(JsonOptions);
        Assert.Equal("danish", turnPayload!.CandidateTurn.Language);
    }

    [Fact]
    public async Task Language_practice_mode_create_prepare_succeeds()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        await EnsureStructuredCvAsync(client, TestUserTokens.UserASub);

        var createResponse = await client.PostAsJsonAsync(
            "/api/interview-prep/sessions",
            new InterviewPrepCreateSessionRequest(
                InterviewPrepMode.LanguagePractice,
                InterviewPrepPersona.Recruiter,
                InterviewPrepLanguage.English,
                InterviewPrepMarket.General,
                InterviewPrepExperienceType.RealisticSimulation,
                InterviewPrepInteractionType.Text),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<InterviewPrepSessionSummaryDto>(JsonOptions);
        Assert.Equal("languagePractice", created!.Mode);

        var prepareResponse = await client.PostAsync($"/api/interview-prep/sessions/{created.Id}/prepare", null);
        Assert.Equal(HttpStatusCode.OK, prepareResponse.StatusCode);
        var prepared = await prepareResponse.Content.ReadFromJsonAsync<InterviewPrepSessionDetailDto>(JsonOptions);
        Assert.Contains(
            prepared!.Plan!.Competencies,
            (c) => c.CompetencyId == InterviewPrepCompetencyCatalog.LanguageFluency);
    }

    private static InterviewPrepCreateSessionRequest CreateRequest() =>
        new(
            InterviewPrepMode.ScreeningAndMotivation,
            InterviewPrepPersona.Recruiter,
            InterviewPrepLanguage.English,
            InterviewPrepMarket.General,
            InterviewPrepExperienceType.RealisticSimulation,
            InterviewPrepInteractionType.Text);

    private async Task EnsureStructuredCvAsync(HttpClient client, string supabaseUserId)
    {
        // Touch an authenticated endpoint so AppUser is upserted.
        var listResponse = await client.GetAsync("/api/interview-prep/sessions");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplyVaultDbContext>();
        var user = db.Users.Single((entry) => entry.SupabaseUserId == supabaseUserId);

        if (db.UserCvDocuments.Any((document) => document.UserId == user.Id))
        {
            return;
        }

        var utcNow = DateTimeOffset.UtcNow;
        var documentId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        db.UserCvDocuments.Add(new UserCvDocumentEntity
        {
            Id = documentId,
            UserId = user.Id,
            OriginalFileName = "cv.pdf",
            ContentType = "application/pdf",
            StorageKey = $"tests/{documentId:N}.pdf",
            BaseStorageKey = $"tests/{documentId:N}.pdf",
            FileSizeBytes = 128,
            OriginalFileSizeBytes = 128,
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
    }
}
