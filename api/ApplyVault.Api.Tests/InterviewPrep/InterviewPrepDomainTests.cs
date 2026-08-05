using ApplyVault.Api.Options;
using ApplyVault.Api.Services.InterviewPrep.Domain;

namespace ApplyVault.Api.Tests.InterviewPrep;

public sealed class InterviewPrepStateMachineTests
{
    [Theory]
    [InlineData(InterviewPrepSessionStatus.Created, InterviewPrepSessionStatus.Preparing, true)]
    [InlineData(InterviewPrepSessionStatus.Preparing, InterviewPrepSessionStatus.Ready, true)]
    [InlineData(InterviewPrepSessionStatus.Ready, InterviewPrepSessionStatus.InProgress, true)]
    [InlineData(InterviewPrepSessionStatus.InProgress, InterviewPrepSessionStatus.Paused, true)]
    [InlineData(InterviewPrepSessionStatus.Paused, InterviewPrepSessionStatus.InProgress, true)]
    [InlineData(InterviewPrepSessionStatus.InProgress, InterviewPrepSessionStatus.Completing, true)]
    [InlineData(InterviewPrepSessionStatus.Completing, InterviewPrepSessionStatus.Completed, true)]
    [InlineData(InterviewPrepSessionStatus.Completed, InterviewPrepSessionStatus.InProgress, false)]
    [InlineData(InterviewPrepSessionStatus.Cancelled, InterviewPrepSessionStatus.Ready, false)]
    [InlineData(InterviewPrepSessionStatus.Ready, InterviewPrepSessionStatus.Preparing, false)]
    public void Session_transitions_are_validated(
        InterviewPrepSessionStatus from,
        InterviewPrepSessionStatus to,
        bool expected)
    {
        Assert.Equal(expected, InterviewPrepSessionStateMachine.CanTransition(from, to));

        if (expected)
        {
            InterviewPrepSessionStateMachine.EnsureTransition(from, to);
        }
        else
        {
            Assert.Throws<InterviewPrepConflictException>(() =>
                InterviewPrepSessionStateMachine.EnsureTransition(from, to));
        }
    }

    [Fact]
    public void Stage_can_skip_warmup_from_opening_to_core()
    {
        Assert.True(InterviewPrepStageStateMachine.CanTransition(
            InterviewPrepStageStatus.Opening,
            InterviewPrepStageStatus.CoreAssessment));
    }
}

public sealed class InterviewLoopGuardTests
{
    private readonly InterviewLoopGuard _guard = new();
    private readonly InterviewPrepLoopGuardOptions _options = new()
    {
        NearDuplicateThreshold = 0.85,
        MaxSessionTurns = 10
    };

    [Fact]
    public void BuildSignature_is_stable_for_normalized_text()
    {
        var left = _guard.BuildSignature("  Hello, World!  ");
        var right = _guard.BuildSignature("hello world");

        Assert.Equal(left, right);
    }

    [Fact]
    public void Evaluate_rejects_exact_duplicate()
    {
        var text = "Tell me about yourself.";
        var signature = _guard.BuildSignature(text);
        var decision = _guard.Evaluate(
            text,
            [new InterviewLoopGuardHistoryItem(signature, text, "motivation")],
            _options);

        Assert.False(decision.Accepted);
        Assert.True(decision.IsExactDuplicate);
    }

    [Fact]
    public void Evaluate_rejects_near_duplicate()
    {
        var prior = "Tell me about a recent achievement you are proud of.";
        var proposed = "Tell me about a recent achievement you are proud of today.";
        var decision = _guard.Evaluate(
            proposed,
            [new InterviewLoopGuardHistoryItem(_guard.BuildSignature(prior), prior, "impact")],
            _options);

        Assert.False(decision.Accepted);
        Assert.True(decision.IsNearDuplicate);
    }

