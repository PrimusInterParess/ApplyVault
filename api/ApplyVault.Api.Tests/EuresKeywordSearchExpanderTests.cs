using ApplyVault.Api.Services.Eures;

namespace ApplyVault.Api.Tests;

public sealed class EuresKeywordSearchExpanderTests
{
    [Fact]
    public void ExpandSearchTerms_DotNetKeyword_ExpandsToRelatedTerms()
    {
        var terms = EuresKeywordSearchExpander.ExpandSearchTerms([".net"]);

        Assert.Equal([".NET", "dotnet", "C#", "Blazor"], terms);
    }

    [Fact]
    public void ExpandSearchTerms_GenericKeyword_ReturnsTrimmedKeyword()
    {
        var terms = EuresKeywordSearchExpander.ExpandSearchTerms(["  backend developer  "]);

        Assert.Equal(["backend developer"], terms);
    }

    [Fact]
    public void ExpandSearchTerms_DuplicateExpandedTerms_AreDistinct()
    {
        var terms = EuresKeywordSearchExpander.ExpandSearchTerms([".net", "dotnet"]);

        Assert.Equal([".NET", "dotnet", "C#", "Blazor"], terms);
    }

    [Theory]
    [InlineData("  keyword  ", "keyword")]
    public void NormalizeForSearch_TrimsInput(string input, string expected)
    {
        Assert.Equal(expected, EuresKeywordSearchExpander.NormalizeForSearch(input));
    }
}
