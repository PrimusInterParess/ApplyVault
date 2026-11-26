namespace ApplyVault.Api.Models.InterviewPrep;

public sealed record PreparationPackInput(
    Guid? ScrapeResultId = null,
    string? TargetRole = null,
    string? TargetLevel = null,
    string Language = "english",
    string? HiringMarket = null,
    int PreparationDays = 14,
    int MinutesPerDay = 60,
    IReadOnlyList<string>? InterviewFormats = null,
    string? KnownWeaknesses = null,
    string? AdditionalInformation = null,
    IReadOnlyList<Guid>? ProjectSummaryIds = null);

public sealed record PreparationDocument(
    string Id,
    string Title,
    string Purpose,
    string Markdown,
    IReadOnlyList<string> PrerequisiteIds,
    bool IsGenerated = true,
    int ContextRevision = 0,
    string? SourceFingerprint = null);

public sealed record PreparationTopic(
    string Name,
    string Relevance,
    string Evidence,
    string DocumentId);

public sealed record PreparationStudySession(
    int Day,
    int Minutes,
    string Activity,
    IReadOnlyList<string> DocumentIds);

public sealed record PreparationPackContent(
    string Title,
    string OverviewMarkdown,
    IReadOnlyList<string> Unknowns,
    IReadOnlyList<PreparationTopic> Topics,
    IReadOnlyList<PreparationDocument> Documents,
    IReadOnlyList<PreparationStudySession> StudyPlan);

public sealed record PreparationProgress(
    IReadOnlyList<string> ReadDocumentIds,
    IReadOnlyList<string> ExplainedDocumentIds,
    IReadOnlyList<string> DemonstratedDocumentIds,
    IReadOnlyList<string> AdaptedDocumentIds,
    IReadOnlyList<int> CompletedDays,
    string ReviewLog)
{
    public static PreparationProgress Empty => new([], [], [], [], [], "");
}

public sealed record PreparationProgressRequest(DateTimeOffset ExpectedUpdatedAt, PreparationProgress Progress);
public sealed record RegeneratePreparationPackRequest(DateTimeOffset ExpectedUpdatedAt, PreparationPackInput Input);

public sealed record PreparationPackDto(
    Guid Id,
    Guid? ScrapeResultId,
    string? JobTitle,
    string? CompanyName,
    PreparationPackInput Input,
    PreparationPackContent? Content,
    PreparationProgress Progress,
    bool NeedsRegeneration,
    IReadOnlyList<string> OutdatedReasons,
    bool IsDemo,
    DateTimeOffset GeneratedAt,
    DateTimeOffset UpdatedAt,
    PreparationDiscovery? Discovery = null);

public sealed record PreparationProjectOption(Guid Id, string Title, string Repository);
public sealed record PreparationContextDto(bool HasStructuredCv, IReadOnlyList<PreparationProjectOption> Projects);

public sealed record PreparationDiscoveryQuestion(
    string Id, string Kind, string Prompt, string WhyItMatters,
    string GuidanceMarkdown, string AnswerStarter, string HypotheticalExample);

public sealed record PreparationDocumentOutline(
    string Id, string Title, string Purpose, IReadOnlyList<string> PrerequisiteIds,
    IReadOnlyList<PreparationDiscoveryQuestion> Questions,
    int ContextRevision = 0);

public sealed record PreparationDiscoveryPlan(
    string Title, string OverviewMarkdown, IReadOnlyList<string> Unknowns,
    IReadOnlyList<PreparationTopic> Topics, IReadOnlyList<PreparationDocumentOutline> Documents,
    IReadOnlyList<PreparationStudySession> StudyPlan);

public sealed record PreparationDiscoveryAnswer(string DocumentId, string QuestionId, string Status, string Text);
public sealed record PreparationDiscovery(
    IReadOnlyList<PreparationDocumentOutline> Documents,
    IReadOnlyList<PreparationDiscoveryAnswer> Answers,
    int ContextRevision = 0);

public sealed record PreparationAnswerInput(string QuestionId, string Status, string? Text = null);
public sealed record PreparationAnswersRequest(DateTimeOffset ExpectedUpdatedAt, IReadOnlyList<PreparationAnswerInput> Answers);
public sealed record PreparationRevisionRequest(DateTimeOffset ExpectedUpdatedAt);
public sealed record PreparationHelpRequest(
    DateTimeOffset ExpectedUpdatedAt, string Request, string? DraftAnswer = null, string? PreviousGuidance = null);
public sealed record PreparationHelpResponse(string Markdown, bool IsDemo = false);
