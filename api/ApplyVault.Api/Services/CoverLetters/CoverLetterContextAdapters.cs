using System.Text.Json;
using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using ApplyVault.Api.Services.CvSectionCatalog;

namespace ApplyVault.Api.Services.CoverLetters;

public interface ICoverLetterCandidateContextAdapter
{
    Task<CoverLetterCandidateSnapshot> CaptureAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default);
}

public interface ICoverLetterJobContextAdapter
{
    Task<CoverLetterJobSnapshot?> CaptureAsync(
        AppUserEntity user,
        Guid scrapeResultId,
        CancellationToken cancellationToken = default);
}

public sealed record CoverLetterCandidateSnapshot(
    Guid CvDocumentId,
    DateTimeOffset? StructuredImportedAt,
    string CatalogVersion,
    string SnapshotJson,
    DateTimeOffset CapturedAt);

public sealed record CoverLetterJobSnapshot(
    Guid ScrapeResultId,
    string? JobTitle,
    string? CompanyName,
    string? JobDescription,
    string SnapshotJson,
    DateTimeOffset CapturedAt);

public sealed class CoverLetterCandidateContextAdapter(
    ICvStructuredDocumentService structuredDocumentService,
    ICvSectionCatalog sectionCatalog) : ICoverLetterCandidateContextAdapter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<CoverLetterCandidateSnapshot> CaptureAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default)
    {
        var structured = await structuredDocumentService.GetStructuredAsync(user, cancellationToken);
        if (structured is null || structured.Sections.Count == 0)
        {
            throw new CoverLetterValidationException(
                "Create a Structured CV before writing a cover letter.");
        }

        var capturedAt = DateTimeOffset.UtcNow;
        // CapturedAt stays on the record only — do not embed in SnapshotJson.
        // includedOnTemplate is template visibility, not entry content. Omit it so a
        // visibility-only save does not change the fingerprint. Entries stay either way.
        var payload = new
        {
            structured.DocumentId,
            structured.StructuredImportedAt,
            CatalogVersion = sectionCatalog.Version.ToString(),
            Sections = structured.Sections.Select((section) => new
            {
                section.Id,
                section.Heading,
                section.SectionType,
                section.SortOrder,
                section.Entries
            })
        };

        return new CoverLetterCandidateSnapshot(
            structured.DocumentId,
            structured.StructuredImportedAt,
            sectionCatalog.Version.ToString(),
            JsonSerializer.Serialize(payload, SerializerOptions),
            capturedAt);
    }
}

public sealed class CoverLetterJobContextAdapter(
    IScrapeResultStore scrapeResultStore) : ICoverLetterJobContextAdapter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<CoverLetterJobSnapshot?> CaptureAsync(
        AppUserEntity user,
        Guid scrapeResultId,
        CancellationToken cancellationToken = default)
    {
        var job = await scrapeResultStore.GetByIdAsync(scrapeResultId, user.Id, cancellationToken);
        if (job is null)
        {
            throw new CoverLetterValidationException(
                "Saved job was not found.");
        }

        var title = FirstNonEmpty(
            job.CaptureQuality.JobTitle.EffectiveValue,
            job.Payload.JobDetails.JobTitle,
            job.Payload.Title);
        var company = FirstNonEmpty(
            job.CaptureQuality.CompanyName.EffectiveValue,
            job.Payload.JobDetails.CompanyName);
        var description = FirstNonEmpty(
            job.CaptureQuality.JobDescription.EffectiveValue,
            job.Payload.JobDetails.JobDescription,
            job.Payload.Text);

        var capturedAt = DateTimeOffset.UtcNow;
        var payload = new
        {
            ScrapeResultId = job.Id,
            JobTitle = title,
            CompanyName = company,
            JobDescription = description,
            job.Payload.Url
        };

        return new CoverLetterJobSnapshot(
            job.Id,
            title,
            company,
            description,
            JsonSerializer.Serialize(payload, SerializerOptions),
            capturedAt);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault((value) => !string.IsNullOrWhiteSpace(value));
}