    [Fact]
    public void EvaluateProposal_rejects_intent_loop()
    {
        var decision = _guard.EvaluateProposal(
            new InterviewLoopGuardProposal(
                QuestionText: null,
                CompetencyId: "motivation",
                IntentId: "intent-01-motivation",
                TargetEvidenceKey: "evidence:motivation",
                ActionType: InterviewPrepRuntimeActionType.AskQuestion),
            new InterviewLoopGuardRuntimeState(
                History: [],
                MainQuestionCount: 1,
                ConsecutiveNoProgress: 0,
                FollowUpsForCurrentIntent: 0,
                IntentRepeatCount: 1,
                TargetEvidenceRepeatCount: 0,
                ConsecutiveSameCompetency: 0),
            new InterviewPrepLoopGuardOptions { MaxIntentRepeats = 1 });

        Assert.False(decision.Accepted);
        Assert.Equal("intent_loop", decision.Reason);
    }

    [Fact]
    public void EvaluateProposal_rejects_signature_duplicate_on_wording()
    {
        var text = "What was your specific contribution?";
        var signature = _guard.BuildSignature(text);
        var decision = _guard.EvaluateProposal(
            new InterviewLoopGuardProposal(
                text,
                "ownership",
                "intent-ownership",
                "evidence:ownership",
                InterviewPrepRuntimeActionType.AskQuestion,
                IsWordingOnly: true),
            new InterviewLoopGuardRuntimeState(
                [new InterviewLoopGuardHistoryItem(signature, text, "ownership", "intent-ownership", "evidence:ownership")],
                MainQuestionCount: 1,
                ConsecutiveNoProgress: 0,
                FollowUpsForCurrentIntent: 0,
                IntentRepeatCount: 1,
                TargetEvidenceRepeatCount: 1,
                ConsecutiveSameCompetency: 1),
            _options);

        Assert.False(decision.Accepted);
        Assert.Equal("exact_duplicate", decision.Reason);
    }

    [Fact]
    public void EvaluateProposal_rejects_no_progress_budget()
    {
        var decision = _guard.EvaluateProposal(
            new InterviewLoopGuardProposal(
                QuestionText: null,
                CompetencyId: "ownership",
                IntentId: "intent-ownership",
                TargetEvidenceKey: "evidence:ownership",
                ActionType: InterviewPrepRuntimeActionType.Probe),
            new InterviewLoopGuardRuntimeState(
                History: [],
                MainQuestionCount: 2,
                ConsecutiveNoProgress: 2,
                FollowUpsForCurrentIntent: 0,
                IntentRepeatCount: 0,
                TargetEvidenceRepeatCount: 0,
                ConsecutiveSameCompetency: 1),
            new InterviewPrepLoopGuardOptions { MaxNoProgressStreak = 2 });

        Assert.False(decision.Accepted);
        Assert.Equal("no_progress_budget", decision.Reason);
    }

    [Fact]
    public void EvaluateProposal_rejects_target_evidence_loop()
    {
        var decision = _guard.EvaluateProposal(
            new InterviewLoopGuardProposal(
                QuestionText: null,
                CompetencyId: "execution",
                IntentId: "intent-execution",
                TargetEvidenceKey: "evidence:execution",
                ActionType: InterviewPrepRuntimeActionType.AskQuestion),
            new InterviewLoopGuardRuntimeState(
                History: [],
                MainQuestionCount: 2,
                ConsecutiveNoProgress: 0,
                FollowUpsForCurrentIntent: 0,
                IntentRepeatCount: 0,
                TargetEvidenceRepeatCount: 2,
                ConsecutiveSameCompetency: 0),
            new InterviewPrepLoopGuardOptions { MaxTargetEvidenceRepeats = 2 });

        Assert.False(decision.Accepted);
        Assert.Equal("target_evidence_loop", decision.Reason);
    }

    [Fact]
    public void Evaluate_accepts_distinct_question()
    {
        var prior = "Tell me about yourself.";
        var proposed = "What are your salary expectations?";
        var decision = _guard.Evaluate(
            proposed,
            [new InterviewLoopGuardHistoryItem(_guard.BuildSignature(prior), prior, "motivation")],
            _options);

        Assert.True(decision.Accepted);
    }
}

public sealed class InterviewPrepIdempotencyHelperTests
{
    [Fact]
    public void ETag_round_trips_concurrency_stamp()
    {
        var stamp = Guid.NewGuid();
        var etag = Services.InterviewPrep.InterviewPrepSessionService.ToETag(stamp);

        Assert.Equal($"\"{stamp:N}\"", etag);
    }
}
