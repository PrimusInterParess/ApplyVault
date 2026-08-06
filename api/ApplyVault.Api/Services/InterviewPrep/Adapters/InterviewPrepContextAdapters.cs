using System.Text.Json;
using ApplyVault.Api.Data;
using ApplyVault.Api.Models;
using ApplyVault.Api.Services;
using ApplyVault.Api.Services.CvSectionCatalog;
using ApplyVault.Api.Services.InterviewPrep.Domain;

namespace ApplyVault.Api.Services.InterviewPrep.Adapters;

public interface IInterviewPrepCandidateContextAdapter
{
    Task<InterviewPrepCandidateSnapshot> CaptureAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default);
}

public interface IInterviewPrepJobContextAdapter
{
    Task<InterviewPrepJobSnapshot?> CaptureAsync(
        AppUserEntity user,
        Guid scrapeResultId,
        CancellationToken cancellationToken = default);
}

public sealed record InterviewPrepCandidateSnapshot(
    Guid CvDocumentId,
    DateTimeOffset? StructuredImportedAt,
    string CatalogVersion,
    string SnapshotJson,
    DateTimeOffset CapturedAt);

public sealed record InterviewPrepJobSnapshot(
    Guid ScrapeResultId,
    string? JobTitle,
    string? CompanyName,
    string? JobDescription,
    string SnapshotJson,
    DateTimeOffset CapturedAt);

public sealed class InterviewPrepCandidateContextAdapter(
    ICvStructuredDocumentService structuredDocumentService,
    ICvSectionCatalog sectionCatalog) : IInterviewPrepCandidateContextAdapter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<InterviewPrepCandidateSnapshot> CaptureAsync(
        AppUserEntity user,
        CancellationToken cancellationToken = default)
    {
        var structured = await structuredDocumentService.GetStructuredAsync(user, cancellationToken);
        if (structured is null || structured.Sections.Count == 0)
        {
            throw new InterviewPrepValidationException(
                "Create a Structured CV before preparing an interview prep session.");
        }

        var capturedAt = DateTimeOffset.UtcNow;
        // CapturedAt stays on the record only — do not embed in SnapshotJson.
        // Study-brief fingerprints hash SnapshotJson; a clock field would make every
        // post-regenerate outdated check look like the Structured CV changed.
        var payload = new
        {
            structured.DocumentId,
            structured.StructuredImportedAt,
            CatalogVersion = sectionCatalog.Version.ToString(),
            structured.Sections
        };

        return new InterviewPrepCandidateSnapshot(
            structured.DocumentId,
            structured.StructuredImportedAt,
            sectionCatalog.Version.ToString(),
            JsonSerializer.Serialize(payload, SerializerOptions),
            capturedAt);
    }
}

public sealed class InterviewPrepJobContextAdapter(
    IScrapeResultStore scrapeResultStore) : IInterviewPrepJobContextAdapter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<InterviewPrepJobSnapshot?> CaptureAsync(
        AppUserEntity user,
        Guid scrapeResultId,
        CancellationToken cancellationToken = default)
    {
        var job = await scrapeResultStore.GetByIdAsync(scrapeResultId, user.Id, cancellationToken);
        if (job is null)
        {
            throw new InterviewPrepValidationException(
                "Scrape result was not found for the current user.");
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

        return new InterviewPrepJobSnapshot(
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
