namespace ApplyVault.Api.Data;

public sealed class ScrapeResultEntity
{
    public Guid Id { get; set; }

    public DateTimeOffset SavedAt { get; set; }

    public bool IsRejected { get; set; }

    public bool IsDeleted { get; set; }

    public required string Title { get; set; }

    public required string Url { get; set; }

    public required string Text { get; set; }

    public int TextLength { get; set; }

    public required string ExtractedAt { get; set; }

    public required string SourceHostname { get; set; }

    public required string DetectedPageType { get; set; }

    public string? JobTitle { get; set; }

    public string? CompanyName { get; set; }

    public string? Location { get; set; }

    public string? JobDescription { get; set; }

    public string? PositionSummary { get; set; }

    public string? HiringManagerName { get; set; }

    public List<ScrapeResultContactEntity> HiringManagerContacts { get; set; } = [];
}

public sealed class ScrapeResultContactEntity
{
    public int Id { get; set; }

    public Guid ScrapeResultId { get; set; }

    public required string Type { get; set; }

    public required string Value { get; set; }

    public string? Label { get; set; }
}
