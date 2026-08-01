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
    public void Defaults_Phase3PromptAlignment_ContactCustomAndAtomicLinks()
    {
        var options = new CvImportAiOptions();

        Assert.Contains("Contact is a first-class sectionType", options.SystemPromptPreface, StringComparison.Ordinal);
        Assert.Contains("Never omit source lines", options.SystemPromptPreface, StringComparison.Ordinal);
        Assert.Contains("Additional information", options.SystemPromptPreface, StringComparison.Ordinal);
        Assert.Contains("single atomic tokens", options.SystemPromptPreface, StringComparison.Ordinal);
        Assert.Contains("never split on \"/\"", options.SystemPromptPreface, StringComparison.Ordinal);

        Assert.Contains("sectionType must be one of: Experience, Projects, Education, Skills, Summary, Contact, Custom", options.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("contact/contact information -> Contact (first-class", options.SystemPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("contact/contact information -> Custom with heading \"Contact\"", options.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("sectionType Contact", options.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("single atomic tokens", options.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("Additional information", options.SystemPrompt, StringComparison.Ordinal);

        Assert.Contains("sectionType Contact", options.UserPromptTemplate, StringComparison.Ordinal);
        Assert.Contains("Never omit source lines", options.UserPromptTemplate, StringComparison.Ordinal);
        Assert.Contains("single atomic tokens", options.UserPromptTemplate, StringComparison.Ordinal);
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
    public void ComposeSystemPrompt_DefaultPreface_CarriesPhase3PlacementRules()
    {
        var composed = GoogleAiCvStructuredImportClient.ComposeSystemPrompt(
            CvImportAiOptions.DefaultSystemPromptPreface,
            "Catalog rules follow.");

        Assert.StartsWith(CvImportAiOptions.DefaultSystemPromptPreface.Trim(), composed, StringComparison.Ordinal);
        Assert.Contains("Contact is a first-class sectionType", composed, StringComparison.Ordinal);
        Assert.Contains("Never omit source lines", composed, StringComparison.Ordinal);
        Assert.Contains("single atomic tokens", composed, StringComparison.Ordinal);
        Assert.Contains("Catalog rules follow.", composed, StringComparison.Ordinal);
    }

    [Fact]
    public void ComposeSystemPrompt_EmptyPreface_ReturnsCatalogOnly()
    {
        var catalog = "Catalog only.";

        Assert.Equal(catalog, GoogleAiCvStructuredImportClient.ComposeSystemPrompt("  ", catalog));
    }
}
