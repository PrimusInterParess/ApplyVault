namespace ApplyVault.Api.Services.InterviewPrep.Domain;

/// <summary>
/// Planned per–main-question language for mixed sessions (no random mid-interview switching).
/// </summary>
public static class InterviewPrepTurnLanguage
{
    public static string? ToWireOrNull(InterviewPrepLanguage language) =>
        language == InterviewPrepLanguage.English
            ? InterviewPrepEnumNames.ToWire(InterviewPrepLanguage.English)
            : InterviewPrepEnumNames.ToWire(language);

    public static IReadOnlyList<InterviewPlanLanguageSlot> BuildAllocation(
        InterviewPrepLanguage sessionLanguage,
        int mainQuestionSlots)
    {
        if (mainQuestionSlots <= 0)
        {
            return [];
        }

        return sessionLanguage switch
        {
            InterviewPrepLanguage.Danish => Enumerable.Range(0, mainQuestionSlots)
                .Select((index) => new InterviewPlanLanguageSlot(index, InterviewPrepLanguage.Danish))
                .ToArray(),
            InterviewPrepLanguage.MixedEnglishDanish => Enumerable.Range(0, mainQuestionSlots)
                .Select((index) => new InterviewPlanLanguageSlot(
                    index,
                    index % 2 == 0 ? InterviewPrepLanguage.English : InterviewPrepLanguage.Danish))
                .ToArray(),
            _ => Enumerable.Range(0, mainQuestionSlots)
                .Select((index) => new InterviewPlanLanguageSlot(index, InterviewPrepLanguage.English))
                .ToArray()
        };
    }

    public static InterviewPrepLanguage ResolveForMainQuestion(
        InterviewPrepLanguage sessionLanguage,
        IReadOnlyList<InterviewPlanLanguageSlot>? allocation,
        int mainQuestionIndex)
    {
        if (allocation is { Count: > 0 })
        {
            var slot = allocation.FirstOrDefault((entry) => entry.MainQuestionIndex == mainQuestionIndex);
            if (slot is not null)
            {
                return slot.Language;
            }
        }

        return sessionLanguage switch
        {
            InterviewPrepLanguage.Danish => InterviewPrepLanguage.Danish,
            InterviewPrepLanguage.MixedEnglishDanish => mainQuestionIndex % 2 == 0
                ? InterviewPrepLanguage.English
                : InterviewPrepLanguage.Danish,
            _ => InterviewPrepLanguage.English
        };
    }

    public static bool RequiresTurnLanguageTags(InterviewPrepLanguage language) =>
        language is InterviewPrepLanguage.Danish or InterviewPrepLanguage.MixedEnglishDanish;

    public static bool SessionUsesLanguageFeedback(InterviewPrepLanguage language, InterviewPrepMode mode) =>
        RequiresTurnLanguageTags(language) || mode == InterviewPrepMode.LanguagePractice;
}

public sealed record InterviewPlanLanguageSlot(int MainQuestionIndex, InterviewPrepLanguage Language);
