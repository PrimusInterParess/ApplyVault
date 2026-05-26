using ApplyVault.Api.Services.Eures;

namespace ApplyVault.Api.Tests;

public sealed class EuresJobRelevanceScorerTests
{
    [Fact]
    public void CalculateScore_TitleMatch_AddsThreePoints()
    {
        var score = EuresJobRelevanceScorer.CalculateScore(
            "Senior Backend Developer",
            "Contoso",
            "Build APIs",
            ["developer"]);

        Assert.Equal(3, score);
    }

    [Fact]
    public void CalculateScore_DescriptionMatchOnly_AddsTwoPoints()
    {
        var score = EuresJobRelevanceScorer.CalculateScore(
            "Engineer",
            "Contoso",
            "Looking for a backend developer with API experience",
            ["developer"]);

        Assert.Equal(2, score);
    }

    [Fact]
    public void CalculateScore_EmployerMatchOnly_AddsOnePoint()
    {
        var score = EuresJobRelevanceScorer.CalculateScore(
            "Engineer",
            "Developer Tools Inc",
            "General engineering role",
            ["developer"]);

        Assert.Equal(1, score);
    }

    [Fact]
    public void CalculateScore_DotNetKeyword_MatchesSynonymsInTitle()
    {
        var score = EuresJobRelevanceScorer.CalculateScore(
            "ASP.NET Core Engineer",
            "Fabrikam",
            "Cloud platform work",
            [".net"]);

        Assert.Equal(3, score);
    }

    [Fact]
    public void CalculateScore_DoesNotMatchPartialWordsInsideOtherTerms()
    {
        var score = EuresJobRelevanceScorer.CalculateScore(
            "Internet Marketing Specialist",
            "Contoso",
            "Promote online services",
            ["net"]);

        Assert.Equal(0, score);
    }

    [Fact]
    public void CalculateScore_MultipleKeywords_AccumulatesScores()
    {
        var score = EuresJobRelevanceScorer.CalculateScore(
            "Backend Developer",
            "Contoso",
            "Experience with C# required",
            ["backend", "c#"]);

        Assert.Equal(5, score);
    }
}
