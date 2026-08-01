using ApplyVault.Api.Options;
using ApplyVault.Api.Services;
using Microsoft.Extensions.Configuration;

namespace ApplyVault.Api.Tests;

public sealed class CvImportAiOptionsTests
{
    [Fact]
    public void Defaults_AiFirstPrompts_AndSparseThresholdSet()
    {
        var options = new CvImportAiOptions();

        Assert.Equal(120, options.SparseMaxAverageCharsPerPage);
        Assert.Equal(CvImportAiOptions.DefaultSystemPromptPreface, options.SystemPromptPreface);
        Assert.Contains("CONTACT IS MANDATORY", options.SystemPromptPreface, StringComparison.Ordinal);
        Assert.Contains("{{payload}}", options.UserPromptTemplate, StringComparison.Ordinal);
        Assert.Contains("extract Contact from the header", options.UserPromptTemplate, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Defaults_ContactChannelsNamedInPrompts()
    {
        var options = new CvImportAiOptions();

        Assert.Contains("title \"Name\"", options.SystemPromptPreface, StringComparison.Ordinal);
        Assert.Contains("LinkedIn", options.SystemPromptPreface, StringComparison.Ordinal);
        Assert.Contains("GitHub", options.SystemPromptPreface, StringComparison.Ordinal);
        Assert.Contains("Address", options.SystemPromptPreface, StringComparison.Ordinal);
        Assert.Contains("Never put contact details only inside Summary", options.SystemPromptPreface, StringComparison.Ordinal);

        Assert.Contains("Contact: REQUIRED", options.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Never bury Contact fields inside Summary", options.SystemPrompt, StringComparison.Ordinal);

        Assert.Contains("Do not leave Contact empty", options.UserPromptTemplate, StringComparison.Ordinal);
        Assert.Contains("Do not put them only in Summary", options.UserPromptTemplate, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfigurationBind_ReadsSparseThreshold()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CvImportAi:SparseMaxAverageCharsPerPage"] = "180"
            })
            .Build();

        var options = new CvImportAiOptions();
        configuration.GetSection(CvImportAiOptions.SectionName).Bind(options);

        Assert.Equal(180, options.SparseMaxAverageCharsPerPage);
    }

    [Fact]
    public void ComposeSystemPrompt_PrefixesCatalogRulesWithPreface()
    {
        var composed = GoogleAiCvStructuredImportClient.ComposeSystemPrompt(
            "CONTACT IS MANDATORY preface.",
            "Catalog rules: sectionType must be one of Experience.");

        Assert.StartsWith("CONTACT IS MANDATORY preface.", composed, StringComparison.Ordinal);
        Assert.Contains("Catalog rules: sectionType must be one of Experience.", composed, StringComparison.Ordinal);
    }

    [Fact]
    public void ComposeSystemPrompt_DefaultPreface_CarriesContactRules()
    {
        var composed = GoogleAiCvStructuredImportClient.ComposeSystemPrompt(
            CvImportAiOptions.DefaultSystemPromptPreface,
            "Catalog rules follow.");

        Assert.Contains("CONTACT IS MANDATORY", composed, StringComparison.Ordinal);
        Assert.Contains("LinkedIn", composed, StringComparison.Ordinal);
        Assert.Contains("Catalog rules follow.", composed, StringComparison.Ordinal);
    }

    [Fact]
    public void ComposeSystemPrompt_EmptyPreface_ReturnsCatalogOnly()
    {
        var catalog = "Catalog only.";

        Assert.Equal(catalog, GoogleAiCvStructuredImportClient.ComposeSystemPrompt("  ", catalog));
    }

    [Fact]
    public void ApplyUserPayload_PrefersPayloadPlaceholder()
    {
        var result = GoogleAiCvStructuredImportClient.ApplyUserPayload(
            "Before\n{{payload}}\nAfter",
            "Alice\nEngineer");

        Assert.Equal("Before\nAlice\nEngineer\nAfter", result);
    }

    [Fact]
    public void ApplyUserPayload_FallsBackToLegacyPayloadJsonPlaceholder()
    {
        var result = GoogleAiCvStructuredImportClient.ApplyUserPayload(
            "Payload:\n{{payloadJson}}",
            "full text");

        Assert.Equal("Payload:\nfull text", result);
    }
}
