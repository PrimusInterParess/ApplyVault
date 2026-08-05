using ApplyVault.Api.Services.InterviewPrep.Ai;
using ApplyVault.Api.Services.InterviewPrep.Ai.Contracts;
using ApplyVault.Api.Services.InterviewPrep.Catalogs;
using ApplyVault.Api.Services.InterviewPrep.Domain;
using ApplyVault.Api.Services.InterviewPrep.Planning;

namespace ApplyVault.Api.Infrastructure;

/// <summary>
/// Human-readable labels for interview-prep file traces (wire value + catalog/display name).
/// </summary>
public static class InterviewPrepDebugTraceLabels
{
    private static readonly InterviewPrepModeCatalog ModeCatalog = new();
    private static readonly InterviewPrepPersonaCatalog PersonaCatalog = new();
    private static readonly InterviewPrepCompetencyCatalog CompetencyCatalog = new();

    public static string Preview(string? text, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var trimmed = text.Trim();
        return trimmed.Length <= maxLen ? trimmed : trimmed[..maxLen] + "...";
    }

    public static string Mode(string? wire) =>
        TryMode(wire, out var mode)
            ? $"{ModeCatalog.Get(mode).DisplayName} [{wire}]"
            : wire ?? "null";

    public static string Persona(string? wire) =>
        TryPersona(wire, out var persona)
            ? $"{PersonaCatalog.Get(persona).DisplayName} [{wire}]"
            : wire ?? "null";

    public static string Language(string? wire) =>
        InterviewPrepEnumNames.TryParseLanguage(wire, out var language)
            ? $"{HumanizeEnumName(language)} [{wire}]"
            : wire ?? "null";

    public static string Market(string? wire) =>
        InterviewPrepEnumNames.TryParseMarket(wire, out var market)
            ? $"{HumanizeEnumName(market)} [{wire}]"
            : wire ?? "null";

    public static string ExperienceType(string? wire) =>
        InterviewPrepEnumNames.TryParseExperienceType(wire, out var experience)
            ? $"{HumanizeEnumName(experience)} [{wire}]"
            : wire ?? "null";

    public static string InteractionType(string? wire) =>
        InterviewPrepEnumNames.TryParseInteractionType(wire, out var interaction)
            ? $"{HumanizeEnumName(interaction)} [{wire}]"
            : wire ?? "null";

    public static string SessionStatus(string? wire) =>
        InterviewPrepEnumNames.TryParseSessionStatus(wire, out var status)
            ? $"{HumanizeEnumName(status)} [{wire}]"
            : wire ?? "null";

    public static string StageStatus(string? wire) =>
        InterviewPrepEnumNames.TryParseStageStatus(wire, out var status)
            ? $"{HumanizeEnumName(status)} [{wire}]"
            : wire ?? "null";

    public static string TurnRole(string? wire) =>
        InterviewPrepEnumNames.TryParseTurnRole(wire, out var role)
            ? $"{HumanizeEnumName(role)} [{wire}]"
            : wire ?? "null";

    public static string Competency(string? competencyId)
    {
        if (string.IsNullOrWhiteSpace(competencyId))
        {
            return "null";
        }

        return CompetencyCatalog.TryGet(competencyId, out var definition)
            ? $"{definition.DisplayName} [{competencyId}]"
            : competencyId;
    }

    public static string AiOperation(InterviewPrepAiOperation operation) =>
        InterviewPrepAiOperationNames.ToName(operation);

    public static string AiErrorCode(string? code) => code switch
    {
        null or "" => "unknown",
        InterviewPrepAiErrorCodes.Disabled => "AI disabled (InterviewPrep:Ai or GoogleAi off)",
        InterviewPrepAiErrorCodes.ProviderUnavailable => "Provider unavailable (missing API key)",
        InterviewPrepAiErrorCodes.Timeout => "Timeout",
        InterviewPrepAiErrorCodes.InvalidStructuredOutput => "Invalid structured output (empty/bad JSON)",
        InterviewPrepAiErrorCodes.ValidationFailed => "Validation failed",
        InterviewPrepAiErrorCodes.TransportFailed => "Transport failed",
        InterviewPrepAiErrorCodes.OperationNotImplemented => "Operation not implemented on live transport",
        InterviewPrepAiErrorCodes.Cancelled => "Cancelled",
        _ => code
    };

    public static string ArtifactSource(string? source) => source switch
    {
        InterviewPrepArtifactSources.Ai => "AI [ai]",
        InterviewPrepArtifactSources.DeterministicFallback => "Deterministic fallback [deterministicFallback]",
        _ => source ?? "null"
    };

    public static string SessionConfigLine(
        string? mode,
        string? persona,
        string? language,
        string? market,
        string? experienceType,
        string? interactionType) =>
        $"mode={Mode(mode)} persona={Persona(persona)} language={Language(language)} market={Market(market)} experience={ExperienceType(experienceType)} interaction={InteractionType(interactionType)}";

    public static string SessionConfigLine(InterviewPrepAiSessionConfig config) =>
        SessionConfigLine(config.Mode, config.Persona, config.Language, config.Market, config.ExperienceType, config.InteractionType);

    private static bool TryMode(string? wire, out InterviewPrepMode mode) =>
        InterviewPrepEnumNames.TryParseMode(wire, out mode);

    private static bool TryPersona(string? wire, out InterviewPrepPersona persona) =>
        InterviewPrepEnumNames.TryParsePersona(wire, out persona);

    private static string HumanizeEnumName(Enum value) =>
        string.Concat(value.ToString().Select((ch, index) =>
            index > 0 && char.IsUpper(ch) ? " " + ch : ch.ToString()));
}
