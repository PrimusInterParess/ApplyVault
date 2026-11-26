using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApplyVault.Api.Data;
using ApplyVault.Api.Models.InterviewPrep;
using ApplyVault.Api.Services.InterviewPrep.Adapters;
using ApplyVault.Api.Services.InterviewPrep.Ai;
using ApplyVault.Api.Services.InterviewPrep.Domain;
using Microsoft.EntityFrameworkCore;

namespace ApplyVault.Api.Services.InterviewPrep;

internal sealed record StoredPreparationPack(
    int SchemaVersion,
    PreparationPackInput Input,
    PreparationPackContent Content,
    PreparationProgress Progress,
    bool IsDemo,
    PreparationDiscovery? Discovery = null);

public sealed class InterviewPreparationPackService(
    ApplyVaultDbContext db,
    IInterviewPrepCandidateContextAdapter candidateAdapter,
    IInterviewPrepJobContextAdapter jobAdapter,
    IInterviewPreparationPackGenerator generator)
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<PreparationContextDto> ContextAsync(AppUserEntity user, CancellationToken ct)
    {
        var hasCv = true;
        try { await candidateAdapter.CaptureAsync(user, ct); }
        catch (InterviewPrepValidationException) { hasCv = false; }
        var projects = await db.UserCvProjectSummaries.AsNoTracking().Where(p => p.UserId == user.Id)
            .OrderBy(p => p.CvTitle).Select(p => new PreparationProjectOption(p.Id, p.CvTitle, p.FullName)).ToListAsync(ct);
        return new(hasCv, projects);
    }

    public async Task<IReadOnlyList<PreparationPackDto>> ListAsync(AppUserEntity user, CancellationToken ct)
    {
        var entities = await db.InterviewPrepStudyBriefs.AsNoTracking().Where(p => p.UserId == user.Id)
            .OrderByDescending(p => p.GeneratedAt).ToListAsync(ct);
        var result = new List<PreparationPackDto>();
        foreach (var entity in entities) result.Add(await MapAsync(user, entity, ct));
        return result;
    }

    public async Task<PreparationPackDto> GetAsync(AppUserEntity user, Guid id, CancellationToken ct) =>
        await MapAsync(user, await OwnedAsync(user.Id, id, ct), ct);

    public async Task<PreparationPackDto> GenerateAsync(AppUserEntity user, PreparationPackInput input, CancellationToken ct)
    {
        input = InterviewPreparationPackValidation.Normalize(input);
        var existing = await db.InterviewPrepStudyBriefs.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == user.Id && p.ScrapeResultId == input.ScrapeResultId, ct);
        if (existing is not null)
            throw new InterviewPrepConflictException("A pack already exists for this job or general preparation. Open it to regenerate.", existing.Id);
        var source = await SourcesAsync(user, input, ct);
        var generated = await generator.PlanAsync(source, ct);
        PreparationDiscoveryValidation.Plan(generated.Plan, input);
        var now = DateTimeOffset.UtcNow;
        var entity = new InterviewPrepStudyBriefEntity
        {
            Id = Guid.NewGuid(), UserId = user.Id, ScrapeResultId = input.ScrapeResultId,
            Language = input.Language, Market = "general", BodyJson = "", CvFingerprint = "",
            CreatedAt = now, GeneratedAt = now, UpdatedAt = now
        };
        Apply(entity, input, source, generated, now);
        db.InterviewPrepStudyBriefs.Add(entity);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // The unique binding index arbitrates concurrent first-generation requests.
            var winner = await db.InterviewPrepStudyBriefs.AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == user.Id && p.ScrapeResultId == input.ScrapeResultId, ct);
            if (winner is not null) throw new InterviewPrepConflictException("Another request created this pack. Open the saved pack.", winner.Id);
            throw;
        }
        return Dto(entity, Read(entity)!, []);
    }

    public async Task<PreparationPackDto> RegenerateAsync(AppUserEntity user, Guid id, RegeneratePreparationPackRequest request, CancellationToken ct)
    {
        var entity = await OwnedAsync(user.Id, id, ct);
        EnsureCurrent(entity, request.ExpectedUpdatedAt);
        var input = InterviewPreparationPackValidation.Normalize(request.Input);
        if (input.ScrapeResultId != entity.ScrapeResultId)
            throw new InterviewPrepValidationException("A pack's saved job cannot be changed. Create or open that job's pack instead.");
        var source = await SourcesAsync(user, input, ct);
        var generated = await generator.PlanAsync(source, ct);
        PreparationDiscoveryValidation.Plan(generated.Plan, input);
        Apply(entity, input, source, generated, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync(ct);
        return Dto(entity, Read(entity)!, []);
    }

    public async Task<PreparationPackDto> SaveProgressAsync(AppUserEntity user, Guid id, PreparationProgressRequest request, CancellationToken ct)
    {
        var entity = await OwnedAsync(user.Id, id, ct);
        EnsureCurrent(entity, request.ExpectedUpdatedAt);
        var stored = Read(entity) ?? throw new InterviewPrepValidationException("Regenerate this older brief before tracking progress.");
        InterviewPreparationPackValidation.Progress(request.Progress, stored.Content);
        stored = stored with { Progress = request.Progress };
        entity.BodyJson = JsonSerializer.Serialize(stored, JsonOptions);
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return await MapAsync(user, entity, ct);
    }

    public async Task<PreparationPackDto> PrepareDocumentAsync(AppUserEntity user, Guid id, string documentId,
        PreparationRevisionRequest request, CancellationToken ct)
    {
        var entity = await OwnedAsync(user.Id, id, ct);
        EnsureCurrent(entity, request.ExpectedUpdatedAt);
        var stored = RequiredPack(entity);
        var document = FindDocument(stored, documentId);
        var sources = await SourcesAsync(user, stored.Input with { ScrapeResultId = entity.ScrapeResultId }, ct);
        var generated = await generator.PrepareDocumentAsync(sources, document, stored.Discovery, ct);
        PreparationDiscoveryValidation.DocumentOutline(generated.Outline, document);
        var previous = stored.Discovery ?? new PreparationDiscovery(stored.Content.Documents
            .Select(d => new PreparationDocumentOutline(d.Id, d.Title, d.Purpose, d.PrerequisiteIds, [])).ToArray(), []);
        var answers = previous.Answers.Where(a => a.DocumentId != documentId).ToArray();
        var revision = previous.ContextRevision + (answers.Length != previous.Answers.Count ? 1 : 0);
        var discovery = previous with
        {
            Documents = previous.Documents.Select(d => d.Id == documentId ? generated.Outline with { ContextRevision = revision } : d).ToArray(),
            Answers = answers,
            ContextRevision = revision
        };
        PreparationDiscoveryValidation.Discovery(discovery, stored.Content);
        await PersistAsync(entity, stored with { Discovery = discovery, IsDemo = stored.IsDemo || generated.IsDemo }, ct);
        return await MapAsync(user, entity, ct);
    }

    public async Task<PreparationPackDto> SaveAnswersAsync(AppUserEntity user, Guid id, string documentId,
        PreparationAnswersRequest request, CancellationToken ct)
    {
        var entity = await OwnedAsync(user.Id, id, ct);
        EnsureCurrent(entity, request.ExpectedUpdatedAt);
        var stored = RequiredPack(entity);
        var outline = FindOutline(stored, documentId);
        var answers = PreparationDiscoveryValidation.Answers(documentId, outline, request.Answers);
        var previous = stored.Discovery!;
        var merged = previous.Answers.Where(a => a.DocumentId != documentId).Concat(answers)
            .OrderBy(a => a.DocumentId).ThenBy(a => a.QuestionId).ToArray();
        var changed = !previous.Answers.OrderBy(a => a.DocumentId).ThenBy(a => a.QuestionId).SequenceEqual(merged);
        var discovery = previous with { Answers = merged, ContextRevision = previous.ContextRevision + (changed ? 1 : 0) };
        PreparationDiscoveryValidation.Discovery(discovery, stored.Content);
        await PersistAsync(entity, stored with { Discovery = discovery }, ct);
        return await MapAsync(user, entity, ct);
    }

    public async Task<PreparationPackDto> WriteDocumentAsync(AppUserEntity user, Guid id, string documentId,
        PreparationRevisionRequest request, CancellationToken ct)
    {
        var entity = await OwnedAsync(user.Id, id, ct);
        EnsureCurrent(entity, request.ExpectedUpdatedAt);
        var stored = RequiredPack(entity);
        var outline = FindOutline(stored, documentId);
        if (outline.Questions.Any(q => !stored.Discovery!.Answers.Any(a => a.DocumentId == documentId
                && a.QuestionId == q.Id && a.Status != "pending")))
            throw new InterviewPrepValidationException("Answer each clarification question, choose I don't know, or skip it before writing this document.");
        var sources = await SourcesAsync(user, stored.Input with { ScrapeResultId = entity.ScrapeResultId }, ct);
        var generated = await generator.WriteDocumentAsync(new(sources, stored.Content, stored.Discovery!, outline), ct);
        var documents = stored.Content.Documents.Select(d => d.Id == documentId ? d with
        {
            Markdown = generated.Markdown, IsGenerated = true, ContextRevision = stored.Discovery!.ContextRevision,
            SourceFingerprint = Fingerprint(sources)
        } : d).ToArray();
        var content = stored.Content with { Documents = documents };
        InterviewPreparationPackValidation.Content(content, stored.Input, allowPending: true);
        // Rewriting one document resets only its readiness and the study days that use it.
        var progress = stored.Progress;
        var affectedDays = content.StudyPlan.Where(s => s.DocumentIds.Contains(documentId)).Select(s => s.Day).ToHashSet();
        progress = progress with
        {
            ReadDocumentIds = progress.ReadDocumentIds.Where(d => d != documentId).ToArray(),
            ExplainedDocumentIds = progress.ExplainedDocumentIds.Where(d => d != documentId).ToArray(),
            DemonstratedDocumentIds = progress.DemonstratedDocumentIds.Where(d => d != documentId).ToArray(),
            AdaptedDocumentIds = progress.AdaptedDocumentIds.Where(d => d != documentId).ToArray(),
            CompletedDays = progress.CompletedDays.Where(d => !affectedDays.Contains(d)).ToArray()
        };
        entity.GeneratedAt = DateTimeOffset.UtcNow;
        await PersistAsync(entity, stored with { Content = content, Progress = progress, IsDemo = stored.IsDemo || generated.IsDemo }, ct);
        return await MapAsync(user, entity, ct);
    }

    public async Task<PreparationHelpResponse> HelpAsync(AppUserEntity user, Guid id, string documentId, string questionId,
        PreparationHelpRequest request, CancellationToken ct)
    {
        var entity = await OwnedAsync(user.Id, id, ct);
        EnsureCurrent(entity, request.ExpectedUpdatedAt);
        var stored = RequiredPack(entity);
        var outline = FindOutline(stored, documentId);
        var question = outline.Questions.FirstOrDefault(q => q.Id == questionId) ?? throw new InterviewPrepNotFoundException();
        PreparationDiscoveryValidation.Help(request);
        var sources = await SourcesAsync(user, stored.Input with { ScrapeResultId = entity.ScrapeResultId }, ct);
        // Help is deliberately read-only. Suggested wording and examples never enter the answer ledger.
        return await generator.HelpAsync(new(sources, outline, question, stored.Discovery!.Answers, request), ct);
    }

    private async Task PersistAsync(InterviewPrepStudyBriefEntity entity, StoredPreparationPack stored, CancellationToken ct)
    {
        entity.BodyJson = JsonSerializer.Serialize(stored, JsonOptions);
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static StoredPreparationPack RequiredPack(InterviewPrepStudyBriefEntity entity) => Read(entity)
        ?? throw new InterviewPrepValidationException("Create an outline for this older brief before personalizing its documents.");

    private static PreparationDocument FindDocument(StoredPreparationPack stored, string id) =>
        stored.Content.Documents.FirstOrDefault(d => d.Id == id) ?? throw new InterviewPrepNotFoundException();

    private static PreparationDocumentOutline FindOutline(StoredPreparationPack stored, string id)
    {
        FindDocument(stored, id);
        return stored.Discovery?.Documents.FirstOrDefault(d => d.Id == id && d.Questions.Count > 0)
            ?? throw new InterviewPrepValidationException("Prepare clarification questions for this document first.");
    }

    public async Task<bool> DeleteAsync(AppUserEntity user, Guid id, CancellationToken ct)
    {
        var entity = await db.InterviewPrepStudyBriefs.FirstOrDefaultAsync(p => p.UserId == user.Id && p.Id == id, ct);
        if (entity is null) return false;
        db.InterviewPrepStudyBriefs.Remove(entity);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<PreparationPackSources> SourcesAsync(AppUserEntity user, PreparationPackInput input, CancellationToken ct)
    {
        var candidate = await candidateAdapter.CaptureAsync(user, ct);
        var job = input.ScrapeResultId is Guid jobId ? await jobAdapter.CaptureAsync(user, jobId, ct) : null;
        var ids = input.ProjectSummaryIds ?? [];
        var projects = await db.UserCvProjectSummaries.AsNoTracking()
            .Where(p => p.UserId == user.Id && ids.Contains(p.Id)).OrderBy(p => p.Id)
            .Select(p => new PreparationProjectSource(p.Id, p.CvTitle, p.CvSummary, p.CvBullets, p.TechStack)).ToListAsync(ct);
        if (projects.Count != ids.Count)
            throw new InterviewPrepValidationException("One or more selected projects are unavailable. Select projects saved in your account.");
        var sources = new PreparationPackSources(input, candidate.SnapshotJson, job?.SnapshotJson, job?.JobTitle,
            job?.CompanyName, candidate.CvDocumentId, projects);
        if (JsonSerializer.Serialize(sources, JsonOptions).Length > 160000)
            throw new InterviewPrepValidationException("The selected material is too large. Select fewer projects or shorten the additional information.");
        return sources;
    }

    private async Task<PreparationPackDto> MapAsync(AppUserEntity user, InterviewPrepStudyBriefEntity entity, CancellationToken ct)
    {
        var stored = Read(entity);
        if (stored is null)
        {
            // Preserve old rows and their binding, expose an explicit upgrade path instead of silently hiding them.
            return new(entity.Id, entity.ScrapeResultId, entity.JobTitle, entity.CompanyName,
                new PreparationPackInput(entity.ScrapeResultId, entity.JobTitle, Language: entity.Language,
                    AdditionalInformation: entity.FocusNoteSnapshot), null, PreparationProgress.Empty,
                true, ["This older study brief needs to be regenerated as a preparation pack."], false, entity.GeneratedAt, entity.UpdatedAt);
        }
        var reasons = new List<string>();
        var input = stored.Input with { ScrapeResultId = entity.ScrapeResultId };
        if (entity.WasJobBound && entity.ScrapeResultId is null)
            reasons.Add("The saved job was deleted. Regeneration now requires a target role and level.");
        try
        {
            var current = await SourcesAsync(user, input, ct);
            var fingerprint = Fingerprint(current);
            if (entity.CvFingerprint != fingerprint || stored.Content.Documents.Any(d =>
                    d.IsGenerated && (d.SourceFingerprint ?? entity.CvFingerprint) != fingerprint))
                reasons.Add("Your CV, saved job, or selected project information has changed since generation.");
        }
        catch (InterviewPrepValidationException)
        {
            reasons.Add("A source CV, saved job, or selected project is no longer available. Review the inputs before regenerating.");
        }
        // FK deletion must also update the editable input; never offer a job id that no longer exists.
        return Dto(entity, stored with { Input = input }, reasons);
    }

    private static PreparationPackDto Dto(InterviewPrepStudyBriefEntity entity, StoredPreparationPack stored, IReadOnlyList<string> reasons) =>
        new(entity.Id, entity.ScrapeResultId, entity.JobTitle, entity.CompanyName, stored.Input, stored.Content,
            stored.Progress, false, reasons, stored.IsDemo, entity.GeneratedAt, entity.UpdatedAt, stored.Discovery);

    private static void Apply(InterviewPrepStudyBriefEntity entity, PreparationPackInput input, PreparationPackSources sources,
        GeneratedPreparationPlan generated, DateTimeOffset now)
    {
        entity.BodyJson = JsonSerializer.Serialize(new StoredPreparationPack(2, input, PreparationDiscoveryValidation.DraftContent(generated.Plan), PreparationProgress.Empty,
            generated.IsDemo, new PreparationDiscovery(generated.Plan.Documents, [])), JsonOptions);
        entity.Language = input.Language;
        entity.FocusNoteSnapshot = null;
        entity.CvDocumentId = sources.CvDocumentId;
        entity.CvFingerprint = Fingerprint(sources);
        entity.JobTitle = sources.JobTitle;
        entity.CompanyName = sources.CompanyName;
        entity.WasJobBound = input.ScrapeResultId is not null;
        entity.UsedAiFallback = false;
        entity.GeneratedAt = now;
        entity.UpdatedAt = now;
    }

    private static string Fingerprint(PreparationPackSources sources) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sources, JsonOptions))));

    private static StoredPreparationPack? Read(InterviewPrepStudyBriefEntity entity)
    {
        try
        {
            var stored = JsonSerializer.Deserialize<StoredPreparationPack>(entity.BodyJson, JsonOptions);
            if (stored is null || stored.SchemaVersion != 2 || stored.Input is null || stored.Progress is null) return null;
            InterviewPreparationPackValidation.Content(stored.Content, stored.Input, allowPending: stored.Discovery is not null);
            if (stored.Discovery is not null) PreparationDiscoveryValidation.Discovery(stored.Discovery, stored.Content);
            return stored;
        }
        catch (JsonException) { return null; }
        catch (InterviewPrepAiUnavailableException) { return null; }
    }

    private async Task<InterviewPrepStudyBriefEntity> OwnedAsync(Guid userId, Guid id, CancellationToken ct) =>
        await db.InterviewPrepStudyBriefs.FirstOrDefaultAsync(p => p.UserId == userId && p.Id == id, ct)
        ?? throw new InterviewPrepNotFoundException();

    private static void EnsureCurrent(InterviewPrepStudyBriefEntity entity, DateTimeOffset expected)
    {
        if (entity.UpdatedAt != expected)
            throw new InterviewPrepConflictException("This pack changed in another tab. Reload it before saving.");
    }
}
