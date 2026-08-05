using ApplyVault.Api.Services.InterviewPrep.Domain;

namespace ApplyVault.Api.Services.InterviewPrep.Catalogs;

public sealed record InterviewModeDefinition(
    InterviewPrepMode Mode,
    string DisplayName,
    string Description,
    IReadOnlyList<string> PriorityCompetencyIds,
    int DefaultQuestionBudget,
    int DefaultProbeBudget);

public sealed record InterviewPersonaDefinition(
    InterviewPrepPersona Persona,
    string DisplayName,
    string Description,
    IReadOnlyList<string> EmphasisCompetencyIds,
    string QuestionStyleHint);

public interface IInterviewPrepModeCatalog
{
    IReadOnlyList<InterviewModeDefinition> All { get; }

    InterviewModeDefinition Get(InterviewPrepMode mode);
}

public interface IInterviewPrepPersonaCatalog
{
    IReadOnlyList<InterviewPersonaDefinition> All { get; }

    InterviewPersonaDefinition Get(InterviewPrepPersona persona);
}

public sealed class InterviewPrepModeCatalog : IInterviewPrepModeCatalog
{
    private static readonly InterviewModeDefinition[] Definitions =
    [
        new(
            InterviewPrepMode.ScreeningAndMotivation,
            "Screening & motivation",
            "Early-stage screen: motivation, fit, and high-level ownership signals.",
            [
                InterviewPrepCompetencyCatalog.Motivation,
                InterviewPrepCompetencyCatalog.Communication,
                InterviewPrepCompetencyCatalog.Ownership,
                InterviewPrepCompetencyCatalog.Execution
            ],
            DefaultQuestionBudget: 4,
            DefaultProbeBudget: 1),
        new(
            InterviewPrepMode.BehavioralAndCulture,
            "Behavioral & culture",
            "Behavioral depth across collaboration, problem-solving, and leadership.",
            [
                InterviewPrepCompetencyCatalog.Collaboration,
                InterviewPrepCompetencyCatalog.Communication,
                InterviewPrepCompetencyCatalog.ProblemSolving,
                InterviewPrepCompetencyCatalog.Leadership,
                InterviewPrepCompetencyCatalog.Ownership
            ],
            DefaultQuestionBudget: 4,
            DefaultProbeBudget: 2),
        new(
            InterviewPrepMode.RoleAndDomainDepth,
            "Role & domain depth",
            "Role-specific technical and domain depth aligned to the job snapshot when present.",
            [
                InterviewPrepCompetencyCatalog.RoleDepth,
                InterviewPrepCompetencyCatalog.ProblemSolving,
                InterviewPrepCompetencyCatalog.Execution,
                InterviewPrepCompetencyCatalog.Ownership,
                InterviewPrepCompetencyCatalog.Communication
            ],
            DefaultQuestionBudget: 5,
            DefaultProbeBudget: 2),
        new(
            InterviewPrepMode.ProcessAndSystems,
            "Process & systems",
            "How the candidate designs processes, scales systems, and drives cross-team execution.",
            [
                InterviewPrepCompetencyCatalog.ProblemSolving,
                InterviewPrepCompetencyCatalog.Execution,
                InterviewPrepCompetencyCatalog.Collaboration,
                InterviewPrepCompetencyCatalog.Ownership,
                InterviewPrepCompetencyCatalog.Leadership
            ],
            DefaultQuestionBudget: 4,
            DefaultProbeBudget: 2),
        new(
            InterviewPrepMode.ProblemSolvingCase,
            "Problem-solving case",
            "Structured case interview with fixed facts, controlled hints, and a final recommendation.",
            [
                InterviewPrepCompetencyCatalog.ProblemSolving,
                InterviewPrepCompetencyCatalog.Execution,
                InterviewPrepCompetencyCatalog.Communication,
                InterviewPrepCompetencyCatalog.Ownership
            ],
            DefaultQuestionBudget: 5,
            DefaultProbeBudget: 1),
        new(
            InterviewPrepMode.LanguagePractice,
            "Language practice",
            "Fluency and phrasing in the chosen language mix with lighter role-depth pressure.",
            [
                InterviewPrepCompetencyCatalog.LanguageFluency,
                InterviewPrepCompetencyCatalog.Communication,
                InterviewPrepCompetencyCatalog.Motivation
            ],
            DefaultQuestionBudget: 4,
            DefaultProbeBudget: 1),
        new(
            InterviewPrepMode.FullLoop,
            "Full loop",
            "Multi-stage panel simulation: screening, behavioral, domain depth, case, and bar raiser.",
            [
                InterviewPrepCompetencyCatalog.Motivation,
                InterviewPrepCompetencyCatalog.Collaboration,
                InterviewPrepCompetencyCatalog.Communication,
                InterviewPrepCompetencyCatalog.ProblemSolving,
                InterviewPrepCompetencyCatalog.RoleDepth,
                InterviewPrepCompetencyCatalog.Execution,
                InterviewPrepCompetencyCatalog.Ownership,
                InterviewPrepCompetencyCatalog.Leadership
            ],
            DefaultQuestionBudget: 6,
            DefaultProbeBudget: 2)
    ];

