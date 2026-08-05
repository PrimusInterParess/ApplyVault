using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ApplyVault.Api.Options;

namespace ApplyVault.Api.Services.InterviewPrep.Domain;

public interface IInterviewLoopGuard
{
    string BuildSignature(string questionText);

    InterviewLoopGuardDecision Evaluate(
        string proposedQuestionText,
        IReadOnlyList<InterviewLoopGuardHistoryItem> history,
        InterviewPrepLoopGuardOptions options);

    InterviewLoopGuardDecision EvaluateProposal(
        InterviewLoopGuardProposal proposal,
        InterviewLoopGuardRuntimeState state,
        InterviewPrepLoopGuardOptions options);
}

public sealed record InterviewLoopGuardHistoryItem(
    string Signature,
    string Text,
    string? CompetencyTag,
    string? IntentId = null,
    string? TargetEvidenceKey = null);

public sealed record InterviewLoopGuardProposal(
    string? QuestionText,
    string? CompetencyId,
    string? IntentId,
    string? TargetEvidenceKey,
    InterviewPrepRuntimeActionType ActionType,
    bool IsWordingOnly = false);

public sealed record InterviewLoopGuardRuntimeState(
    IReadOnlyList<InterviewLoopGuardHistoryItem> History,
    int MainQuestionCount,
    int ConsecutiveNoProgress,
    int FollowUpsForCurrentIntent,
    int IntentRepeatCount,
    int TargetEvidenceRepeatCount,
    int ConsecutiveSameCompetency);

public sealed record InterviewLoopGuardDecision(
    bool Accepted,
    string Signature,
    string Reason,
    bool IsExactDuplicate,
    bool IsNearDuplicate);

