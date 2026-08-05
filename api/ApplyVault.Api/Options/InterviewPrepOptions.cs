namespace ApplyVault.Api.Options;

public sealed class InterviewPrepOptions
{
    public const string SectionName = "InterviewPrep";

    public InterviewPrepLoopGuardOptions LoopGuard { get; set; } = new();

    public InterviewPrepAiOptions Ai { get; set; } = new();
}

public sealed class InterviewPrepLoopGuardOptions
{
    public double NearDuplicateThreshold { get; set; } = 0.85;

    public int MaxExactRetries { get; set; } = 0;

    public int MaxSessionTurns { get; set; } = 40;

    /// <summary>Session-turn ceiling for Full-loop sessions (~80–100).</summary>
    public int MaxSessionTurnsFullLoop { get; set; } = 90;

    public int MaxConsecutiveSameCompetency { get; set; } = 3;

    /// <summary>Max times the same question intent may be reused before fallback.</summary>
    public int MaxIntentRepeats { get; set; } = 1;

    /// <summary>Max follow-up/probe turns under the same intent.</summary>
    public int MaxFollowUpsPerIntent { get; set; } = 2;

    /// <summary>Consecutive no-progress assessments before forcing competency/intent advance.</summary>
    public int MaxNoProgressStreak { get; set; } = 2;

    /// <summary>Max times the same target-evidence key may be pursued.</summary>
    public int MaxTargetEvidenceRepeats { get; set; } = 2;

    /// <summary>Max wording regenerations after Loop Guard rejects AI text.</summary>
    public int MaxWordingRetries { get; set; } = 3;
}
