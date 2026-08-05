using ApplyVault.Api.Services.InterviewPrep.Domain;

namespace ApplyVault.Api.Services.InterviewPrep.Catalogs;

public sealed record InterviewCaseFactDefinition(
    string FactId,
    string Text,
    bool IsFixed);

public sealed record InterviewCaseHintStep(
    int Level,
    string Text);

public sealed record InterviewCaseComplication(
    string ComplicationId,
    string TriggerAfterMainQuestions,
    string Narrative);

public sealed record InterviewCaseRubricCriterion(
    string CriterionId,
    string Description,
    int Weight);

public sealed record InterviewCaseCompletionConditions(
    int MinimumMainQuestionsBeforeRecommendation,
    bool RequireAtLeastOneClarificationOrHint,
    int MinimumRecommendationAnswerLength);

public sealed record InterviewCaseDefinition(
    string CaseId,
    string Title,
    string InitialSituation,
    string Objective,
    IReadOnlyList<InterviewCaseFactDefinition> FixedFacts,
    IReadOnlyList<InterviewCaseFactDefinition> FactsOnRequest,
    IReadOnlyList<string> Constraints,
    IReadOnlyList<InterviewCaseComplication> PlannedComplications,
    IReadOnlyList<InterviewCaseHintStep> HintLadder,
    IReadOnlyList<InterviewCaseRubricCriterion> Rubric,
    InterviewCaseCompletionConditions Completion);

public interface IInterviewPrepCaseCatalog
{
    IReadOnlyList<InterviewCaseDefinition> All { get; }

    InterviewCaseDefinition Get(string caseId);

    InterviewCaseDefinition ResolveDefaultForSession(string? jobTitle, string? companyName);
}

public sealed class InterviewPrepCaseCatalog : IInterviewPrepCaseCatalog
{
    private static readonly InterviewCaseDefinition[] Definitions =
    [
        new(
            CaseId: "b2b-saas-churn-spike",
            Title: "B2B SaaS churn spike",
            InitialSituation:
                "You are the product lead for a mid-market B2B SaaS analytics platform. "
                + "Over the last two quarters, logo churn rose from 4% to 9% while new sales stayed flat. "
                + "Support tickets mention confusing onboarding and missing integrations.",
            Objective:
                "Diagnose likely drivers, propose a prioritized plan, and explain trade-offs for the next 90 days.",
            FixedFacts:
            [
                new("fixed-churn-window", "Churn increase started exactly after a pricing tier change eight months ago.", true),
                new("fixed-segment", "Most churn is in accounts under 50 seats; enterprise renewals are stable.", true)
            ],
            FactsOnRequest:
            [
                new("fact-onboarding-nps", "Onboarding NPS dropped from 42 to 28 after self-serve signup launched.", false),
                new("fact-integration-gap", "The top requested integration (Salesforce) is still on the roadmap, not shipped.", false),
                new("fact-support-load", "Support volume per active account is up 35% quarter over quarter.", false)
            ],
            Constraints:
            [
                "Engineering capacity is fixed for one quarter — no net-new headcount.",
                "Pricing rollback is politically difficult; leadership wants retention fixes first.",
                "Do not change established revenue recognition facts during the case."
            ],
            PlannedComplications:
            [
                new(
                    "competitor-bundle",
                    TriggerAfterMainQuestions: "2",
                    Narrative:
                        "Mid-case update: a competitor just bundled analytics free with their CRM suite. "
                        + "Two at-risk accounts cite this in exit interviews.")
            ],
            HintLadder:
            [
                new(1, "Structure your answer: clarify goals, diagnose with data, then prioritize actions with owners."),
                new(2, "Segment churn by cohort and seat band before proposing fixes."),
                new(3, "Consider onboarding, integrations, and pricing perception as separate hypotheses to test.")
            ],
            Rubric:
            [
                new("hypothesis", "Forms testable hypotheses tied to provided facts.", 3),
                new("prioritization", "Prioritizes under stated constraints with clear trade-offs.", 3),
                new("recommendation", "Delivers a actionable 90-day recommendation with success metrics.", 4)
            ],
            Completion: new(
                MinimumMainQuestionsBeforeRecommendation: 2,
                RequireAtLeastOneClarificationOrHint: false,
                MinimumRecommendationAnswerLength: 80)),
        new(
            CaseId: "fulfillment-backlog-surge",
            Title: "Fulfillment backlog surge",
            InitialSituation:
                "You run operations for an e-commerce fulfillment network. "
                + "Order volume grew 40% after a marketing campaign, but on-time shipment rate fell from 97% to 86%. "
                + "Warehouse overtime costs are up and customer complaints mention late deliveries.",
            Objective:
                "Stabilize service levels, protect margin, and recommend a sustainable operating plan.",
            FixedFacts:
            [
                new("fixed-peak-window", "The surge aligns with a two-week campaign; baseline volume is 15% above last year.", true),
                new("fixed-bottleneck", "Pick-and-pack stations are the primary bottleneck; inbound receiving is not saturated.", true)
            ],
            FactsOnRequest:
            [
                new("fact-carrier-sla", "Primary carrier missed SLA on 12% of lanes last week, up from 3%.", false),
                new("fact-inventory-mismatch", "15% of delayed orders had inventory sync lag between web and WMS.", false),
                new("fact-staffing", "Temporary staff onboarding takes five days; only 60% of requested temps were approved.", false)
            ],
            Constraints:
            [
                "Capital budget for new automation is frozen this quarter.",
                "Customer promise date on the website cannot be rolled back without executive approval.",
                "Case facts remain fixed — do not invent new warehouse counts or SLA numbers at runtime."
            ],
            PlannedComplications:
            [
                new(
                    "comp-weather-delay",
                    TriggerAfterMainQuestions: "2",
                    Narrative:
                        "Update: severe weather closes a regional hub for 48 hours, affecting 18% of daily outbound volume.")
            ],
            HintLadder:
            [
                new(1, "Separate short-term firefighting from structural fixes."),
                new(2, "Quantify impact across receiving, pick-pack, and carrier handoff."),
                new(3, "Propose metrics: on-time ship rate, cost per order, and backlog age.")
            ],
            Rubric:
            [
                new("diagnosis", "Uses facts to localize bottleneck and failure modes.", 3),
                new("operations", "Balances service recovery with cost and staffing constraints.", 3),
                new("recommendation", "Clear phased plan with owners and measurable targets.", 4)
            ],
            Completion: new(
                MinimumMainQuestionsBeforeRecommendation: 2,
                RequireAtLeastOneClarificationOrHint: false,
                MinimumRecommendationAnswerLength: 80))
    ];

    private static readonly Dictionary<string, InterviewCaseDefinition> ById =
        Definitions.ToDictionary((definition) => definition.CaseId, StringComparer.Ordinal);

    public IReadOnlyList<InterviewCaseDefinition> All => Definitions;

    public InterviewCaseDefinition Get(string caseId)
    {
        if (ById.TryGetValue(caseId.Trim(), out var definition))
        {
            return definition;
        }

        throw new InterviewPrepValidationException($"Unknown case id '{caseId}'.");
    }

    public InterviewCaseDefinition ResolveDefaultForSession(string? jobTitle, string? companyName)
    {
        if (!string.IsNullOrWhiteSpace(jobTitle)
            && jobTitle.Contains("operation", StringComparison.OrdinalIgnoreCase))
        {
            return Get("fulfillment-backlog-surge");
        }

        _ = companyName;
        return Get("b2b-saas-churn-spike");
    }
}
