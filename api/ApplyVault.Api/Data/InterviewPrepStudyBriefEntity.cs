namespace ApplyVault.Api.Data;

/// <summary>
/// Preparation pack storage. The legacy table name is retained to preserve saved briefs (ADR-0046).
/// </summary>
public sealed class InterviewPrepStudyBriefEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Owned scrape binding; null for CV-only. SET NULL when scrape deleted.</summary>
    public Guid? ScrapeResultId { get; set; }

    public required string Language { get; set; }

    public required string Market { get; set; }

    /// <summary>Legacy brief focus note. New pack inputs live in the versioned body.</summary>
    public string? FocusNoteSnapshot { get; set; }

    /// <summary>Versioned preparation pack JSON: inputs, documents, study plan, and progress.</summary>
    public required string BodyJson { get; set; }

    /// <summary>
    /// Source fingerprint at generation, including CV, job, selected projects, and preparation inputs.
    /// </summary>
    public required string CvFingerprint { get; set; }

    public Guid? CvDocumentId { get; set; }

    public string? JobTitle { get; set; }

    public string? CompanyName { get; set; }

    /// <summary>True when generate/regenerate had a job binding (even if title/company empty).</summary>
    public bool WasJobBound { get; set; }

    public bool UsedAiFallback { get; set; }

    public DateTimeOffset GeneratedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
