using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services.Eures;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Tests;

public sealed class EuresJobSearchRequestNormalizerTests
{
    private static EuresJobSearchRequestNormalizer CreateNormalizer(int maxResultsPerPage = 50) =>
        new(Microsoft.Extensions.Options.Options.Create(new EuresIntegrationOptions
        {
            DefaultLocationCode = "dk",
            MaxResultsPerPage = maxResultsPerPage
        }));

    [Fact]
    public void TryNormalizeSearchRequest_NoKeywords_ReturnsValidationError()
    {
        var normalizer = CreateNormalizer();
        var request = new EuresJobSearchRequest();

        var success = normalizer.TryNormalizeSearchRequest(request, out _, out var message);

        Assert.False(success);
        Assert.Equal("At least one keyword is required.", message);
    }

    [Fact]
    public void TryNormalizeSearchRequest_SingleKeyword_NormalizesDefaults()
    {
        var normalizer = CreateNormalizer();
        var request = new EuresJobSearchRequest
        {
            Keyword = "  developer  ",
            Page = 0,
            ResultsPerPage = 100,
            RequestLanguage = "  da  "
        };

        var success = normalizer.TryNormalizeSearchRequest(request, out var normalized, out var message);

        Assert.True(success);
        Assert.Equal(string.Empty, message);
        Assert.Equal(["developer"], normalized.Keywords);
        Assert.Equal("developer", normalized.Keyword);
        Assert.Equal("dk", normalized.LocationCode);
        Assert.Equal(1, normalized.Page);
        Assert.Equal(50, normalized.ResultsPerPage);
        Assert.Equal("da", normalized.RequestLanguage);
        Assert.Equal("MOST_RECENT", normalized.SortSearch);
    }

    [Fact]
    public void TryNormalizeSearchRequest_MultipleKeywords_ForcesBestMatchSort()
    {
        var normalizer = CreateNormalizer();
        var request = new EuresJobSearchRequest
        {
            Keywords = [" .NET ", "dotnet", "C#"],
            SortSearch = "MOST_RECENT",
            LocationCode = " se "
        };

        var success = normalizer.TryNormalizeSearchRequest(request, out var normalized, out _);

        Assert.True(success);
        Assert.Equal([".NET", "dotnet", "C#"], normalized.Keywords);
        Assert.Null(normalized.Keyword);
        Assert.Equal("se", normalized.LocationCode);
        Assert.Equal("BEST_MATCH", normalized.SortSearch);
    }

    [Fact]
    public void TryNormalizeSearchRequest_ResultsPerPage_IsClampedToMinimumOne()
    {
        var normalizer = CreateNormalizer(maxResultsPerPage: 10);
        var request = new EuresJobSearchRequest
        {
            Keyword = "developer",
            ResultsPerPage = 0
        };

        normalizer.TryNormalizeSearchRequest(request, out var normalized, out _);

        Assert.Equal(1, normalized.ResultsPerPage);
    }

    [Theory]
    [InlineData(null, "en")]
    [InlineData("", "en")]
    [InlineData("  fr  ", "fr")]
    public void NormalizeRequestLanguage_ReturnsTrimmedDefault(string? input, string expected)
    {
        var normalizer = CreateNormalizer();

        Assert.Equal(expected, normalizer.NormalizeRequestLanguage(input));
    }
}
