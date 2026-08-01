using System.Net;
using System.Text;
using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services;

namespace ApplyVault.Api.Tests;

public sealed class GoogleAiCvStructuredSummaryProposeClientTests
{
    [Fact]
    public void Normalize_TrimsSummaryAndCapsChangeBullets()
    {
        var longBullet = new string('x', GoogleAiCvStructuredSummaryProposeClient.MaxBulletLength + 40);
        var result = GoogleAiCvStructuredSummaryProposeClient.Normalize(
            new CvStructuredSummaryProposeAiResponse(
                "  Proposed summary prose.  ",
                [
                    "  First change  ",
                    " ",
                    "Second change",
                    "Third",
                    longBullet,
                    "Fifth",
                    "Sixth should drop"
                ]));

        Assert.Equal("Proposed summary prose.", result.ProposedSummaryText);
        Assert.Equal(5, result.ChangeBullets.Count);
        Assert.Equal("First change", result.ChangeBullets[0]);
        Assert.Equal("Second change", result.ChangeBullets[1]);
        Assert.Equal(GoogleAiCvStructuredSummaryProposeClient.MaxBulletLength, result.ChangeBullets[3].Length);
        Assert.DoesNotContain(result.ChangeBullets, (bullet) => bullet.Contains("Sixth", StringComparison.Ordinal));
        Assert.All(
            result.ChangeBullets,
            (bullet) => Assert.True(bullet.Length <= GoogleAiCvStructuredSummaryProposeClient.MaxBulletLength));
    }

