using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services;

namespace ApplyVault.Api.Tests;

public sealed class CvStructuredImportAiGateTests
{
    [Fact]
    public void Decide_SkipsWhenGoogleAiDisabled()
    {
        var decision = CvStructuredImportAiGate.Decide(
            googleAiEnabled: false,
            CvPdfExtractionQuality.Good,
            CreateStrongRawSections(),
            CreateStrongHeuristicSections(),
            new CvImportAiOptions());

        Assert.Equal(CvStructuredImportAiGateDecision.SkipAi, decision);
    }

    [Fact]
    public void Decide_SkipsWhenEmptyExtraction()
    {
        var decision = CvStructuredImportAiGate.Decide(
            googleAiEnabled: true,
            CvPdfExtractionQuality.Empty,
            [],
            [],
            new CvImportAiOptions());

        Assert.Equal(CvStructuredImportAiGateDecision.SkipAi, decision);
    }

    [Fact]
    public void Decide_SkipsWhenHeuristicConfidenceHigh()
    {
        var decision = CvStructuredImportAiGate.Decide(
            googleAiEnabled: true,
            CvPdfExtractionQuality.Good,
            CreateStrongRawSections(),
            CreateStrongHeuristicSections(),
            new CvImportAiOptions());

        Assert.Equal(CvStructuredImportAiGateDecision.SkipAi, decision);
    }

    [Fact]
    public void Decide_CallsWhenForceAi()
    {
        var decision = CvStructuredImportAiGate.Decide(
            googleAiEnabled: true,
            CvPdfExtractionQuality.Good,
            CreateStrongRawSections(),
            CreateStrongHeuristicSections(),
            new CvImportAiOptions { ForceAi = true });

        Assert.Equal(CvStructuredImportAiGateDecision.CallAi, decision);
    }

    [Fact]
    public void Decide_CallsWhenSparse()
    {
        var decision = CvStructuredImportAiGate.Decide(
            googleAiEnabled: true,
            CvPdfExtractionQuality.Sparse,
            CreateStrongRawSections(),
            CreateStrongHeuristicSections(),
            new CvImportAiOptions());

        Assert.Equal(CvStructuredImportAiGateDecision.CallAi, decision);
    }

    [Fact]
    public void Decide_CallsWhenHeuristicSectionsEmpty()
    {
        var decision = CvStructuredImportAiGate.Decide(
            googleAiEnabled: true,
            CvPdfExtractionQuality.Good,
            CreateStrongRawSections(),
            [],
            new CvImportAiOptions());

        Assert.Equal(CvStructuredImportAiGateDecision.CallAi, decision);
    }

    [Fact]
    public void Decide_CallsWhenOnlyLargeProfileBucket()
    {
        var body = new string('x', 500);
        var raw = new[]
        {
            new CvPdfRawSection("Profile", "summary", 0, body)
        };

        var heuristic = new[]
        {
            new CvStructuredSectionWriteDto(
                null,
                "Summary",
                CvSectionTypes.Summary,
                0,
                [
                    new CvStructuredEntryWriteDto(
                        null,
                        string.Empty,
                        null,
                        null,
                        body,
                        [],
                        string.Empty,
                        CvEntrySources.Import,
                        null,
                        0)
                ])
        };

        var decision = CvStructuredImportAiGate.Decide(
            googleAiEnabled: true,
            CvPdfExtractionQuality.Good,
            raw,
            heuristic,
            new CvImportAiOptions { LowConfidenceMinBodyChars = 400 });

        Assert.Equal(CvStructuredImportAiGateDecision.CallAi, decision);
    }

    [Fact]
    public void Decide_CallsWhenExperienceCueMissingTypedSection()
    {
        var raw = new[]
        {
            new CvPdfRawSection(
                "Profile",
                "summary",
                0,
                """
                Experience
                Software Engineer at Acme
                Built services.
                """)
        };

        var heuristic = new[]
        {
            new CvStructuredSectionWriteDto(
                null,
                "Summary",
                CvSectionTypes.Summary,
                0,
                [
                    new CvStructuredEntryWriteDto(
                        null,
                        string.Empty,
                        null,
                        null,
                        "Experience\nSoftware Engineer at Acme\nBuilt services.",
                        [],
                        string.Empty,
                        CvEntrySources.Import,
                        null,
                        0)
                ])
        };

        var decision = CvStructuredImportAiGate.Decide(
            googleAiEnabled: true,
            CvPdfExtractionQuality.Good,
            raw,
            heuristic,
            new CvImportAiOptions());

        Assert.Equal(CvStructuredImportAiGateDecision.CallAi, decision);
    }

    private static IReadOnlyList<CvPdfRawSection> CreateStrongRawSections() =>
    [
        new CvPdfRawSection("Summary", "summary", 0, "Experienced software engineer."),
        new CvPdfRawSection(
            "Experience",
            "experience",
            0,
            """
            Software Engineer
            Acme Corp
            2020 – 2024
            Built reliable services.
            """),
        new CvPdfRawSection("Skills", "skills", 0, "Languages: English, Danish")
    ];

    private static IReadOnlyList<CvStructuredSectionWriteDto> CreateStrongHeuristicSections() =>
    [
        new CvStructuredSectionWriteDto(
            null,
            "Summary",
            CvSectionTypes.Summary,
            0,
            [
                new CvStructuredEntryWriteDto(
                    null,
                    string.Empty,
                    null,
                    null,
                    "Experienced software engineer.",
                    [],
                    string.Empty,
                    CvEntrySources.Import,
                    null,
                    0)
            ]),
        new CvStructuredSectionWriteDto(
            null,
            "Experience",
            CvSectionTypes.Experience,
            1,
            [
                new CvStructuredEntryWriteDto(
                    null,
                    "Software Engineer",
                    "Acme Corp",
                    "2020 – 2024",
                    "Built reliable services.",
                    [],
                    string.Empty,
                    CvEntrySources.Import,
                    null,
                    0)
            ]),
        new CvStructuredSectionWriteDto(
            null,
            "Skills",
            CvSectionTypes.Skills,
            2,
            [
                new CvStructuredEntryWriteDto(
                    null,
                    "Languages",
                    null,
                    null,
                    string.Empty,
                    [],
                    "English, Danish",
                    CvEntrySources.Import,
                    null,
                    0)
            ])
    ];
}
