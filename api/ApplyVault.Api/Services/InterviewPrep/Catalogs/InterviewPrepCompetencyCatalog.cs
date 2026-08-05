namespace ApplyVault.Api.Services.InterviewPrep.Catalogs;

public sealed record InterviewCompetencyDefinition(
    string Id,
    string DisplayName,
    string Description,
    bool IsCore,
    bool IsRoleDepthExtension);

public interface IInterviewPrepCompetencyCatalog
{
    IReadOnlyList<InterviewCompetencyDefinition> All { get; }

    IReadOnlyList<InterviewCompetencyDefinition> Core { get; }

    bool TryGet(string? idOrAlias, out InterviewCompetencyDefinition definition);

    string? ResolveId(string? idOrAlias);
}

/// <summary>
/// Initial competency catalog: Motivation, Communication, Collaboration, Ownership,
/// Problem-solving, Execution, Leadership, plus RoleDepth as an extension point.
/// </summary>
public sealed class InterviewPrepCompetencyCatalog : IInterviewPrepCompetencyCatalog
{
    public const string Motivation = "motivation";
    public const string Communication = "communication";
    public const string Collaboration = "collaboration";
    public const string Ownership = "ownership";
    public const string ProblemSolving = "problemSolving";
    public const string Execution = "execution";
    public const string Leadership = "leadership";
    public const string RoleDepth = "roleDepth";
    public const string LanguageFluency = "languageFluency";

    private static readonly InterviewCompetencyDefinition[] Definitions =
    [
        new(Motivation, "Motivation", "Why this role/company and career direction.", IsCore: true, IsRoleDepthExtension: false),
        new(Communication, "Communication", "Clarity, listening, and stakeholder messaging.", IsCore: true, IsRoleDepthExtension: false),
        new(Collaboration, "Collaboration", "Working across teams and resolving friction.", IsCore: true, IsRoleDepthExtension: false),
        new(Ownership, "Ownership", "End-to-end accountability and follow-through.", IsCore: true, IsRoleDepthExtension: false),
        new(ProblemSolving, "Problem-solving", "Structuring ambiguous problems and trade-offs.", IsCore: true, IsRoleDepthExtension: false),
        new(Execution, "Execution", "Delivery, prioritization, and measurable outcomes.", IsCore: true, IsRoleDepthExtension: false),
        new(Leadership, "Leadership", "Influence, mentoring, and raising the bar.", IsCore: true, IsRoleDepthExtension: false),
        new(RoleDepth, "Role depth", "Role-specific depth (extension point for later modes).", IsCore: false, IsRoleDepthExtension: true),
        new(LanguageFluency, "Language fluency", "Interview language clarity and phrasing (separate from role competence).", IsCore: false, IsRoleDepthExtension: false)
    ];

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["motivation"] = Motivation,
        ["communication"] = Communication,
        ["collaboration"] = Collaboration,
        ["ownership"] = Ownership,
        ["problemSolving"] = ProblemSolving,
        ["problem-solving"] = ProblemSolving,
        ["problem_solving"] = ProblemSolving,
        ["execution"] = Execution,
        ["leadership"] = Leadership,
        ["roleDepth"] = RoleDepth,
        ["role-depth"] = RoleDepth,
        ["role_depth"] = RoleDepth,
        ["languageFluency"] = LanguageFluency,
        ["language_fluency"] = LanguageFluency,
        // Fixed-bank legacy tags → catalog ids
        ["impact"] = Execution,
        ["planning"] = Execution,
        ["prioritization"] = Execution,
        ["adaptability"] = ProblemSolving,
        ["ambiguity"] = ProblemSolving,
        ["influence"] = Leadership,
        ["learning"] = Ownership,
        ["logistics"] = Motivation,
        ["relevant experience"] = RoleDepth,
        ["role fit"] = Motivation,
        ["general background"] = Motivation
    };

    public IReadOnlyList<InterviewCompetencyDefinition> All => Definitions;

    public IReadOnlyList<InterviewCompetencyDefinition> Core =>
        Definitions.Where((definition) => definition.IsCore).ToArray();

    public bool TryGet(string? idOrAlias, out InterviewCompetencyDefinition definition)
    {
        definition = null!;
        var id = ResolveId(idOrAlias);
        if (id is null)
        {
            return false;
        }

        definition = Definitions.First((entry) => entry.Id == id);
        return true;
    }

    public string? ResolveId(string? idOrAlias)
    {
        if (string.IsNullOrWhiteSpace(idOrAlias))
        {
            return null;
        }

        var trimmed = idOrAlias.Trim();
        if (Aliases.TryGetValue(trimmed, out var mapped))
        {
            return mapped;
        }

        return Definitions.Any((entry) => string.Equals(entry.Id, trimmed, StringComparison.OrdinalIgnoreCase))
            ? Definitions.First((entry) => string.Equals(entry.Id, trimmed, StringComparison.OrdinalIgnoreCase)).Id
            : null;
    }
}
