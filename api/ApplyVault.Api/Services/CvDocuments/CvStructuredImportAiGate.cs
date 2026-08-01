namespace ApplyVault.Api.Services;

/// <summary>
/// Thin AI-first enable check. Call Gemini when Google AI is enabled and extract is non-empty.
/// Confidence / ForceAi gating removed.
/// </summary>
internal static class CvStructuredImportAiGate
{
    public static bool ShouldCallAi(bool googleAiEnabled, CvPdfExtractionQuality extractionQuality) =>
        googleAiEnabled && extractionQuality != CvPdfExtractionQuality.Empty;
}
