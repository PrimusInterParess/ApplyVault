using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using ApplyVault.Api.Models.InterviewPrep;
using ApplyVault.Api.Services.InterviewPrep.Domain;

namespace ApplyVault.Api.Services.InterviewPrep;

internal static partial class InterviewPreparationPackValidation
{
    internal const int MaxPreparationDays = 90;
    internal static PreparationPackInput Normalize(PreparationPackInput? input)
    {
        if (input is null) throw new InterviewPrepValidationException("Preparation inputs are required.");
        var normalized = input with
        {
            TargetRole = Text(input.TargetRole, 200, "Target role"),
            TargetLevel = Text(input.TargetLevel, 100, "Target level"),
            HiringMarket = Text(input.HiringMarket, 120, "Hiring market"),
            KnownWeaknesses = Text(input.KnownWeaknesses, 4000, "Known weaknesses"),
            AdditionalInformation = Text(input.AdditionalInformation, 16000, "Additional information"),
            InterviewFormats = input.InterviewFormats?.Select(f => Text(f, 120, "Interview format") ?? "")
                .Where(f => f.Length > 0).Distinct().ToArray() ?? [],
            ProjectSummaryIds = input.ProjectSummaryIds?.Distinct().Order().ToArray() ?? []
        };
        if (normalized.ScrapeResultId is null && (normalized.TargetRole is null || normalized.TargetLevel is null))
            throw new InterviewPrepValidationException("Without a saved job, enter a target role and level.");
        if (normalized.Language is not ("english" or "danish" or "mixedEnglishDanish"))
            throw new InterviewPrepValidationException("Choose a supported preparation language.");
        if (normalized.PreparationDays is < 1 or > MaxPreparationDays || normalized.MinutesPerDay is < 15 or > 240)
            throw new InterviewPrepValidationException($"Choose 1–{MaxPreparationDays} preparation days and 15–240 minutes per day.");
        if (normalized.InterviewFormats!.Count > 8 || normalized.ProjectSummaryIds!.Count > 10)
            throw new InterviewPrepValidationException("Choose at most 8 interview formats and 10 projects.");
        return normalized;
    }

    internal static void Content(PreparationPackContent? content, PreparationPackInput input, bool allowPending = false)
    {
        if (content is null || string.IsNullOrWhiteSpace(content.Title) || content.Title.Length > 300
            || string.IsNullOrWhiteSpace(content.OverviewMarkdown) || content.OverviewMarkdown.Length > 20000
            || content.Unknowns is null || content.Unknowns.Any(string.IsNullOrWhiteSpace)
            || content.Topics is null || content.Topics.Count is < 1 or > 30
            || content.Documents is null || content.Documents.Count is < 1 or > 12
            || content.StudyPlan is null || content.StudyPlan.Count is < 1 or > MaxPreparationDays)
            Invalid("pack_structure");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var doc in content!.Documents)
        {
            if (doc is null || string.IsNullOrEmpty(doc.Id) || !DocumentId().IsMatch(doc.Id) || !ids.Add(doc.Id)
                || string.IsNullOrWhiteSpace(doc.Title) || string.IsNullOrWhiteSpace(doc.Purpose)
                || (doc.IsGenerated ? string.IsNullOrWhiteSpace(doc.Markdown) || doc.Markdown.Length is < 200 or > 60000
                    : !allowPending || doc.Markdown != "")
                || doc.PrerequisiteIds is null)
                Invalid("document_structure");
            if (doc.PrerequisiteIds.Any(p => p == doc.Id || !ids.Contains(p))) Invalid("prerequisite_order");
        }
        foreach (var markdown in content.Documents.Select(d => d.Markdown).Append(content.OverviewMarkdown))
        {
            // Only local document links and HTTPS sources: no invented file destinations.
            foreach (Match link in MarkdownLink().Matches(markdown))
            {
                var href = link.Groups[1].Value;
                if (Uri.TryCreate(href, UriKind.Absolute, out var uri) && uri.Scheme == "https") continue;
                if (href.EndsWith(".md", StringComparison.Ordinal) && ids.Contains(href[..^3])) continue;
                Invalid("document_link");
            }
        }
        if (content.Topics.Any(t => t is null || string.IsNullOrWhiteSpace(t.Name)
            || string.IsNullOrWhiteSpace(t.Relevance) || string.IsNullOrWhiteSpace(t.Evidence) || !ids.Contains(t.DocumentId)))
            Invalid("topic_reference");
        var days = new HashSet<int>();
        var scheduledIds = new HashSet<string>();
        var documents = content.Documents.ToDictionary(d => d.Id);
        foreach (var session in content.StudyPlan.OrderBy(s => s?.Day))
        {
            if (session is null) Invalid("study_session");
            if (session.Day < 1 || session.Day > input.PreparationDays) Invalid("study_day_range");
            if (!days.Add(session.Day)) Invalid("duplicate_study_day");
            if (session.Minutes < 1 || session.Minutes > input.MinutesPerDay) Invalid("study_minutes_range");
            if (string.IsNullOrWhiteSpace(session.Activity)) Invalid("study_activity");
            if (session.DocumentIds is null || session.DocumentIds.Any(d => !ids.Contains(d)))
                Invalid("study_document_reference");
            foreach (var id in session.DocumentIds)
            {
                if (documents[id].PrerequisiteIds.Any(prerequisite => !scheduledIds.Contains(prerequisite))) Invalid("study_prerequisite_order");
                scheduledIds.Add(id);
            }
        }
        if (!scheduledIds.SetEquals(ids)) Invalid("unscheduled_document");
    }

    internal static void Progress(PreparationProgress? progress, PreparationPackContent content)
    {
        if (progress is null || progress.ReviewLog is null || progress.ReviewLog.Length > 16000
            || progress.ReadDocumentIds is null || progress.ExplainedDocumentIds is null
            || progress.DemonstratedDocumentIds is null || progress.AdaptedDocumentIds is null || progress.CompletedDays is null)
            throw new InterviewPrepValidationException("Invalid preparation progress.");
        var ids = content.Documents.Where(d => d.IsGenerated).Select(d => d.Id).ToHashSet();
        var days = content.StudyPlan.Where(s => s.DocumentIds.All(ids.Contains)).Select(s => s.Day).ToHashSet();
        if (new[] { progress.ReadDocumentIds, progress.ExplainedDocumentIds, progress.DemonstratedDocumentIds, progress.AdaptedDocumentIds }
                .Any(list => list.Count > ids.Count || list.Distinct().Count() != list.Count || list.Any(id => !ids.Contains(id)))
            || progress.CompletedDays.Distinct().Count() != progress.CompletedDays.Count
            || progress.CompletedDays.Any(day => !days.Contains(day)))
            throw new InterviewPrepValidationException("Progress must refer to documents and days in this pack.");
    }

    private static string? Text(string? value, int max, string label)
    {
        var result = value?.Trim();
        if (result?.Length > max) throw new InterviewPrepValidationException($"{label} must be at most {max} characters.");
        return string.IsNullOrEmpty(result) ? null : result;
    }

    [DoesNotReturn]
    private static void Invalid(string code) => throw new InterviewPrepAiUnavailableException(
        "The generated pack was incomplete or inconsistent. Your previous documents are safe; please try again.") { FailureCode = code };

    [GeneratedRegex("^[a-z][a-z0-9-]{0,63}$")]
    private static partial Regex DocumentId();
    [GeneratedRegex(@"\]\(([^\s)]+)\)")]
    private static partial Regex MarkdownLink();
}
