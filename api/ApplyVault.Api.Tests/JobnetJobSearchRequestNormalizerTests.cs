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
            out var validationMessage,
            out var validationField);

        Assert.False(success);
        Assert.Equal("At least one keyword is required.", validationMessage);
        Assert.Equal(nameof(JobnetJobSearchRequest.Keywords), validationField);
    }

    [Fact]
    public void TryNormalizeSearchRequest_CanonicalizesPlace()
    {
        var normalizer = CreateNormalizer();

        var success = normalizer.TryNormalizeSearchRequest(
            new JobnetJobSearchRequest
            {
                Keyword = "developer",
                Place = "Aarhus"
            },
            out var normalized,
            out _,
            out _);

        Assert.True(success);
        Assert.Equal("aarhus", normalized.Place);
    }

    [Fact]
    public void TryNormalizeSearchRequest_RejectsUnknownPlace()
    {
        var normalizer = CreateNormalizer();

        var success = normalizer.TryNormalizeSearchRequest(
            new JobnetJobSearchRequest
            {
                Keyword = "developer",
                Place = "Berlin"
            },
            out _,
            out var validationMessage,
            out var validationField);

        Assert.False(success);
        Assert.Equal("Place must be Copenhagen, Aarhus, Odense, Aalborg, or Randers.", validationMessage);
        Assert.Equal(nameof(JobnetJobSearchRequest.Place), validationField);
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
            out _,
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
