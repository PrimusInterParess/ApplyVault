using System.Text.Json;

namespace ApplyVault.Api.Services.InterviewPrep.Domain;

public enum InterviewPrepProgressClass
{
    StrongProgress,
    PartialProgress,
    NoProgress
}

public enum InterviewPrepRuntimeActionType
{
    Opening,
    AskQuestion,
    Probe,
    CandidateQuestions,
    WrapUp,
    Close,
    DiscloseFact,
    OfferHint,
    IntroduceComplication
}

public static class InterviewPrepRuntimeNames
{
    public static string ToWire(Enum value) =>
        JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    public static bool TryParseProgress(string? value, out InterviewPrepProgressClass result) =>
        TryParse(value, out result);

    public static bool TryParseAction(string? value, out InterviewPrepRuntimeActionType result) =>
        TryParse(value, out result);

    public static InterviewPrepProgressClass ClassifyScore(int score) =>
        score switch
        {
            >= 70 => InterviewPrepProgressClass.StrongProgress,
            >= 40 => InterviewPrepProgressClass.PartialProgress,
            _ => InterviewPrepProgressClass.NoProgress
        };

    public static InterviewPrepRuntimeActionType NormalizeAction(string? actionType)
    {
        if (string.IsNullOrWhiteSpace(actionType))
        {
            return InterviewPrepRuntimeActionType.AskQuestion;
        }

        var raw = actionType.Trim().Replace('-', '_').Replace(' ', '_');
        if (TryParse(raw, out InterviewPrepRuntimeActionType parsed))
        {
            return parsed;
        }

        return raw.ToLowerInvariant() switch
        {
            "ask_question" or "askquestion" or "question" => InterviewPrepRuntimeActionType.AskQuestion,
            "follow_up" or "followup" or "probe" => InterviewPrepRuntimeActionType.Probe,
            "candidate_questions" or "candidatequestions" => InterviewPrepRuntimeActionType.CandidateQuestions,
            "wrap_up" or "wrapup" => InterviewPrepRuntimeActionType.WrapUp,
            "close" or "closing" => InterviewPrepRuntimeActionType.Close,
            "opening" => InterviewPrepRuntimeActionType.Opening,
            "disclose_fact" or "disclosefact" => InterviewPrepRuntimeActionType.DiscloseFact,
            "offer_hint" or "offerhint" => InterviewPrepRuntimeActionType.OfferHint,
            "introduce_complication" or "introducecomplication" => InterviewPrepRuntimeActionType.IntroduceComplication,
            _ => InterviewPrepRuntimeActionType.AskQuestion
        };
    }

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
