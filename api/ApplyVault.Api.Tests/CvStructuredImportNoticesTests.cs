using ApplyVault.Api.Models;
using ApplyVault.Api.Services;

namespace ApplyVault.Api.Tests;

public sealed class CvStructuredImportNoticesTests
{
    [Fact]
    public void Build_EmptyExtraction_ReturnsClearNoticeWithoutOcrWording()
    {
        var notice = CvStructuredImportNotices.Build(
            CvPdfExtractionQuality.Empty,
            [],
            [],
            usedAi: false,
            aiAttempted: false,
            aiFailed: false);

        Assert.Equal(CvStructuredImportNotices.EmptyExtraction, notice);
        Assert.DoesNotContain("OCR", notice, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Google AI", notice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_QuietSuccess_ReturnsNull()
    {
        var sections = CreateStrongSections();

        var notice = CvStructuredImportNotices.Build(
            CvPdfExtractionQuality.Good,
            sections,
            sections,
            usedAi: false,
            aiAttempted: false,
            aiFailed: false);

        Assert.Null(notice);
    }

    [Fact]
    public void Build_AiUsedSuccess_OmitsAiAssistedNotice()
    {
        var sections = CreateStrongSections();

        var notice = CvStructuredImportNotices.Build(
            CvPdfExtractionQuality.Good,
            sections,
            sections,
            usedAi: true,
            aiAttempted: true,
            aiFailed: false);

        Assert.Null(notice);
    }

    [Fact]
    public void Build_SparseWeakHeuristic_ReturnsIncompleteNotice()
    {
        var weak = new[]
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
                        "Short",
                        [],
                        string.Empty,
                        CvEntrySources.Import,
                        null,
                        0)
                ])
        };

        var notice = CvStructuredImportNotices.Build(
            CvPdfExtractionQuality.Sparse,
            weak,
            weak,
            usedAi: false,
            aiAttempted: false,
            aiFailed: false);

        Assert.Equal(CvStructuredImportNotices.IncompleteReview, notice);
        Assert.DoesNotContain("enable", notice, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("may not have been imported", notice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_CatchAllCustom_ReturnsIncompleteNotice()
    {
        var sections = CreateStrongSections();

        var notice = CvStructuredImportNotices.Build(
            CvPdfExtractionQuality.Good,
            sections,
            sections,
            usedAi: false,
            aiAttempted: false,
            aiFailed: false,
            usedCatchAllCustom: true);

        Assert.Equal(CvStructuredImportNotices.IncompleteReview, notice);
        Assert.DoesNotContain("lines missing", notice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_AiFailedWithWeakHeuristic_ReturnsIncompleteNotice()
    {
        var weak = new[]
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
                        "Only summary",
                        [],
                        string.Empty,
                        CvEntrySources.Import,
                        null,
                        0)
                ])
        };

        var notice = CvStructuredImportNotices.Build(
            CvPdfExtractionQuality.Good,
            weak,
            weak,
            usedAi: false,
            aiAttempted: true,
            aiFailed: true);

        Assert.Equal(CvStructuredImportNotices.IncompleteReview, notice);
    }

    private static IReadOnlyList<CvStructuredSectionWriteDto> CreateStrongSections() =>
    [
        new CvStructuredSectionWriteDto(
            null,
            "Experience",
            CvSectionTypes.Experience,
            0,
            [
                new CvStructuredEntryWriteDto(
                    null,
                    "Software Engineer",
                    "Acme",
                    "2020 – 2024",
                    "Built services.",
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
            1,
            [
                new CvStructuredEntryWriteDto(
                    null,
                    "Languages",
                    null,
                    null,
                    string.Empty,
                    [],
                    "English",
                    CvEntrySources.Import,
                    null,
                    0)
            ])
    ];
}
