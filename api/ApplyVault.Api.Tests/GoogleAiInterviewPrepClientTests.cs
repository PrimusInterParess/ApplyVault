using System.Net;
using System.Text;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services;

namespace ApplyVault.Api.Tests;

public sealed class GoogleAiInterviewPrepClientTests
{
    [Fact]
    public void NormalizeScorecard_KeepsExactFiveDimensionsInStableOrder()
    {
        var scorecard = GoogleAiInterviewPrepClient.NormalizeScorecard(
            new InterviewPrepAiRawScorecard(
                88,
                " Solid round ",
                [
                    new InterviewPrepAiRawScorecardDimension("language", 70, "Clear EN"),
                    new InterviewPrepAiRawScorecardDimension("clarity", 90, "Clear answers"),
                    new InterviewPrepAiRawScorecardDimension("relevance", 99, "Drop unknown"),
                    new InterviewPrepAiRawScorecardDimension("evidence", 85, "Good examples"),
                    new InterviewPrepAiRawScorecardDimension("STRUCTURE", 80, "Organized")
                ]));

        Assert.Equal(88, scorecard.Overall);
        Assert.Equal("Solid round", scorecard.Summary);
        Assert.Equal(GoogleAiInterviewPrepClient.ScorecardDimensionIds, scorecard.Dimensions.Select((d) => d.Id));
        Assert.Equal(90, scorecard.Dimensions[0].Score);
        Assert.Equal(85, scorecard.Dimensions[1].Score);
        Assert.Equal(80, scorecard.Dimensions[2].Score);
        Assert.Equal(88, scorecard.Dimensions[3].Score); // role_fit filled from overall
        Assert.Equal("No dimension note returned.", scorecard.Dimensions[3].Note);
        Assert.Equal(70, scorecard.Dimensions[4].Score);
        Assert.DoesNotContain(scorecard.Dimensions, (d) => d.Id == "relevance");
    }

    [Fact]
    public void Normalize_ParsesInferenceAndNullScorecard()
    {
        var result = GoogleAiInterviewPrepClient.Normalize(
            new InterviewPrepAiRawResponse(
                "debrief",
                new InterviewPrepAiRawInference("Pediatric nurse", "mid", "competency_behavioral", false),
                "  Strong STAR story.  ",
                null,
                ["What was the outcome?"],
                ["Keep quantifying impact"]));

        Assert.Equal("debrief", result.Phase);
        Assert.Equal("Pediatric nurse", result.Inference.Role);
        Assert.Equal("mid", result.Inference.Seniority);
        Assert.Equal("competency_behavioral", result.Inference.InterviewStyle);
        Assert.False(result.Inference.IsTechnicalContext);
        Assert.Equal("Strong STAR story.", result.CoachMessage);
        Assert.Null(result.Scorecard);
        Assert.Equal(["What was the outcome?"], result.FollowUps);
        Assert.Equal(["Keep quantifying impact"], result.DebriefBullets);
    }

    [Fact]
    public void Normalize_FillsMissingListsAndClampsScores()
    {
        var result = GoogleAiInterviewPrepClient.Normalize(
            new InterviewPrepAiRawResponse(
                "interview",
                new InterviewPrepAiRawInference("Warehouse supervisor", "senior", "process", true),
                "Walk me through a shift handover.",
                new InterviewPrepAiRawScorecard(
                    140,
                    null,
                    [
                        new InterviewPrepAiRawScorecardDimension("clarity", -5, "  "),
                        new InterviewPrepAiRawScorecardDimension("evidence", 55, "Concrete"),
                        new InterviewPrepAiRawScorecardDimension("structure", 60, "Ok"),
                        new InterviewPrepAiRawScorecardDimension("role_fit", 70, "Fits"),
                        new InterviewPrepAiRawScorecardDimension("language", 75, "Fluent")
                    ]),
                null,
                null));

        Assert.Equal([], result.FollowUps);
        Assert.Equal([], result.DebriefBullets);
        Assert.NotNull(result.Scorecard);
        Assert.Equal(100, result.Scorecard!.Overall);
        Assert.Equal(0, result.Scorecard.Dimensions[0].Score);
        Assert.Equal("No dimension note returned.", result.Scorecard.Dimensions[0].Note);
        Assert.True(result.Inference.IsTechnicalContext);
    }