    public IReadOnlyList<InterviewModeDefinition> All => Definitions;

    public InterviewModeDefinition Get(InterviewPrepMode mode) =>
        Definitions.FirstOrDefault((entry) => entry.Mode == mode)
        ?? throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported interview mode.");
}

public sealed class InterviewPrepPersonaCatalog : IInterviewPrepPersonaCatalog
{
    private static readonly InterviewPersonaDefinition[] Definitions =
    [
        new(
            InterviewPrepPersona.Recruiter,
            "Recruiter",
            "Screens for motivation, communication clarity, and collaboration signals.",
            [
                InterviewPrepCompetencyCatalog.Motivation,
                InterviewPrepCompetencyCatalog.Communication,
                InterviewPrepCompetencyCatalog.Collaboration
            ],
            QuestionStyleHint: "concise_screen"),
        new(
            InterviewPrepPersona.HiringManager,
            "Hiring manager",
            "Probes ownership, problem-solving, execution, and leadership depth.",
            [
                InterviewPrepCompetencyCatalog.Ownership,
                InterviewPrepCompetencyCatalog.ProblemSolving,
                InterviewPrepCompetencyCatalog.Execution,
                InterviewPrepCompetencyCatalog.Leadership
            ],
            QuestionStyleHint: "depth_probe"),
        new(
            InterviewPrepPersona.SeniorPeer,
            "Senior peer",
            "Peer-level technical depth: trade-offs, system design, and hands-on execution signals.",
            [
                InterviewPrepCompetencyCatalog.ProblemSolving,
                InterviewPrepCompetencyCatalog.Execution,
                InterviewPrepCompetencyCatalog.Ownership,
                InterviewPrepCompetencyCatalog.RoleDepth,
                InterviewPrepCompetencyCatalog.Leadership
            ],
            QuestionStyleHint: "peer_technical_depth"),
        new(
            InterviewPrepPersona.BarRaiser,
            "Bar raiser",
            "Calm consistency review: explore contradictions in evidence without hostility or trick questions.",
            [
                InterviewPrepCompetencyCatalog.Communication,
                InterviewPrepCompetencyCatalog.ProblemSolving,
                InterviewPrepCompetencyCatalog.Ownership,
                InterviewPrepCompetencyCatalog.Leadership
            ],
            QuestionStyleHint: "bar_raiser_consistency")
    ];

    public IReadOnlyList<InterviewPersonaDefinition> All => Definitions;

    public InterviewPersonaDefinition Get(InterviewPrepPersona persona) =>
        Definitions.FirstOrDefault((entry) => entry.Persona == persona)
        ?? throw new ArgumentOutOfRangeException(nameof(persona), persona, "Unsupported interview persona.");
}
