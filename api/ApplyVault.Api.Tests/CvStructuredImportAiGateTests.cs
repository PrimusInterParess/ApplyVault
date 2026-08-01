using ApplyVault.Api.Services;

namespace ApplyVault.Api.Tests;

public sealed class CvStructuredImportAiGateTests
{
    [Fact]
    public void ShouldCallAi_FalseWhenGoogleAiDisabled()
    {
        Assert.False(CvStructuredImportAiGate.ShouldCallAi(
            googleAiEnabled: false,
            CvPdfExtractionQuality.Good));
    }

    [Fact]
    public void ShouldCallAi_FalseWhenEmptyExtraction()
    {
        Assert.False(CvStructuredImportAiGate.ShouldCallAi(
            googleAiEnabled: true,
            CvPdfExtractionQuality.Empty));
    }

    [Fact]
    public void ShouldCallAi_TrueWhenEnabledAndNonEmpty()
    {
        Assert.True(CvStructuredImportAiGate.ShouldCallAi(
            googleAiEnabled: true,
            CvPdfExtractionQuality.Good));
        Assert.True(CvStructuredImportAiGate.ShouldCallAi(
            googleAiEnabled: true,
            CvPdfExtractionQuality.Sparse));
    }
}
