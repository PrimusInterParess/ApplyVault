namespace ApplyVault.Api.Data;

/// <summary>
/// Durable Interview Prep study brief (ADR-0025). Independent of sessions.
/// </summary>
public sealed class InterviewPrepStudyBriefEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Owned scrape binding; null for CV-only. SET NULL when scrape deleted.</summary>
    public Guid? ScrapeResultId { get; set; }

    public required string Language { get; set; }

    public required string Market { get; set; }

    /// <summary>Last focus note supplied on generate/regenerate (null if omitted that run).</summary>
    public string? FocusNoteSnapshot { get; set; }

    /// <summary>Structured body JSON: topics, sampleQuestions, talkingPoints (camelCase).</summary>
    public required string BodyJson { get; set; }

    /// <summary>
    /// Structured CV change token at last generate (document id + imported-at + content hash).
    /// Compared on read to label <c>structuredCvChanged</c>.
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
