namespace ApplyVault.Api.Options;

/// <summary>
/// Preparation pack generation settings. Nested under <c>InterviewPrep:Ai</c>.
/// Live Gemini calls also require <c>GoogleAi:Enabled</c> and <c>GoogleAi:ApiKey</c>.
/// </summary>
public sealed class InterviewPrepAiOptions
{
    public const string SectionName = "InterviewPrep:Ai";

    /// <summary>
    /// When false, the live provider refuses generation.
    /// Use the explicit demo provider only for local previews and tests.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Enable a clearly labeled document preview; no personalized analysis is performed.
    /// </summary>
    public bool UseFakeProvider { get; set; }

    /// <summary>Per-call timeout; falls back to GoogleAi:TimeoutSeconds when null.</summary>
    public int? TimeoutSeconds { get; set; } = 180;

    /// <summary>Additional attempts after the first failure (invalid JSON / validation / transport).</summary>
    public int MaxRetries { get; set; } = 1;

    /// <summary>Delay between retries.</summary>
    public int RetryDelayMilliseconds { get; set; } = 250;
}