public sealed class InterviewLoopGuard : IInterviewLoopGuard
{
    private static readonly Regex NonWord = new(@"[^\p{L}\p{N}\s]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MultiSpace = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string BuildSignature(string questionText)
    {
        var normalized = Normalize(questionText);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public InterviewLoopGuardDecision Evaluate(
        string proposedQuestionText,
        IReadOnlyList<InterviewLoopGuardHistoryItem> history,
        InterviewPrepLoopGuardOptions options) =>
        EvaluateProposal(
            new InterviewLoopGuardProposal(
                proposedQuestionText,
                CompetencyId: null,
                IntentId: null,
                TargetEvidenceKey: null,
                ActionType: InterviewPrepRuntimeActionType.AskQuestion,
                IsWordingOnly: true),
            new InterviewLoopGuardRuntimeState(
                history,
                MainQuestionCount: history.Count,
                ConsecutiveNoProgress: 0,
                FollowUpsForCurrentIntent: 0,
                IntentRepeatCount: 0,
                TargetEvidenceRepeatCount: 0,
                ConsecutiveSameCompetency: 0),
            options);

    public InterviewLoopGuardDecision EvaluateProposal(
        InterviewLoopGuardProposal proposal,
        InterviewLoopGuardRuntimeState state,
        InterviewPrepLoopGuardOptions options)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(options);

        var text = proposal.QuestionText ?? string.Empty;
        var signature = string.IsNullOrWhiteSpace(text) ? string.Empty : BuildSignature(text);
        var normalizedProposed = Normalize(text);

        if (proposal.ActionType is InterviewPrepRuntimeActionType.AskQuestion
            or InterviewPrepRuntimeActionType.Probe
            or InterviewPrepRuntimeActionType.Opening
            or InterviewPrepRuntimeActionType.CandidateQuestions)
        {
            if (string.IsNullOrWhiteSpace(normalizedProposed) && proposal.IsWordingOnly)
            {
                return Reject(signature, "empty_question");
            }
        }

        if (state.History.Count >= Math.Max(1, options.MaxSessionTurns))
        {
            return Reject(signature, "max_session_turns");
        }

        if (!proposal.IsWordingOnly)
        {
            if (state.ConsecutiveNoProgress >= Math.Max(1, options.MaxNoProgressStreak)
                && proposal.ActionType is InterviewPrepRuntimeActionType.AskQuestion
                    or InterviewPrepRuntimeActionType.Probe)
            {
                return Reject(signature, "no_progress_budget");
            }

            if (proposal.ActionType == InterviewPrepRuntimeActionType.Probe
                && state.FollowUpsForCurrentIntent >= Math.Max(0, options.MaxFollowUpsPerIntent))
            {
                return Reject(signature, "follow_up_budget");
            }

            if (!string.IsNullOrWhiteSpace(proposal.IntentId)
                && state.IntentRepeatCount >= Math.Max(1, options.MaxIntentRepeats)
                && proposal.ActionType is InterviewPrepRuntimeActionType.AskQuestion
                    or InterviewPrepRuntimeActionType.Probe)
            {
                return Reject(signature, "intent_loop");
            }

            if (!string.IsNullOrWhiteSpace(proposal.TargetEvidenceKey)
                && state.TargetEvidenceRepeatCount >= Math.Max(1, options.MaxTargetEvidenceRepeats))
            {
                return Reject(signature, "target_evidence_loop");
            }

            if (!string.IsNullOrWhiteSpace(proposal.CompetencyId)
                && state.ConsecutiveSameCompetency >= Math.Max(1, options.MaxConsecutiveSameCompetency)
                && proposal.ActionType is InterviewPrepRuntimeActionType.AskQuestion
                    or InterviewPrepRuntimeActionType.Probe)
            {
                return Reject(signature, "competency_repeat");
            }
        }

        if (string.IsNullOrWhiteSpace(normalizedProposed))
        {
            return new InterviewLoopGuardDecision(
                Accepted: true,
                Signature: signature,
                Reason: "accepted",
                IsExactDuplicate: false,
                IsNearDuplicate: false);
        }

        var exactCount = state.History.Count((item) =>
            string.Equals(item.Signature, signature, StringComparison.Ordinal));

        if (exactCount > 0)
        {
            return new InterviewLoopGuardDecision(
                Accepted: false,
                Signature: signature,
                Reason: "exact_duplicate",
                IsExactDuplicate: true,
                IsNearDuplicate: false);
        }

        var proposedTokens = Tokenize(normalizedProposed);
        foreach (var prior in state.History)
        {
            var priorTokens = Tokenize(Normalize(prior.Text));
            var similarity = Jaccard(proposedTokens, priorTokens);
            if (similarity >= options.NearDuplicateThreshold)
            {
                return new InterviewLoopGuardDecision(
                    Accepted: false,
                    Signature: signature,
                    Reason: "near_duplicate",
                    IsExactDuplicate: false,
                    IsNearDuplicate: true);
            }
        }

        return new InterviewLoopGuardDecision(
            Accepted: true,
            Signature: signature,
            Reason: "accepted",
            IsExactDuplicate: false,
            IsNearDuplicate: false);
    }

    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var lowered = text.Trim().ToLowerInvariant();
        var stripped = NonWord.Replace(lowered, " ");
        return MultiSpace.Replace(stripped, " ").Trim();
    }

    private static InterviewLoopGuardDecision Reject(string signature, string reason) =>
        new(
            Accepted: false,
            Signature: signature,
            Reason: reason,
            IsExactDuplicate: reason is "exact_duplicate",
            IsNearDuplicate: reason is "near_duplicate");

    private static HashSet<string> Tokenize(string normalized) =>
        string.IsNullOrEmpty(normalized)
            ? []
            : normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.Ordinal);

    private static double Jaccard(HashSet<string> left, HashSet<string> right)
    {
        if (left.Count == 0 && right.Count == 0)
        {
            return 1d;
        }

        if (left.Count == 0 || right.Count == 0)
        {
            return 0d;
        }

        var intersection = left.Count((token) => right.Contains(token));
        var union = left.Count + right.Count - intersection;
        return union == 0 ? 0d : (double)intersection / union;
    }
}