    [Fact]
    public void Normalize_RejectsEmptyProposedSummary()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            GoogleAiCvStructuredSummaryProposeClient.Normalize(
                new CvStructuredSummaryProposeAiResponse("   ", ["Changed tone"])));

        Assert.Contains("empty proposed summary", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Normalize_RejectsMissingChangeBullets()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            GoogleAiCvStructuredSummaryProposeClient.Normalize(
                new CvStructuredSummaryProposeAiResponse("Valid summary", [" ", ""])));

        Assert.Contains("change bullets", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExtractCurrentSummaryText_UsesFirstSummaryBySortOrder()
    {
        var document = new CvStructuredDocumentDto(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            [
                new CvStructuredSectionDto(
                    Guid.NewGuid(),
                    "Later summary",
                    CvSectionTypes.Summary,
                    2,
                    [
                        new CvStructuredEntryDto(
                            Guid.NewGuid(),
                            "Summary",
                            null,
                            null,
                            "Later text",
                            [],
                            string.Empty,
                            new Dictionary<string, object?>(),
                            CvEntrySources.Manual,
                            null,
                            0)
                    ]),
                new CvStructuredSectionDto(
                    Guid.NewGuid(),
                    "Primary summary",
                    CvSectionTypes.Summary,
                    0,
                    [
                        new CvStructuredEntryDto(
                            Guid.NewGuid(),
                            "Summary",
                            null,
                            null,
                            "  Primary text  ",
                            [],
                            string.Empty,
                            new Dictionary<string, object?>(),
                            CvEntrySources.Manual,
                            null,
                            0)
                    ])
            ]);

        Assert.Equal(
            "Primary text",
            GoogleAiCvStructuredSummaryProposeClient.ExtractCurrentSummaryText(document));
    }

    [Fact]
    public async Task ProposeAsync_BuildsPromptWithIdentityAndParsesSchemaResponse()
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
                            "text": "{\"proposedSummaryText\":\"Backend lead with cloud delivery.\",\"changeBullets\":[\"Emphasized backend leadership\",\"Tightened length\"]}"
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

        var document = CreateDocumentWithContactAndSummary();
        var result = await client.ProposeAsync(
            document,
            "  Emphasize backend leadership  ",
            "App User Name",
            "appuser@example.com");

        Assert.Equal("Backend lead with cloud delivery.", result.ProposedSummaryText);
        Assert.Equal(["Emphasized backend leadership", "Tightened length"], result.ChangeBullets);
        Assert.False(string.IsNullOrWhiteSpace(requestBody));
        Assert.Contains("Emphasize backend leadership", requestBody, StringComparison.Ordinal);
        Assert.Contains("Current summary body", requestBody, StringComparison.Ordinal);
        Assert.Contains("Contact Person", requestBody, StringComparison.Ordinal);
        Assert.Contains("contact@example.com", requestBody, StringComparison.Ordinal);
        Assert.Contains("appuser@example.com", requestBody, StringComparison.Ordinal);
        Assert.Contains("Prefer Contact name/email", requestBody, StringComparison.Ordinal);
        Assert.Contains("proposedSummaryText", requestBody, StringComparison.Ordinal);
        Assert.Contains("changeBullets", requestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProposeAsync_RejectsWhenGoogleAiDisabled()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            throw new InvalidOperationException("HTTP should not be called when AI is disabled.")));
        var client = CreateClient(httpClient, enabled: false);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.ProposeAsync(CreateDocumentWithContactAndSummary(), null, null, null));

        Assert.Contains("Google AI is disabled", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FakeClient_IsUsableForBackendServiceTests()
    {
        var document = CreateDocumentWithContactAndSummary();
        var fake = new FakeCvStructuredSummaryProposeAiClient
        {
            Result = new CvSummaryProposeAiResult(
                "Fake proposed summary",
                ["Created initial summary from CV"])
        };

        var result = await fake.ProposeAsync(
            document,
            instructions: null,
            appUserDisplayName: "Display",
            appUserEmail: "display@example.com");

        Assert.Same(document, fake.LastCurrent);
        Assert.Null(fake.LastInstructions);
        Assert.Equal("Display", fake.LastAppUserDisplayName);
        Assert.Equal("display@example.com", fake.LastAppUserEmail);
        Assert.Equal("Fake proposed summary", result.ProposedSummaryText);
        Assert.Equal(["Created initial summary from CV"], result.ChangeBullets);
        Assert.False(fake.SaveWasCalled);
    }

    private static GoogleAiCvStructuredSummaryProposeClient CreateClient(HttpClient httpClient, bool enabled) =>
        new(
            httpClient,
            Microsoft.Extensions.Options.Options.Create(new GoogleAiOptions
            {
                Enabled = enabled,
                ApiKey = "test-key",
                Model = "gemini-2.5-flash",
                TimeoutSeconds = 30
            }),
            Microsoft.Extensions.Options.Options.Create(new CvSummaryProposeAiOptions()));

    private static CvStructuredDocumentDto CreateDocumentWithContactAndSummary() =>
        new(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            [
                new CvStructuredSectionDto(
                    Guid.NewGuid(),
                    "Contact",
                    CvSectionTypes.Contact,
                    0,
                    [
                        new CvStructuredEntryDto(
                            Guid.NewGuid(),
                            "Contact Person",
                            "Sofia, Bulgaria",
                            null,
                            string.Empty,
                            ["contact@example.com", "+359 88 000 0000"],
                            string.Empty,
                            new Dictionary<string, object?>(),
                            CvEntrySources.Import,
                            null,
                            0)
                    ]),
                new CvStructuredSectionDto(
                    Guid.NewGuid(),
                    "Summary",
                    CvSectionTypes.Summary,
                    1,
                    [
                        new CvStructuredEntryDto(
                            Guid.NewGuid(),
                            "Summary",
                            null,
                            null,
                            "Current summary body",
                            [],
                            string.Empty,
                            new Dictionary<string, object?>(),
                            CvEntrySources.Manual,
                            null,
                            0)
                    ])
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

/// <summary>
/// Test double for backend service tests — ephemeral propose only; never persists.
/// </summary>
public sealed class FakeCvStructuredSummaryProposeAiClient : ICvStructuredSummaryProposeAiClient
{
    public CvSummaryProposeAiResult Result { get; set; } =
        new("Proposed summary", ["Updated summary focus"]);

    public CvStructuredDocumentDto? LastCurrent { get; private set; }
    public string? LastInstructions { get; private set; }
    public string? LastAppUserDisplayName { get; private set; }
    public string? LastAppUserEmail { get; private set; }
    public bool SaveWasCalled { get; private set; }

    public Task<CvSummaryProposeAiResult> ProposeAsync(
        CvStructuredDocumentDto current,
        string? instructions,
        string? appUserDisplayName,
        string? appUserEmail,
        CancellationToken cancellationToken = default)
    {
        LastCurrent = current;
        LastInstructions = instructions;
        LastAppUserDisplayName = appUserDisplayName;
        LastAppUserEmail = appUserEmail;
        return Task.FromResult(Result);
    }
}
