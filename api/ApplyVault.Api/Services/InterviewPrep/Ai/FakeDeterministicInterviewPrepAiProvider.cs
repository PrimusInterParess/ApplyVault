using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApplyVault.Api.Services.InterviewPrep.Ai.Contracts;
using ApplyVault.Api.Services.InterviewPrep.Ai.Prompts;
using ApplyVault.Api.Services.InterviewPrep.Catalogs;
using ApplyVault.Api.Services.InterviewPrep.Domain;

namespace ApplyVault.Api.Services.InterviewPrep.Ai;

/// <summary>
/// Deterministic provider for unit tests and local AI-off paths. Implements all named operations.
/// </summary>
public sealed class FakeDeterministicInterviewPrepAiProvider : IInterviewPrepAiProvider
{
    public const string Name = "fake-deterministic";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// When set, the next CompleteAsync for that operation returns invalid JSON once (then clears).
    /// Used by unit tests to exercise gateway retries.
    /// </summary>
    public InterviewPrepAiOperation? ForceInvalidJsonOnceFor { get; set; }

    public string ProviderName => Name;

    public Task<InterviewPrepAiProviderResult> CompleteAsync(
        InterviewPrepPromptDefinition prompt,
        object request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ForceInvalidJsonOnceFor == prompt.Operation)
        {
            ForceInvalidJsonOnceFor = null;
            return Task.FromResult(new InterviewPrepAiProviderResult(
                true,
                """{"notValidForAnyOperation":true}""",
                null,
                null));
        }

        var json = prompt.Operation switch
        {
            InterviewPrepAiOperation.CreateInterviewBrief => Serialize(BuildBrief(As<CreateInterviewBriefRequest>(request))),
            InterviewPrepAiOperation.PlanInterview => Serialize(BuildPlan(As<PlanInterviewRequest>(request))),
            InterviewPrepAiOperation.GenerateOpening => Serialize(BuildOpening(As<GenerateOpeningRequest>(request))),
            InterviewPrepAiOperation.AssessAnswer => Serialize(BuildAssess(As<AssessAnswerRequest>(request))),
            InterviewPrepAiOperation.SelectNextAction => Serialize(BuildSelect(As<SelectNextActionRequest>(request))),
            InterviewPrepAiOperation.GenerateInterviewerMessage => Serialize(BuildMessage(As<GenerateInterviewerMessageRequest>(request))),
            InterviewPrepAiOperation.SummarizeConversation => Serialize(BuildSummary(As<SummarizeConversationRequest>(request))),
            InterviewPrepAiOperation.EvaluateStage => Serialize(BuildStage(As<EvaluateStageRequest>(request))),
            InterviewPrepAiOperation.GenerateFeedback => Serialize(BuildFeedback(As<GenerateFeedbackRequest>(request))),
            InterviewPrepAiOperation.CompareAnswerRetry => Serialize(BuildCompare(As<CompareAnswerRetryRequest>(request))),
            InterviewPrepAiOperation.ExtractProfessionalStory => Serialize(BuildStories(As<ExtractProfessionalStoryRequest>(request))),
            InterviewPrepAiOperation.PlanFullLoop => Serialize(BuildFullLoop(As<PlanFullLoopRequest>(request))),
            InterviewPrepAiOperation.GeneratePanelDebrief => Serialize(BuildDebrief(As<GeneratePanelDebriefRequest>(request))),
            InterviewPrepAiOperation.GenerateInterviewPrepStudyBrief =>
                Serialize(BuildStudyBrief(As<GenerateInterviewPrepStudyBriefRequest>(request))),
            _ => throw new InterviewPrepAiException(
                InterviewPrepAiErrorCodes.OperationNotImplemented,
                $"Fake provider has no handler for {prompt.Operation}.")
        };

