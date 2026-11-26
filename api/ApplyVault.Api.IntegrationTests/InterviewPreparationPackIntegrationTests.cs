using System.Net;
using System.Net.Http.Json;
using ApplyVault.Api.Data;
using ApplyVault.Api.Services;
using ApplyVault.Api.Models.InterviewPrep;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services.CvSectionCatalog;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ApplyVault.Api.IntegrationTests;

public sealed class InterviewPreparationPackIntegrationTests : IDisposable
{
    private readonly ApplyVaultWebApplicationFactory _factory = new();
    private readonly WebApplicationFactory<Program> _app;

    public InterviewPreparationPackIntegrationTests() => _app = _factory.WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services => services.PostConfigure<InterviewPrepAiOptions>(options => options.UseFakeProvider = true)));

    [Fact]
    public async Task Generate_read_track_progress_regenerate_and_delete_document_pack()
    {
        using var client = Client(TestUserTokens.UserA);
        await EnsureCvAsync(client);
        var input = new PreparationPackInput(TargetRole: "Product designer", TargetLevel: "Mid-level", AdditionalInformation: "Portfolio interview.");
        var generated = await client.PostAsJsonAsync("/api/interview-prep/packs", input);
        Assert.Equal(HttpStatusCode.Created, generated.StatusCode);
        Assert.NotNull(generated.Headers.Location);
        var pack = (await generated.Content.ReadFromJsonAsync<PreparationPackDto>())!;
        Assert.True(pack.IsDemo);
        Assert.NotEmpty(pack.Content!.Documents);
        Assert.Empty(pack.OutdatedReasons);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(generated.Headers.Location)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/interview-prep/packs", input)).StatusCode);

        var documentId = pack.Content.Documents[0].Id;
        var questionId = pack.Discovery!.Documents[0].Questions[0].Id;
        Assert.False(pack.Content.Documents[0].IsGenerated);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/interview-prep/packs/{pack.Id}/documents/{documentId}/generate", new PreparationRevisionRequest(pack.UpdatedAt))).StatusCode);
        var help = await client.PostAsJsonAsync($"/api/interview-prep/packs/{pack.Id}/documents/{documentId}/questions/{questionId}/help",
            new PreparationHelpRequest(pack.UpdatedAt, "I do not know how to do this. Walk me through the first step.", "Unconfirmed draft"));
        Assert.Equal(HttpStatusCode.OK, help.StatusCode);
        Assert.Contains("guidance", (await help.Content.ReadFromJsonAsync<PreparationHelpResponse>())!.Markdown);
        Assert.Empty((await client.GetFromJsonAsync<PreparationPackDto>($"/api/interview-prep/packs/{pack.Id}"))!.Discovery!.Answers);
        var answersResponse = await client.PutAsJsonAsync($"/api/interview-prep/packs/{pack.Id}/documents/{documentId}/answers",
            new PreparationAnswersRequest(pack.UpdatedAt, [new(questionId, "unknown", "I need help starting.")]));
        Assert.Equal(HttpStatusCode.OK, answersResponse.StatusCode);
        pack = (await answersResponse.Content.ReadFromJsonAsync<PreparationPackDto>())!;
        var written = await client.PostAsJsonAsync($"/api/interview-prep/packs/{pack.Id}/documents/{documentId}/generate", new PreparationRevisionRequest(pack.UpdatedAt));
        Assert.Equal(HttpStatusCode.OK, written.StatusCode);
        pack = (await written.Content.ReadFromJsonAsync<PreparationPackDto>())!;
        Assert.True(pack.Content!.Documents[0].IsGenerated);
        Assert.Equal("unknown", Assert.Single(pack.Discovery!.Answers).Status);

        var progress = PreparationProgress.Empty with { ReadDocumentIds = [pack.Content.Documents[0].Id], ReviewLog = "Need more independent practice." };
        var progressResponse = await client.PutAsJsonAsync($"/api/interview-prep/packs/{pack.Id}/progress", new PreparationProgressRequest(pack.UpdatedAt, progress));
        Assert.Equal(HttpStatusCode.OK, progressResponse.StatusCode);
        var updated = (await progressResponse.Content.ReadFromJsonAsync<PreparationPackDto>())!;
        Assert.Equal(progress.ReviewLog, updated.Progress.ReviewLog);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync($"/api/interview-prep/packs/{pack.Id}/progress", new PreparationProgressRequest(pack.UpdatedAt, progress))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/interview-prep/packs/{pack.Id}/progress",
            new PreparationProgressRequest(updated.UpdatedAt, progress with { ReadDocumentIds = ["another-pack-document"] }))).StatusCode);

        var regenerated = await client.PostAsJsonAsync($"/api/interview-prep/packs/{pack.Id}/regenerate", new RegeneratePreparationPackRequest(updated.UpdatedAt, input));
        Assert.Equal(HttpStatusCode.OK, regenerated.StatusCode);
        Assert.Empty((await regenerated.Content.ReadFromJsonAsync<PreparationPackDto>())!.Progress.ReadDocumentIds);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/interview-prep/packs/{pack.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/interview-prep/packs/{pack.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/interview-prep/sessions")).StatusCode);
    }

    [Fact]
    public async Task Packs_require_authentication_and_are_isolated_by_owner()
    {
        using var anonymous = _app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/interview-prep/packs")).StatusCode);
        using var a = Client(TestUserTokens.UserA);
        await EnsureCvAsync(a);
        var input = new PreparationPackInput(TargetRole: "Designer", TargetLevel: "Senior");
        var created = await a.PostAsJsonAsync("/api/interview-prep/packs", input);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var pack = (await created.Content.ReadFromJsonAsync<PreparationPackDto>())!;
        using var b = Client(TestUserTokens.UserB);
        Assert.Empty((await b.GetFromJsonAsync<PreparationPackDto[]>("/api/interview-prep/packs"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/interview-prep/packs/{pack.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync($"/api/interview-prep/packs/{pack.Id}/regenerate", new RegeneratePreparationPackRequest(pack.UpdatedAt, input))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PutAsJsonAsync($"/api/interview-prep/packs/{pack.Id}/progress", new PreparationProgressRequest(pack.UpdatedAt, pack.Progress))).StatusCode);
        var documentId = pack.Content!.Documents[0].Id;
        var questionId = pack.Discovery!.Documents[0].Questions[0].Id;
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync($"/api/interview-prep/packs/{pack.Id}/documents/{documentId}/discovery", new PreparationRevisionRequest(pack.UpdatedAt))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PutAsJsonAsync($"/api/interview-prep/packs/{pack.Id}/documents/{documentId}/answers",
            new PreparationAnswersRequest(pack.UpdatedAt, [new(questionId, "skipped")]))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync($"/api/interview-prep/packs/{pack.Id}/documents/{documentId}/generate", new PreparationRevisionRequest(pack.UpdatedAt))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync($"/api/interview-prep/packs/{pack.Id}/documents/{documentId}/questions/{questionId}/help",
            new PreparationHelpRequest(pack.UpdatedAt, "Help me start."))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.DeleteAsync($"/api/interview-prep/packs/{pack.Id}")).StatusCode);
    }

    [Fact]
    public async Task Generation_without_a_cv_or_target_returns_actionable_validation()
    {
        using var client = Client(TestUserTokens.UserB);
        var noTarget = await client.PostAsJsonAsync("/api/interview-prep/packs", new PreparationPackInput());
        Assert.Equal(HttpStatusCode.BadRequest, noTarget.StatusCode);
        Assert.Contains("target role and level", await noTarget.Content.ReadAsStringAsync());
        var noCv = await client.PostAsJsonAsync("/api/interview-prep/packs", new PreparationPackInput(TargetRole: "Designer", TargetLevel: "Senior"));
        Assert.Equal(HttpStatusCode.BadRequest, noCv.StatusCode);
        Assert.Contains("Structured CV", await noCv.Content.ReadAsStringAsync());
    }

    private HttpClient Client(string token)
    {
        var client = _app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    private async Task EnsureCvAsync(HttpClient client)
    {
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/interview-prep/context")).StatusCode);
        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplyVaultDbContext>();
        var user = db.Users.Single(u => u.SupabaseUserId == TestUserTokens.UserASub);
        if (db.UserCvDocuments.Any(d => d.UserId == user.Id)) return;
        var now = DateTimeOffset.UtcNow;
        var documentId = Guid.NewGuid(); var sectionId = Guid.NewGuid();
        db.UserCvDocuments.Add(new UserCvDocumentEntity
        {
            Id = documentId, UserId = user.Id, OriginalFileName = "cv.pdf", ContentType = "application/pdf",
            StorageKey = $"tests/{documentId:N}.pdf", FileSizeBytes = 128, OriginalFileSizeBytes = 128,
            UploadedAt = now, UpdatedAt = now, StructuredImportedAt = now
        });
        db.UserCvSections.Add(new UserCvSectionEntity
        {
            Id = sectionId, UserId = user.Id, UserCvDocumentId = documentId, Heading = "Experience",
            SectionType = CvSectionTypes.Experience, SortOrder = 0
        });
        db.UserCvEntries.Add(new UserCvEntryEntity
        {
            Id = Guid.NewGuid(), UserId = user.Id, SectionId = sectionId, Title = "Designer", Summary = "Designed services.",
            BulletsJson = "[]", TechStack = "", Source = "manual", SortOrder = 0
        });
        await db.SaveChangesAsync();
    }

    public void Dispose() { _app.Dispose(); _factory.Dispose(); }
}
