using ApplyVault.Api.Services.InterviewPrep.Domain;
using ApplyVault.Api.Services.InterviewPrep.Planning;

namespace ApplyVault.Api.Services.InterviewPrep.FullLoop;

/// <summary>
/// Deterministic full-loop stage sequence aligned with operational modes (M7–M9).
/// </summary>
public static class InterviewPrepFullLoopCatalog
{
    public static IReadOnlyList<InterviewPrepFullLoopStageSlot> StandardStageSlots() =>
    [
        new(
            "recruiterScreening",
            "Recruiter screening and motivation",
            InterviewPrepMode.ScreeningAndMotivation,
            InterviewPrepPersona.Recruiter,
            []),
        new(
            "hiringManagerScreening",
            "Hiring manager role fit and expectations",
            InterviewPrepMode.ScreeningAndMotivation,
            InterviewPrepPersona.HiringManager,
            []),
        new(
            "behavioralAndCulture",
            "Behavioral and culture signals",
            InterviewPrepMode.BehavioralAndCulture,
            InterviewPrepPersona.HiringManager,
            []),
        new(
            "seniorPeerDomain",
            "Senior peer role and domain depth",
            InterviewPrepMode.RoleAndDomainDepth,
            InterviewPrepPersona.SeniorPeer,
            []),
        new(
            "problemSolvingCase",
            "Problem-solving case interview",
            InterviewPrepMode.ProblemSolvingCase,
            InterviewPrepPersona.HiringManager,
            []),
        new(
            "barRaiser",
            "Bar raiser consistency and leveling",
            InterviewPrepMode.RoleAndDomainDepth,
            InterviewPrepPersona.BarRaiser,
            [])
    ];

    public static InterviewPrepFullLoopOrchestration BuildDeterministicOrchestration(
        string loopSummary,
        IReadOnlyList<string> coverageGoals) =>
        new(
            loopSummary,
            coverageGoals,
            StandardStageSlots(),
            InterviewPrepArtifactSources.DeterministicFallback,
            UsedAiFallback: true);
}
