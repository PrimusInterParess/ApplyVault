using ApplyVault.Api.Options;
using ApplyVault.Api.Services;
using Microsoft.Extensions.Configuration;

namespace ApplyVault.Api.Tests;

public sealed class CvImportAiOptionsTests
{
    [Fact]
    public void Defaults_ForceAiOff_AndGateThresholdsSet()
    {
        var options = new CvImportAiOptions();

        Assert.False(options.ForceAi);
        Assert.Equal(400, options.LowConfidenceMinBodyChars);
        Assert.Equal(120, options.SparseMaxAverageCharsPerPage);
        Assert.Equal(CvImportAiOptions.DefaultSystemPromptPreface, options.SystemPromptPreface);
        Assert.Contains("Deterministic structuring was insufficient", options.UserPromptTemplate, StringComparison.Ordinal);
        Assert.Contains("Do not invent", options.UserPromptTemplate, StringComparison.Ordinal);
        Assert.Contains("invoked only when deterministic", options.SystemPromptPreface, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigurationBind_ReadsAdditiveGateKeys()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CvImportAi:ForceAi"] = "true",
                ["CvImportAi:LowConfidenceMinBodyChars"] = "500",
                ["CvImportAi:SparseMaxAverageCharsPerPage"] = "180"
            })
            .Build();

        var options = new CvImportAiOptions();
        configuration.GetSection(CvImportAiOptions.SectionName).Bind(options);

        Assert.True(options.ForceAi);
        Assert.Equal(500, options.LowConfidenceMinBodyChars);
        Assert.Equal(180, options.SparseMaxAverageCharsPerPage);
    }

    [Fact]
    public void ComposeSystemPrompt_PrefixesCatalogRulesWithGatedPreface()
    {
        var composed = GoogleAiCvStructuredImportClient.ComposeSystemPrompt(
            "Gated preface. Use only source facts.",
            "Catalog rules: sectionType must be one of Experience.");

        Assert.StartsWith("Gated preface. Use only source facts.", composed, StringComparison.Ordinal);
        Assert.Contains("Catalog rules: sectionType must be one of Experience.", composed, StringComparison.Ordinal);
        Assert.Contains("\n\n", composed, StringComparison.Ordinal);
    }

    [Fact]
    public void ComposeSystemPrompt_EmptyPreface_ReturnsCatalogOnly()
    {
        var catalog = "Catalog only.";

        Assert.Equal(catalog, GoogleAiCvStructuredImportClient.ComposeSystemPrompt("  ", catalog));
    }
}
