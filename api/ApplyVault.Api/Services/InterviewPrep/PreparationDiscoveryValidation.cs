using ApplyVault.Api.Models.InterviewPrep;
using ApplyVault.Api.Services.InterviewPrep.Domain;

namespace ApplyVault.Api.Services.InterviewPrep;

internal static class PreparationDiscoveryValidation
{
    internal static PreparationPackContent DraftContent(PreparationDiscoveryPlan plan) => new(
        plan.Title, plan.OverviewMarkdown, plan.Unknowns, plan.Topics,
        plan.Documents.Select(d => new PreparationDocument(d.Id, d.Title, d.Purpose, "", d.PrerequisiteIds, false)).ToArray(),
        plan.StudyPlan);

    internal static void Plan(PreparationDiscoveryPlan? plan, PreparationPackInput input)
    {
        if (plan?.Documents is null || plan.Documents.Any(d => d is null)) Invalid();
        InterviewPreparationPackValidation.Content(DraftContent(plan!), input, allowPending: true);
        // A planned document may collect questions later; the write transition requires them.
        Discovery(new(plan!.Documents, []), DraftContent(plan));
    }

    internal static void DocumentOutline(PreparationDocumentOutline? outline, PreparationDocument document)
    {
        if (outline is null || outline.Id != document.Id || outline.Title != document.Title || outline.Purpose != document.Purpose
            || outline.PrerequisiteIds is null || !outline.PrerequisiteIds.SequenceEqual(document.PrerequisiteIds)) Invalid();
        Questions(outline!, new HashSet<string>(), requireQuestions: true);
    }

    internal static void Discovery(PreparationDiscovery? discovery, PreparationPackContent content, bool requireQuestions = false)
    {
        if (discovery is null || discovery.Documents is null || discovery.Answers is null || discovery.ContextRevision < 0
            || discovery.Documents.Count != content.Documents.Count || discovery.Answers.Count > 36) Invalid();
        var seenIds = new HashSet<string>();
        var seenDocuments = new HashSet<string>();
        foreach (var outline in discovery!.Documents)
        {
            if (outline is null || outline.ContextRevision < 0 || outline.ContextRevision > discovery.ContextRevision || !seenDocuments.Add(outline.Id)) Invalid();
            var document = content.Documents.FirstOrDefault(d => d.Id == outline!.Id);
            if (document is null || outline!.Title != document.Title || outline.Purpose != document.Purpose
                || outline.PrerequisiteIds is null || !outline.PrerequisiteIds.SequenceEqual(document.PrerequisiteIds)) Invalid();
            Questions(outline, seenIds, requireQuestions);
        }
        var seenAnswers = new HashSet<string>();
        foreach (var answer in discovery.Answers)
        {
            if (answer is null || !seenAnswers.Add(answer.QuestionId) || answer.Text is null || answer.Text.Length > 4000
                || !discovery.Documents.Any(d => d.Id == answer.DocumentId && d.Questions.Any(q => q.Id == answer.QuestionId))) Invalid();
            ValidateStatus(answer!.Status, answer.Text, generated: true);
        }
    }

    internal static IReadOnlyList<PreparationDiscoveryAnswer> Answers(string documentId,
        PreparationDocumentOutline outline, IReadOnlyList<PreparationAnswerInput>? inputs)
    {
        if (inputs is null || inputs.Count != outline.Questions.Count || inputs.Any(a => a is null)
            || !inputs.Select(a => a.QuestionId).ToHashSet().SetEquals(outline.Questions.Select(q => q.Id)))
            throw new InterviewPrepValidationException("Supply an answer, uncertainty, skip, or pending state for each question in this document.");
        return inputs.Select(a =>
        {
            var text = a.Status is "skipped" or "pending" ? "" : a.Text?.Trim() ?? "";
            ValidateStatus(a.Status, text, generated: false);
            return new PreparationDiscoveryAnswer(documentId, a.QuestionId, a.Status, text);
        }).ToArray();
    }

    internal static void Help(PreparationHelpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Request) || request.Request.Length > 2000
            || request.DraftAnswer?.Length > 4000 || request.PreviousGuidance?.Length > 12000)
            throw new InterviewPrepValidationException("Ask a guidance question of at most 2000 characters.");
    }

    private static void Questions(PreparationDocumentOutline outline, HashSet<string> seenIds, bool requireQuestions)
    {
        if (outline.Questions is null || outline.Questions.Count > 3 || (requireQuestions && outline.Questions.Count < 1)) Invalid("question_count");
        foreach (var q in outline.Questions!)
        {
            if (q is null || string.IsNullOrEmpty(q.Id) || q.Id.Length > 96 || !q.Id.StartsWith(outline.Id + "-", StringComparison.Ordinal)
                || !q.Id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') || !seenIds.Add(q.Id)) Invalid("question_id");
            if (q.Kind is not ("knowledge" or "experience" or "preference")) Invalid("question_kind");
            if (string.IsNullOrWhiteSpace(q.Prompt) || q.Prompt.Length > 1000
                || string.IsNullOrWhiteSpace(q.WhyItMatters) || q.WhyItMatters.Length > 1000
                || string.IsNullOrWhiteSpace(q.GuidanceMarkdown) || q.GuidanceMarkdown.Length > 6000
                || string.IsNullOrWhiteSpace(q.AnswerStarter) || q.AnswerStarter.Length > 1500
                || string.IsNullOrWhiteSpace(q.HypotheticalExample) || q.HypotheticalExample.Length > 2500) Invalid("question_guidance");
        }
    }

    private static void ValidateStatus(string status, string text, bool generated)
    {
        if ((status is "answered" or "unknown" or "skipped" or "pending") && text.Length <= 4000
            && (status != "answered" || !string.IsNullOrWhiteSpace(text))) return;
        if (generated) Invalid();
        throw new InterviewPrepValidationException("Choose answered, I don't know, skip, or pending. Written answers must contain text and be at most 4000 characters.");
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Invalid(string code = "discovery_structure") => throw new InterviewPrepAiUnavailableException("The discovery outline was incomplete or inconsistent. Please try again; saved material was preserved.") { FailureCode = code };
}