    [Fact]
    public void Normalize_RejectsMissingCoachMessage()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            GoogleAiInterviewPrepClient.Normalize(
                new InterviewPrepAiRawResponse(
                    "interview",
                    new InterviewPrepAiRawInference("Nurse", "mid", "screening", false),
                    "  ",
                    null,
                    [],
                    [])));

        Assert.Contains("coachMessage", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateTurnAsync_RejectsWhenGoogleAiDisabled()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            throw new InvalidOperationException("HTTP should not be called when AI is disabled.")));
        var client = CreateClient(httpClient, enabled: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GenerateTurnAsync(CreateRequest()));

        Assert.Contains("Google AI is disabled", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateTurnAsync_BuildsProfessionAgnosticPromptAndParsesSchemaResponse()
    {
        string? requestBody = null;
        var handler = new StubHttpMessageHandler(async (request) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync();
            Assert.Contains("generativelanguage.googleapis.com", request.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
            Assert.Contains("key=test-key", request.RequestUri.Query, StringComparison.Ordinal);

            return JsonResponse("""
                {
                  "candidates": [
                    {
                      "content": {
                        "parts": [
                          {
                            "text": "{\"phase\":\"interview\",\"inference\":{\"role\":\"Pediatric nurse\",\"seniority\":\"mid\",\"interviewStyle\":\"competency_behavioral\",\"isTechnicalContext\":false},\"coachMessage\":\"Tell me about a difficult patient handoff.\",\"scorecard\":null,\"followUps\":[\"What was the outcome?\"],\"debriefBullets\":[]}"
                          }
                        ]
                      }
                    }
                  ]
                }
                """);
        });

        using var httpClient = new HttpClient(handler);
        var client = CreateClient(httpClient, enabled: true);

        var result = await client.GenerateTurnAsync(CreateRequest(
            mode: "behavioral",
            languageMix: "en",
            userMessage: "Ready to begin.",
            job: new InterviewPrepJobContext(
                "City Hospital",
                "Pediatric nurse",
                "Copenhagen",
                "Ward nursing role",
                "Care for pediatric patients.")));

        Assert.Equal("interview", result.Phase);
        Assert.Equal("Pediatric nurse", result.Inference.Role);
        Assert.False(result.Inference.IsTechnicalContext);
        Assert.Equal("Tell me about a difficult patient handoff.", result.CoachMessage);
        Assert.Null(result.Scorecard);
        Assert.Equal(["What was the outcome?"], result.FollowUps);
        Assert.Equal([], result.DebriefBullets);

        Assert.False(string.IsNullOrWhiteSpace(requestBody));
        Assert.Contains("Do NOT default to software engineering", requestBody, StringComparison.Ordinal);
        Assert.Contains("Mode: behavioral", requestBody, StringComparison.Ordinal);
        Assert.Contains("Language mix: en", requestBody, StringComparison.Ordinal);
        Assert.Contains("Ready to begin.", requestBody, StringComparison.Ordinal);
        Assert.Contains("Pediatric nurse", requestBody, StringComparison.Ordinal);
        Assert.Contains("City Hospital", requestBody, StringComparison.Ordinal);
        Assert.Contains("companyName", requestBody, StringComparison.Ordinal);
        Assert.Contains("jobTitle", requestBody, StringComparison.Ordinal);
        Assert.Contains("isTechnicalContext", requestBody, StringComparison.Ordinal);
        Assert.Contains("role_fit", requestBody, StringComparison.Ordinal);
        Assert.Contains("never default to software engineer", requestBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InterviewPrepAiOptions_DefaultsAreProfessionAgnostic()
    {
        var options = new InterviewPrepAiOptions();

        Assert.Equal(InterviewPrepAiOptions.SectionName, "InterviewPrepAi");
        Assert.Equal(12, options.MaxPriorTurns);
        Assert.Equal(4_000, options.MaxUserMessageChars);
        Assert.Equal(2_000, options.MaxPriorTurnChars);
        Assert.Equal("en", options.DefaultLanguageMix);
        Assert.Contains("Do NOT default to software engineering", options.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("{{mode}}", options.UserPromptTemplate, StringComparison.Ordinal);
        Assert.Contains("{{languageMix}}", options.UserPromptTemplate, StringComparison.Ordinal);
        Assert.Contains("{{cvJson}}", options.UserPromptTemplate, StringComparison.Ordinal);
        Assert.Contains("{{jobJson}}", options.UserPromptTemplate, StringComparison.Ordinal);
        Assert.Contains("{{priorTurnsJson}}", options.UserPromptTemplate, StringComparison.Ordinal);
        Assert.Contains("{{userMessage}}", options.UserPromptTemplate, StringComparison.Ordinal);
        Assert.Contains("isTechnicalContext", options.SystemPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("You are a software", options.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    private static GoogleAiInterviewPrepClient CreateClient(HttpClient httpClient, bool enabled) =>
        new(
            httpClient,
            Microsoft.Extensions.Options.Options.Create(new GoogleAiOptions
            {
                Enabled = enabled,
                ApiKey = "test-key",
                Model = "gemini-2.5-flash",
                TimeoutSeconds = 30
            }),
            Microsoft.Extensions.Options.Options.Create(new InterviewPrepAiOptions()));

    private static InterviewPrepAiTurnRequest CreateRequest(
        string mode = "behavioral",
        string? languageMix = "en",
        string userMessage = "Let's start.",
        InterviewPrepJobContext? job = null) =>
        new(
            new CvStructuredDocumentDto(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                [
                    new CvStructuredSectionDto(
                        Guid.NewGuid(),
                        "Experience",
                        "experience",
                        0,
                        [
                            new CvStructuredEntryDto(
                                Guid.NewGuid(),
                                "Pediatric nurse",
                                "City Hospital",
                                "2020–Present",
                                "Ward care and handoffs",
                                ["Led pediatric shift handoffs"],
                                string.Empty,
                                new Dictionary<string, object?>(),
                                "manual",
                                null,
                                0)
                        ])
                ]),
            job,
            mode,
            languageMix,
            userMessage,
            [
                new InterviewPrepPriorTurn("user", "Ready when you are.", "interview"),
                new InterviewPrepPriorTurn("coach", "Great — we'll focus on behavioral stories.", "interview")
            ]);

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            responder(request);
    }
}
