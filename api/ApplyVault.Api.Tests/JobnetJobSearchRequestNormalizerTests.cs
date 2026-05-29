using ApplyVault.Api.Models;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services.Jobnet;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Tests;

public sealed class JobnetJobSearchRequestNormalizerTests
{
    [Fact]
    public void TryNormalizeSearchRequest_RejectsEmptyKeywords()
    {
        var normalizer = CreateNormalizer();

        var success = normalizer.TryNormalizeSearchRequest(
            new JobnetJobSearchRequest(),
            out _,
            out var validationMessage);

        Assert.False(success);
        Assert.Equal("At least one keyword is required.", validationMessage);
    }

    [Fact]
    public void TryNormalizeSearchRequest_ClampsResultsPerPage()
    {
        var normalizer = CreateNormalizer();

        var success = normalizer.TryNormalizeSearchRequest(
            new JobnetJobSearchRequest
            {
                Keyword = "developer",
                ResultsPerPage = 500,
                Page = 0
            },
            out var normalized,
            out _);

        Assert.True(success);
        Assert.Equal(50, normalized.ResultsPerPage);
        Assert.Equal(1, normalized.Page);
        Assert.Equal("developer", normalized.Keyword);
    }

    private static JobnetJobSearchRequestNormalizer CreateNormalizer()
    {
        return new JobnetJobSearchRequestNormalizer(Microsoft.Extensions.Options.Options.Create(new JobnetIntegrationOptions
        {
            MaxResultsPerPage = 50
        }));
    }
}
