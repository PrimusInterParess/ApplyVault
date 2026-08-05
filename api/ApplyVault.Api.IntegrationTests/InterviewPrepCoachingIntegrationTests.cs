using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Models.InterviewPrep;
using ApplyVault.Api.Services;
using ApplyVault.Api.Services.CvSectionCatalog;
using ApplyVault.Api.Services.InterviewPrep.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ApplyVault.Api.IntegrationTests;

public sealed class InterviewPrepCoachingIntegrationTests(ApplyVaultWebApplicationFactory factory)
    : IClassFixture<ApplyVaultWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public async Task GuidedCoaching_review_retry_does_not_mutate_transcript()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        await EnsureStructuredCvAsync(client, TestUserTokens.UserASub);

        var createResponse = await client.PostAsJsonAsync(
            "/api/interview-prep/sessions",
            new InterviewPrepCreateSessionRequest(
                InterviewPrepMode.ScreeningAndMotivation,
                InterviewPrepPersona.Recruiter,
                InterviewPrepLanguage.English,
                InterviewPrepMarket.General,
                InterviewPrepExperienceType.GuidedCoaching,
                InterviewPrepInteractionType.Text),
            JsonOptions);
        var created = await createResponse.Content.ReadFromJsonAsync<InterviewPrepSessionSummaryDto>(JsonOptions);
        Assert.NotNull(created);

        await client.PostAsync($"/api/interview-prep/sessions/{created!.Id}/prepare", null);
        await client.PostAsync($"/api/interview-prep/sessions/{created.Id}/start", null);

        var turnResponse = await client.PostAsJsonAsync(
            $"/api/interview-prep/sessions/{created.Id}/turns",
            new InterviewPrepSubmitTurnRequest("coach-int-1", "Brief answer."),
            JsonOptions);
        var turn = await turnResponse.Content.ReadFromJsonAsync<InterviewPrepTurnSubmitResponseDto>(JsonOptions);
        Assert.NotNull(turn);

        var reviewResponse = await client.PostAsync(
            $"/api/interview-prep/sessions/{created.Id}/turns/{turn!.CandidateTurn.Id}/review",
            null);
        Assert.Equal(HttpStatusCode.OK, reviewResponse.StatusCode);
        var review = await reviewResponse.Content.ReadFromJsonAsync<InterviewPrepAnswerReviewDto>(JsonOptions);
        Assert.Equal("reviewed", review!.Status);
        Assert.DoesNotContain(
            await reviewResponse.Content.ReadAsStringAsync(),
            "AssessmentJson",
            StringComparison.OrdinalIgnoreCase);

        var retryResponse = await client.PostAsJsonAsync(
            $"/api/interview-prep/sessions/{created.Id}/turns/{turn.CandidateTurn.Id}/retry",
            new InterviewPrepSubmitAnswerRetryRequest(
                "I led a migration with clear metrics, stakeholder alignment, and a measurable thirty percent latency reduction over two quarters with weekly checkpoints."),
            JsonOptions);
        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);

        var getRetry = await client.GetAsync(
            $"/api/interview-prep/sessions/{created.Id}/turns/{turn.CandidateTurn.Id}/retry");
        Assert.Equal(HttpStatusCode.OK, getRetry.StatusCode);

        var transcript = await client.GetAsync($"/api/interview-prep/sessions/{created.Id}/transcript");
        var transcriptBody = await transcript.Content.ReadFromJsonAsync<InterviewPrepTranscriptDto>(JsonOptions);
        var candidateLine = transcriptBody!.Turns.Single((t) => t.Id == turn.CandidateTurn.Id);
        Assert.Equal("Brief answer.", candidateLine.Text);
    }

    [Fact]
    public async Task RealisticSimulation_live_review_returns_409()
    {
        using var client = factory.CreateAuthenticatedClient(TestUserTokens.UserA);
        await EnsureStructuredCvAsync(client, TestUserTokens.UserASub);

        var createResponse = await client.PostAsJsonAsync(
            "/api/interview-prep/sessions",
            new InterviewPrepCreateSessionRequest(
                InterviewPrepMode.ScreeningAndMotivation,
                InterviewPrepPersona.Recruiter,
                InterviewPrepLanguage.English,
                InterviewPrepMarket.General,
                InterviewPrepExperienceType.RealisticSimulation,
                InterviewPrepInteractionType.Text),
            JsonOptions);
        var created = await createResponse.Content.ReadFromJsonAsync<InterviewPrepSessionSummaryDto>(JsonOptions);

        await client.PostAsync($"/api/interview-prep/sessions/{created!.Id}/prepare", null);
        await client.PostAsync($"/api/interview-prep/sessions/{created.Id}/start", null);

        var turnResponse = await client.PostAsJsonAsync(
            $"/api/interview-prep/sessions/{created.Id}/turns",
            new InterviewPrepSubmitTurnRequest("sim-int-1", "Structured answer with outcomes."),
            JsonOptions);
        var turn = await turnResponse.Content.ReadFromJsonAsync<InterviewPrepTurnSubmitResponseDto>(JsonOptions);

        var reviewResponse = await client.PostAsync(
            $"/api/interview-prep/sessions/{created.Id}/turns/{turn!.CandidateTurn.Id}/review",
            null);
        Assert.Equal(HttpStatusCode.Conflict, reviewResponse.StatusCode);
        var payload = await reviewResponse.Content.ReadAsStringAsync();
        Assert.Contains("interview_prep_coaching_not_allowed_during_simulation", payload, StringComparison.OrdinalIgnoreCase);
    }

    private async Task EnsureStructuredCvAsync(HttpClient client, string supabaseUserId)
    {
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
