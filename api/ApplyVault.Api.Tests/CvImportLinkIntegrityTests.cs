using ApplyVault.Api.Services;

namespace ApplyVault.Api.Tests;

public sealed class CvImportLinkIntegrityTests
{
    [Fact]
    public void SplitContactTokens_PreservesHttpsAndBareHostPaths()
    {
        var tokens = CvImportLinkIntegrity.SplitContactTokens(
            "jane@example.com | https://github.com/PrimusInterParess/ApplyVault | linkedin.com/in/jane-doe");

        Assert.Equal(
            [
                "jane@example.com",
                "https://github.com/PrimusInterParess/ApplyVault",
                "linkedin.com/in/jane-doe"
            ],
            tokens);
    }

    [Fact]
    public void SplitContactTokens_NeverSplitsOnSlashOrBackslashAlone()
    {
        var tokens = CvImportLinkIntegrity.SplitContactTokens(
            "https://example.com/a/b\\c | phone");

        Assert.Contains("https://example.com/a/b\\c", tokens);
        Assert.DoesNotContain("https:", tokens);
        Assert.Contains("phone", tokens);
    }

    [Fact]
    public void JoinAdjacentTokens_OmitsSpaceInsideUrlSpan()
    {
        var joined = CvImportLinkIntegrity.JoinAdjacentTokens(
            ["https://", "example.com/a/b", "profile"]);

        Assert.Equal("https://example.com/a/b profile", joined);
        Assert.DoesNotContain("https:// ", joined);
    }

    [Fact]
    public void JoinAdjacentTokens_KeepsSpaceBetweenNormalWords()
    {
        var joined = CvImportLinkIntegrity.JoinAdjacentTokens(["Software", "Engineer", "Acme"]);

        Assert.Equal("Software Engineer Acme", joined);
    }

    [Fact]
    public void SplitContactTokens_DoesNotSplitStreetAddressOnCommas()
    {
        var tokens = CvImportLinkIntegrity.SplitContactTokens(
            "Address: Fruenshave 24, 8541 Skødstrup, Denmark");

        Assert.Equal(
            ["Address: Fruenshave 24, 8541 Skødstrup, Denmark"],
            tokens);
    }

    [Fact]
    public void LooksLikeLocationLine_MatchesLabeledAndPostalStreetLines()
    {
        Assert.True(
            CvStructuredImportEntrySupport.LooksLikeLocationLine(
                "Address: Fruenshave 24, 8541 Skødstrup, Denmark"));
        Assert.True(
            CvStructuredImportEntrySupport.LooksLikeLocationLine("8541 Skødstrup"));
        Assert.True(
            CvStructuredImportEntrySupport.LooksLikeLocationLine("Aarhus, Denmark"));
        Assert.False(
            CvStructuredImportEntrySupport.LooksLikeLocationLine("Backend, Frontend, Cloud"));
    }
}