        return Task.FromResult(new InterviewPrepAiProviderResult(true, json, null, null));
    }

    public static AssessAnswerResponse SafeAssessFallback(AssessAnswerRequest request) =>
        new(
            Score: 50,
            Summary: "Unable to complete model assessment; returning a neutral provisional score.",
            Strengths: [],
            Gaps: ["Model assessment unavailable."],
            Evidence:
            [
                new AssessAnswerEvidenceItem(
                    "Provisional placeholder",
                    Truncate(request.AnswerText, 120),
                    "neutral")
            ],
            Confidence: 0.1);

    public static GenerateInterviewerMessageResponse SafeMessageFallback(GenerateInterviewerMessageRequest request)
    {
        var topic = NormalizeTopicHint(request.TopicHint, request.CompetencyId);
        return new GenerateInterviewerMessageResponse(
            MessageText: string.IsNullOrWhiteSpace(topic)
                ? "Could you tell me more about a recent example that demonstrates that skill?"
                : $"Could you share a recent example related to {topic}?",
            Intent: "question",
            CompetencyId: request.CompetencyId);
    }

    public static EvaluateStageResponse SafeStageFallback(EvaluateStageRequest request) =>
        new(
            Score: 50,
            Summary: string.IsNullOrWhiteSpace(request.StageGoal)
                ? "Stage review is provisional because automated evaluation was unavailable."
                : $"Provisional review for stage goal: {Truncate(request.StageGoal, 120)}.",
            AchievedGoals: [],
            MissedGoals: ["Automated stage evaluation unavailable."]);

    public static GenerateFeedbackResponse SafeFeedbackFallback(GenerateFeedbackRequest request) =>
        new(
            OverallFeedback: string.IsNullOrWhiteSpace(request.ConversationSummary)
                ? "Practice feedback is limited because the session summary was unavailable."
                : Truncate(request.ConversationSummary, 240),
            CoachingTips: request.Gaps.Take(3).Select((gap) => $"Consider strengthening: {gap}").ToArray(),
            PracticeSuggestions:
            [
                "Rehearse one answer with a clear situation, action, and measurable result.",
                "Note where evidence was missing and prepare one concrete example."
            ]);

    private static CreateInterviewBriefResponse BuildBrief(CreateInterviewBriefRequest request)
    {
        var role = request.JobSnapshot?.Title?.Trim();
        var hasCv = !string.IsNullOrWhiteSpace(request.CvSnapshot?.Text);
        var hasJob = request.JobSnapshot is not null;
        var mode = request.Config.Mode;
        var persona = request.Config.Persona;
        var modeLabel = ResolveModeDisplayName(mode);
        var personaLabel = ResolvePersonaDisplayName(persona);
        var summary = string.IsNullOrWhiteSpace(role)
            ? $"Practice session: {modeLabel} with a {personaLabel.ToLowerInvariant()} interviewer."
            : $"Practice for {role}: {modeLabel} with a {personaLabel.ToLowerInvariant()} interviewer.";

        var themes = ResolveBriefThemes(mode, persona, hasCv, hasJob);
        if (InterviewPrepEnumNames.TryParsePersona(persona, out var parsedPersona)
            && parsedPersona == InterviewPrepPersona.BarRaiser)
        {
            themes = themes
                .Concat(["communication", "ownership"])
                .Distinct(StringComparer.Ordinal)
                .Take(5)
                .ToArray();
        }

        return new CreateInterviewBriefResponse(
            summary,
            Themes: themes,
            Risks: hasCv
                ? persona.Contains("barRaiser", StringComparison.OrdinalIgnoreCase)
                    ? ["explore narrative consistency calmly", "verify depth on claimed impact"]
                    : ["verify depth on claimed impact"]
                : ["limited CV context"],
            TalkingPoints:
            [
                "Have a clear story from your most recent role (situation, your actions, outcome).",
                "Be ready to explain why this opportunity fits your goals."
            ]);
    }

    private static PlanInterviewResponse BuildPlan(PlanInterviewRequest request)
    {
        var mode = request.Config.Mode;
        var persona = request.Config.Persona;
        var styleHint = ResolvePersonaStyleHint(persona);
        var themes = request.Brief.Themes.Count > 0
            ? request.Brief.Themes.Take(4).ToArray()
            : ResolveBriefThemes(mode, persona, hasCv: true, hasJob: false);

        return new PlanInterviewResponse(
            PlanSummary: $"Plan ({mode}, personaStyle={styleHint}): {Truncate(request.Brief.Summary, 80)}",
            Stages:
            [
                new InterviewPrepAiPlannedStage("opening", "Rapport and expectations", []),
                new InterviewPrepAiPlannedStage("core", $"Core probe ({styleHint})", themes),
                new InterviewPrepAiPlannedStage("close", "Candidate questions and wrap-up", [])
            ]);
    }

    private static GenerateOpeningResponse BuildOpening(GenerateOpeningRequest request)
    {
        var name = string.IsNullOrWhiteSpace(request.CandidateDisplayName) ? "there" : request.CandidateDisplayName.Trim();
        var role = string.IsNullOrWhiteSpace(request.RoleTitle) ? "this role" : request.RoleTitle.Trim();
        return new GenerateOpeningResponse(
            $"Hello {name}, thanks for joining. Today we'll talk about the {role} role. I'll start with a few questions shortly.",
            "professional");
    }

    private static AssessAnswerResponse BuildAssess(AssessAnswerRequest request)
    {
        var length = request.AnswerText?.Trim().Length ?? 0;
        var score = length switch
        {
            < 20 => 35,
            < 120 => 55,
            < 400 => 72,
            _ => 80
        };
        var quote = Truncate(request.AnswerText, 160);
        return new AssessAnswerResponse(
            score,
            Summary: length < 20
                ? "Answer is too brief to demonstrate depth."
                : "Answer provides usable evidence with room to quantify impact.",
            Strengths: length >= 120 ? ["concrete narrative"] : [],
            Gaps: length < 120 ? ["add specifics and outcomes"] : ["could strengthen measurable outcomes"],
            Evidence:
            [
                new AssessAnswerEvidenceItem(
                    length < 20 ? "Insufficient detail" : "Candidate provided narrative detail",
                    string.IsNullOrWhiteSpace(quote) ? "(empty answer)" : quote,
                    length < 20 ? "negative" : "positive")
            ],
            Confidence: length < 20 ? 0.4 : 0.75);
    }

    private static SelectNextActionResponse BuildSelect(SelectNextActionRequest request)
    {
        if (InterviewPrepEnumNames.TryParsePersona(request.Config.Persona, out var persona)
            && persona == InterviewPrepPersona.BarRaiser
            && request.RecentTurns.Any((turn) =>
                turn.Text.Contains("contradiction", StringComparison.OrdinalIgnoreCase)
                || turn.Text.Contains("conflict", StringComparison.OrdinalIgnoreCase)))
        {
            var competency = request.RemainingCompetencyIds.FirstOrDefault() ?? "communication";
            return new SelectNextActionResponse(
                "probe",
                competency,
                "BarRaiser: explore consistency without hostility.");
        }

        if (request.RemainingCompetencyIds.Count > 0)
        {
            var next = request.RemainingCompetencyIds[0];
            return new SelectNextActionResponse("ask_question", next, $"Cover remaining competency {next}.");
        }

        return new SelectNextActionResponse("wrap_up", null, "Coverage complete; move to wrap-up.");
    }

    private static GenerateInterviewerMessageResponse BuildMessage(GenerateInterviewerMessageRequest request)
    {
        var topicHint = NormalizeTopicHint(request.TopicHint, request.CompetencyId);
        var hasCandidateAnswer = request.RecentTurns.Any((turn) =>
            turn.Role.Contains("candidate", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(turn.Text));
        var blocked = request.BlockedQuestionSignatures ?? [];
        string[] candidates = hasCandidateAnswer
            ?
            [
                "What was your specific contribution in that situation?",
                "How did you measure success for that work?",
                "What would you do differently next time?",
                string.IsNullOrWhiteSpace(topicHint)
                    ? "Can you add a bit more detail to that example?"
                    : $"Can you add detail related to {topicHint}?"
            ]
            :
            [
                string.IsNullOrWhiteSpace(topicHint)
                    ? "Can you walk me through a recent example from your work?"
                    : $"Can you walk me through a recent example related to {topicHint}?",
                "Tell me about a time you had to collaborate with others to deliver something important.",
                "What drew you to this kind of role, and what are you looking for next?"
            ];

        var message = candidates[0];
        foreach (var candidate in candidates)
        {
            if (!blocked.Any(b => string.Equals(b, StableSignature(candidate), StringComparison.OrdinalIgnoreCase)))
            {
                message = candidate;
                break;
            }
        }

        message = ApplyPersonaMessageFraming(request.Config.Persona, message);
        message = ApplyLanguageFraming(request.Config, message);

        return new GenerateInterviewerMessageResponse(
            message,
            request.ActionType.Contains("probe", StringComparison.OrdinalIgnoreCase) ? "probe" : "question",
            request.CompetencyId);
    }

    private static SummarizeConversationResponse BuildSummary(SummarizeConversationRequest request)
    {
        var count = request.Turns?.Count ?? 0;
        return new SummarizeConversationResponse(
            $"Conversation with {count} turn(s).",
            request.Turns?.Take(3).Select(t => Truncate($"{t.Role}: {t.Text}", 80)).ToArray() ?? []);
    }

    private static EvaluateStageResponse BuildStage(EvaluateStageRequest request)
    {
        var count = request.StageTurns?.Count ?? 0;
        return new EvaluateStageResponse(
            count == 0 ? 40 : 70,
            count == 0 ? "No stage turns to evaluate." : $"Stage '{request.StageKey}' reviewed against goal.",
            count == 0 ? [] : ["exchanged substantive turns"],
            count == 0 ? ["no evidence collected"] : []);
    }

    private static GenerateFeedbackResponse BuildFeedback(GenerateFeedbackRequest request)
    {
        var supplement = InterviewPrepEnumNames.TryParseLanguage(request.Config.Language, out var language)
            && InterviewPrepEnumNames.TryParseMarket(request.Config.Market, out var market)
            ? InterviewPrepLanguageMarketCatalog.PromptSupplement(language, market)
            : string.Empty;

        var tips = request.Gaps.Take(3).Select(g => $"Practice: {g}").ToList();
        if (!string.IsNullOrWhiteSpace(supplement))
        {
            tips.Add("Language feedback is separate from role competence scores.");
        }

        return new GenerateFeedbackResponse(
            OverallFeedback: string.IsNullOrWhiteSpace(request.ConversationSummary)
                ? "Keep answers structured: situation, action, result."
                : $"Based on the session: {Truncate(request.ConversationSummary, 120)}",
            CoachingTips: tips.ToArray(),
            PracticeSuggestions: ["Rehearse one STAR story out loud.", "Add one measurable outcome per story."]);
    }

    private static CompareAnswerRetryResponse BuildCompare(CompareAnswerRetryRequest request)
    {
        var originalLen = request.OriginalAnswerText?.Trim().Length ?? 0;
        var retryLen = request.RetryAnswerText?.Trim().Length ?? 0;
        var improved = retryLen > originalLen + 20;
        return new CompareAnswerRetryResponse(
            improved
                ? "Retry adds more detail than the original answer."
                : "Retry does not clearly improve on the original answer.",
            improved,
            improved ? ["greater detail"] : [],
            improved ? [] : ["still needs concrete outcomes"]);
    }

    private static ExtractProfessionalStoryResponse BuildStories(ExtractProfessionalStoryRequest request)
    {
        var text = request.CvSnapshot?.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return new ExtractProfessionalStoryResponse([]);
        }

        return new ExtractProfessionalStoryResponse(
        [
            new ProfessionalStoryItem(
                Title: string.IsNullOrWhiteSpace(request.CvSnapshot?.Title) ? "Primary experience" : request.CvSnapshot!.Title!,
                Narrative: Truncate(text, 280),
                CompetencyTags: ["ownership"],
                EvidenceQuote: Truncate(text, 120))
        ]);
    }

    private static PlanFullLoopResponse BuildFullLoop(PlanFullLoopRequest request) =>
        new(
            LoopSummary: $"Full loop for {request.Config.Mode}.",
            Stages:
            [
                new InterviewPrepAiPlannedStage("opening", "Open", []),
                new InterviewPrepAiPlannedStage("warmup", "Warm-up", []),
                new InterviewPrepAiPlannedStage("core", "Core assessment", request.Brief.Themes.Take(3).ToArray()),
                new InterviewPrepAiPlannedStage("candidate_questions", "Candidate questions", []),
                new InterviewPrepAiPlannedStage("closing", "Close", [])
            ],
            CoverageGoals: request.Brief.Themes.ToArray());

    private static GeneratePanelDebriefResponse BuildDebrief(GeneratePanelDebriefRequest request) =>
        new(
            OverallDebrief: string.IsNullOrWhiteSpace(request.ConversationSummary)
                ? "Panel debrief unavailable without conversation summary."
                : Truncate(request.ConversationSummary, 200),
            Perspectives:
            [
                new PanelPerspective("Recruiter", "Motivation and communication look workable.", 70),
                new PanelPerspective("HiringManager", "Need stronger evidence of impact.", 62)
            ]);

    /// <summary>
    /// Deterministic nested study brief for Dev / AI-off (ADR-0025). Profession-agnostic topics;
    /// each topic nests coverageItems (≥1), sampleQuestions, talkingPoints (sibling lists).
    /// </summary>
    private static GenerateInterviewPrepStudyBriefResponse BuildStudyBrief(GenerateInterviewPrepStudyBriefRequest request)
    {
        var hasCv = !string.IsNullOrWhiteSpace(request.CvSnapshot?.Text);
        var hasJob = request.JobSnapshot is not null;
        var role = request.JobSnapshot?.Title?.Trim();
        var focus = string.IsNullOrWhiteSpace(request.FocusNote) ? null : request.FocusNote.Trim();

        var topics = new List<InterviewPrepAiStudyBriefTopic>
        {
            new(
                Name: "Role motivation and fit",
                Gap: hasCv ? "alreadyStrong" : "unclear",
                Priority: 1,
                Note: hasJob && !string.IsNullOrWhiteSpace(role)
                    ? $"Ground answers in why {role} matches your recent work."
                    : "Prepare a clear motivation narrative from your CV.",
                CoverageItems: new List<InterviewPrepAiStudyBriefItem>
                {
                    new(
                        Text: "Map your recent responsibilities to the opportunity's stated purpose.",
                        Note: "Syllabus leaf — study the fit story; not a checklist."),
                    new(
                        Text: "Prepare one concise motivation statement without inventing employers or metrics.",
                        Note: null)
                },
                SampleQuestions: new List<InterviewPrepAiStudyBriefItem>
                {
                    new(
                        Text: hasJob && !string.IsNullOrWhiteSpace(role)
                            ? $"What in your background prepared you for the {role} responsibilities?"
                            : "Walk me through a recent responsibility you owned end to end.",
                        Note: "Keep the answer profession-agnostic: situation, your actions, outcome.")
                },
                TalkingPoints: new List<InterviewPrepAiStudyBriefItem>
                {
                    new(
                        Text: hasCv
                            ? "Lead with one concrete example from your most recent role (situation, actions, outcome)."
                            : "Add Structured CV detail so talking points can cite real evidence.",
                        Note: null)
                }),
            new(
                Name: "Evidence of ownership and outcomes",
                Gap: hasCv ? "mustStudy" : "unclear",
                Priority: 2,
                Note: "Rehearse one situation → action → result story with a measurable outcome.",
                CoverageItems: new List<InterviewPrepAiStudyBriefItem>
                {
                    new(
                        Text: "Choose one ownership story with a clear outcome you can defend from CV evidence.",
                        Note: null),
                    new(
                        Text: "Identify where feedback changed your approach and what improved.",
                        Note: "Study the adjustment, not a progress checkbox.")
                },
                SampleQuestions: new List<InterviewPrepAiStudyBriefItem>
                {
                    new(
                        Text: "Tell me about a time you had to adjust your approach after feedback.",
                        Note: null)
                },
                TalkingPoints: new List<InterviewPrepAiStudyBriefItem>
                {
                    new(
                        Text: "Separate motivation for the opportunity from claims about tools or tech stacks.",
                        Note: "Prefer domain craft and outcomes over software-default technology lists.")
                }),
            new(
                Name: "Domain methods and collaboration",
                Gap: hasJob ? "niceToHave" : "mustStudy",
                Priority: 3,
                Note: "Focus on how you work with others and apply methods in your field — not tool lists.",
                CoverageItems: new List<InterviewPrepAiStudyBriefItem>
                {
                    new(
                        Text: "Review how you collaborate across roles and apply methods in your domain.",
                        Note: null)
                },
                SampleQuestions: Array.Empty<InterviewPrepAiStudyBriefItem>(),
                TalkingPoints: Array.Empty<InterviewPrepAiStudyBriefItem>())
        };

        if (!string.IsNullOrWhiteSpace(focus))
        {
            topics.Add(new InterviewPrepAiStudyBriefTopic(
                Name: "Seeker focus area",
                Gap: "mustStudy",
                Priority: 4,
                Note: Truncate(focus, 160),
                CoverageItems: new List<InterviewPrepAiStudyBriefItem>
                {
                    new(Text: $"Study the seeker focus: {Truncate(focus, 120)}", Note: null)
                },
                SampleQuestions: Array.Empty<InterviewPrepAiStudyBriefItem>(),
                TalkingPoints: Array.Empty<InterviewPrepAiStudyBriefItem>()));
        }

        if (!hasCv)
        {
            var first = topics[0];
            topics[0] = first with
            {
                Gap = "unclear",
                Note = "Limited CV evidence; strengthen source material before relying on this brief."
            };
        }

        return new GenerateInterviewPrepStudyBriefResponse(topics);
    }

    private static T As<T>(object request)
        where T : class =>
        request as T
        ?? throw new InterviewPrepAiException(
            InterviewPrepAiErrorCodes.ValidationFailed,
            $"Fake provider expected {typeof(T).Name}.");

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, SerializerOptions);

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private static int StableHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return BitConverter.ToInt32(bytes, 0) & int.MaxValue;
    }

    private static string StableSignature(string text)
    {
        var normalized = string.Join(
            ' ',
            text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    private static string ResolvePersonaStyleHint(string personaWire)
    {
        if (InterviewPrepEnumNames.TryParsePersona(personaWire, out var persona))
        {
            return new InterviewPrepPersonaCatalog().Get(persona).QuestionStyleHint;
        }

        return "neutral";
    }

    private static string[] ResolveBriefThemes(string modeWire, string personaWire, bool hasCv, bool hasJob)
    {
        if (InterviewPrepEnumNames.TryParseMode(modeWire, out var mode)
            && InterviewPrepEnumNames.TryParsePersona(personaWire, out var persona))
        {
            var modeDef = new InterviewPrepModeCatalog().Get(mode);
            var personaDef = new InterviewPrepPersonaCatalog().Get(persona);
            var themes = modeDef.PriorityCompetencyIds
                .Concat(personaDef.EmphasisCompetencyIds)
                .Distinct(StringComparer.Ordinal)
                .Take(5)
                .ToList();
            if (hasJob && !themes.Contains(InterviewPrepCompetencyCatalog.RoleDepth, StringComparer.Ordinal))
            {
                themes.Insert(0, InterviewPrepCompetencyCatalog.RoleDepth);
            }

            return themes.ToArray();
        }

        return hasCv ? ["motivation", "ownership", "execution"] : ["motivation", "communication"];
    }

    private static string ApplyPersonaMessageFraming(string personaWire, string message)
    {
        if (!InterviewPrepEnumNames.TryParsePersona(personaWire, out var persona))
        {
            return message;
        }

        return persona switch
        {
            InterviewPrepPersona.Recruiter when message.Length > 80 =>
                Truncate(message, 80).TrimEnd('.') + "?",
            InterviewPrepPersona.BarRaiser =>
                message.EndsWith("?", StringComparison.Ordinal) ? message : $"{message}?",
            _ => message
        };
    }

    private static string? NormalizeTopicHint(string? topicHint, string? competencyId)
    {
        if (!string.IsNullOrWhiteSpace(topicHint))
        {
            var trimmed = topicHint.Trim();
            if (!LooksLikeInternalTopicHint(trimmed))
            {
                return trimmed;
            }
        }

        if (string.IsNullOrWhiteSpace(competencyId))
        {
            return null;
        }

        var catalog = new InterviewPrepCompetencyCatalog();
        return catalog.TryGet(competencyId, out var definition)
            ? definition.DisplayName
            : null;
    }

    private static bool LooksLikeInternalTopicHint(string topicHint) =>
        topicHint.Contains("competency", StringComparison.OrdinalIgnoreCase)
        || topicHint.Contains("Deterministic", StringComparison.OrdinalIgnoreCase)
        || topicHint.Contains("BarRaiser:", StringComparison.OrdinalIgnoreCase)
        || topicHint.Contains("Loop Guard", StringComparison.OrdinalIgnoreCase);

    private static string ResolveModeDisplayName(string modeWire)
    {
        if (InterviewPrepEnumNames.TryParseMode(modeWire, out var mode))
        {
            return new InterviewPrepModeCatalog().Get(mode).DisplayName;
        }

        return modeWire;
    }

    private static string ResolvePersonaDisplayName(string personaWire)
    {
        if (InterviewPrepEnumNames.TryParsePersona(personaWire, out var persona))
        {
            return new InterviewPrepPersonaCatalog().Get(persona).DisplayName;
        }

        return personaWire;
    }

    private static string ApplyLanguageFraming(InterviewPrepAiSessionConfig config, string message)
    {
        if (!InterviewPrepEnumNames.TryParseLanguage(config.Language, out var language))
        {
            return message;
        }

        var supplement = InterviewPrepLanguageMarketCatalog.PromptSupplement(
            language,
            InterviewPrepEnumNames.TryParseMarket(config.Market, out var market)
                ? market
                : InterviewPrepMarket.General);

        return language switch
        {
            InterviewPrepLanguage.Danish => $"[da] {message}",
            InterviewPrepLanguage.MixedEnglishDanish =>
                message.StartsWith("[da]", StringComparison.Ordinal) || message.StartsWith("[en]", StringComparison.Ordinal)
                    ? message
                    : $"[en] {message}",
            _ => string.IsNullOrWhiteSpace(supplement) ? message : message
        };
    }
}
