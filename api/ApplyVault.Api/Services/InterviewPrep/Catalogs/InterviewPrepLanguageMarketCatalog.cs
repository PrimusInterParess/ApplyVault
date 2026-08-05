using ApplyVault.Api.Services.InterviewPrep.Domain;

namespace ApplyVault.Api.Services.InterviewPrep.Catalogs;

/// <summary>
/// Language/market guidance for prompts and planning (M9). Profession-agnostic; no stereotypes.
/// </summary>
public static class InterviewPrepLanguageMarketCatalog
{
    public static void EnsureOperational(InterviewPrepLanguage language, InterviewPrepMarket market)
    {
        if (!OperationalLanguages.Contains(language))
        {
            throw new InterviewPrepValidationException(
                $"Interview language '{InterviewPrepEnumNames.ToWire(language)}' is not available yet.");
        }

        if (!OperationalMarkets.Contains(market))
        {
            throw new InterviewPrepValidationException(
                $"Interview market '{InterviewPrepEnumNames.ToWire(market)}' is not available yet.");
        }
    }

    public static string PromptSupplement(InterviewPrepLanguage language, InterviewPrepMarket market)
    {
        var parts = new List<string>();
        parts.AddRange(LanguagePromptLines(language));
        parts.AddRange(MarketPromptLines(market));
        return parts.Count == 0 ? string.Empty : string.Join(' ', parts);
    }

    public static string MarketCoachingHint(InterviewPrepMarket market) =>
        market switch
        {
            InterviewPrepMarket.Danish =>
                "Danish hiring context: be direct and respectful; emphasize practical ownership and clear motivation.",
            _ => string.Empty
        };

    private static readonly HashSet<InterviewPrepLanguage> OperationalLanguages =
    [
        InterviewPrepLanguage.English,
        InterviewPrepLanguage.Danish,
        InterviewPrepLanguage.MixedEnglishDanish
    ];

    private static readonly HashSet<InterviewPrepMarket> OperationalMarkets =
    [
        InterviewPrepMarket.General,
        InterviewPrepMarket.Danish
    ];

    private static IEnumerable<string> LanguagePromptLines(InterviewPrepLanguage language) =>
        language switch
        {
            InterviewPrepLanguage.Danish =>
            [
                "Conduct interviewer messages in Danish unless the payload specifies otherwise.",
                "Assess language clarity separately from role competence when scoring fluency."
            ],
            InterviewPrepLanguage.MixedEnglishDanish =>
            [
                "Follow plannedLanguageOnTurn in the payload for each main question; do not switch languages randomly.",
                "Bilingual EN↔DA switching is normal in Danish hiring; keep tone professional in both languages."
            ],
            _ => []
        };

    private static IEnumerable<string> MarketPromptLines(InterviewPrepMarket market) =>
        market switch
        {
            InterviewPrepMarket.Danish =>
            [
                "Apply Danish-market coaching bias: direct/respectful communication, practical ownership, clear motivation.",
                "Do not rely on stereotypes about nationality or culture."
            ],
            _ => []
        };
}
