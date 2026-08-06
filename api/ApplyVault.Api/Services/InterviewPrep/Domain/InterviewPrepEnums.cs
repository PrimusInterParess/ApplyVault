using System.Text.Json;

namespace ApplyVault.Api.Services.InterviewPrep.Domain;

public enum InterviewPrepMode
{
    ScreeningAndMotivation,
    BehavioralAndCulture,
    RoleAndDomainDepth,
    ProcessAndSystems,
    ProblemSolvingCase,
    LanguagePractice,
    FullLoop
}

public enum InterviewPrepPersona
{
    Recruiter,
    HiringManager,
    SeniorPeer,
    BarRaiser
}

public enum InterviewPrepLanguage
{
    English,
    Danish,
    MixedEnglishDanish
}

public enum InterviewPrepMarket
{
    General,
    Danish
}

public enum InterviewPrepExperienceType
{
    RealisticSimulation,
    GuidedCoaching
}

public enum InterviewPrepInteractionType
{
    Text
}

public enum InterviewPrepSessionStatus
{
    Created,
    Preparing,
    Ready,
    InProgress,
    Paused,
    Completing,
    Completed,
    Cancelled,
    Failed
}

public enum InterviewPrepStageStatus
{
    Planned,
    Opening,
    WarmUp,
    CoreAssessment,
    CandidateQuestions,
    Closing,
    AssessmentPending,
    Assessed,
    Completed
}

public enum InterviewPrepTurnRole
{
    System,
    Interviewer,
    Candidate,
    Coach
}

/// <summary>Brief topic gap for durable Interview Prep study briefs (ADR-0025).</summary>
public enum InterviewPrepBriefTopicGap
{
    AlreadyStrong,
    MustStudy,
    NiceToHave,
    Unclear
}

/// <summary>Outdated reason wire values for study briefs (computed on read).</summary>
public enum InterviewPrepBriefOutdatedReason
{
    StructuredCvChanged,
    BoundJobMissing
}

public static class InterviewPrepEnumNames
{
    public static string ToWire(Enum value) =>
        JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    public static bool TryParseMode(string? value, out InterviewPrepMode mode) =>
        TryParse(value, out mode);

    public static bool TryParsePersona(string? value, out InterviewPrepPersona persona) =>
        TryParse(value, out persona);

    public static bool TryParseLanguage(string? value, out InterviewPrepLanguage language) =>
        TryParse(value, out language);

    public static bool TryParseMarket(string? value, out InterviewPrepMarket market) =>
        TryParse(value, out market);

    public static bool TryParseExperienceType(string? value, out InterviewPrepExperienceType experienceType) =>
        TryParse(value, out experienceType);

    public static bool TryParseInteractionType(string? value, out InterviewPrepInteractionType interactionType) =>
        TryParse(value, out interactionType);

    public static bool TryParseSessionStatus(string? value, out InterviewPrepSessionStatus status) =>
        TryParse(value, out status);

    public static bool TryParseStageStatus(string? value, out InterviewPrepStageStatus status) =>
        TryParse(value, out status);

    public static bool TryParseTurnRole(string? value, out InterviewPrepTurnRole role) =>
        TryParse(value, out role);

    public static bool TryParseBriefTopicGap(string? value, out InterviewPrepBriefTopicGap gap) =>
        TryParse(value, out gap);

    public static bool TryParseBriefOutdatedReason(string? value, out InterviewPrepBriefOutdatedReason reason) =>
        TryParse(value, out reason);

    private static bool TryParse<TEnum>(string? value, out TEnum result)
        where TEnum : struct, Enum
    {
        result = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (TEnum candidate in Enum.GetValues<TEnum>())
        {
            if (string.Equals(value, candidate.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, ToWire(candidate), StringComparison.OrdinalIgnoreCase))
            {
                result = candidate;
                return true;
            }
        }

        return false;
    }
}
