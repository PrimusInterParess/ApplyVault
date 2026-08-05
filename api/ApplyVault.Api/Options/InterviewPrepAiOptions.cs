namespace ApplyVault.Api.Options;

/// <summary>
/// Interview Prep AI gateway settings. Nested under <c>InterviewPrep:Ai</c>.
/// Live Gemini calls also require <c>GoogleAi:Enabled</c> and <c>GoogleAi:ApiKey</c>.
/// </summary>
public sealed class InterviewPrepAiOptions
{
    public const string SectionName = "InterviewPrep:Ai";

    /// <summary>
    /// When false, the gateway still runs but the live transport refuses Gemini calls.
    /// Prefer <see cref="UseFakeProvider"/> for deterministic local/tests.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Force the deterministic fake provider (recommended for unit tests and local without keys).
    /// </summary>
    public bool UseFakeProvider { get; set; } = true;

    /// <summary>Per-call timeout; falls back to GoogleAi:TimeoutSeconds when null.</summary>
    public int? TimeoutSeconds { get; set; } = 30;

    /// <summary>Additional attempts after the first failure (invalid JSON / validation / transport).</summary>
    public int MaxRetries { get; set; } = 1;

    /// <summary>Delay between retries.</summary>
    public int RetryDelayMilliseconds { get; set; } = 250;

    /// <summary>
    /// When true and retries are exhausted, gateway may return a safe structured fallback
    /// (where defined) instead of only a normalized error.
    /// </summary>
    public bool AllowSafeFallback { get; set; } = true;
}
