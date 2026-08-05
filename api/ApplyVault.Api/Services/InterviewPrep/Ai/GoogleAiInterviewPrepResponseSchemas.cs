namespace ApplyVault.Api.Services.InterviewPrep.Ai;

internal static class GoogleAiInterviewPrepResponseSchemas
{
    public static object For(InterviewPrepAiOperation operation) =>
        operation switch
        {
            InterviewPrepAiOperation.CreateInterviewBrief => CreateInterviewBriefSchema(),
            InterviewPrepAiOperation.PlanInterview => PlanInterviewSchema(),
            InterviewPrepAiOperation.GenerateOpening => GenerateOpeningSchema(),
            InterviewPrepAiOperation.AssessAnswer => AssessAnswerSchema(),
            InterviewPrepAiOperation.SelectNextAction => SelectNextActionSchema(),
            InterviewPrepAiOperation.GenerateInterviewerMessage => GenerateInterviewerMessageSchema(),
            InterviewPrepAiOperation.PlanFullLoop => PlanFullLoopSchema(),
            _ => throw new InvalidOperationException($"No response schema for {operation}.")
        };

    private static object CreateInterviewBriefSchema() =>
        new
        {
            responseMimeType = "application/json",
            responseSchema = new
            {
                type = "OBJECT",
                required = new[] { "summary", "themes", "risks", "talkingPoints" },
                properties = new
                {
                    summary = new { type = "STRING" },
                    themes = new { type = "ARRAY", items = new { type = "STRING" } },
                    risks = new { type = "ARRAY", items = new { type = "STRING" } },
                    talkingPoints = new { type = "ARRAY", items = new { type = "STRING" } }
                }
            }
        };

    private static object PlanInterviewSchema() =>
        new
        {
            responseMimeType = "application/json",
            responseSchema = new
            {
                type = "OBJECT",
                required = new[] { "planSummary", "stages" },
                properties = new
                {
                    planSummary = new { type = "STRING" },
                    stages = new { type = "ARRAY", items = PlannedStageItemSchema() }
                }
            }
        };

    private static object GenerateOpeningSchema() =>
        new
        {
            responseMimeType = "application/json",
            responseSchema = new
            {
                type = "OBJECT",
                required = new[] { "messageText", "tone" },
                properties = new
                {
                    messageText = new { type = "STRING" },
                    tone = new { type = "STRING" }
                }
            }
        };

    private static object SelectNextActionSchema() =>
        new
        {
            responseMimeType = "application/json",
            responseSchema = new
            {
                type = "OBJECT",
                required = new[] { "actionType", "rationale" },
                properties = new
                {
                    actionType = new { type = "STRING" },
                    competencyId = new { type = "STRING", nullable = true },
                    rationale = new { type = "STRING" }
                }
            }
        };

    private static object PlanFullLoopSchema() =>
        new
        {
            responseMimeType = "application/json",
            responseSchema = new
            {
                type = "OBJECT",
                required = new[] { "loopSummary", "stages", "coverageGoals" },
                properties = new
                {
                    loopSummary = new { type = "STRING" },
                    stages = new { type = "ARRAY", items = PlannedStageItemSchema() },
                    coverageGoals = new { type = "ARRAY", items = new { type = "STRING" } }
                }
            }
        };

    private static object PlannedStageItemSchema() =>
        new
        {
            type = "OBJECT",
            required = new[] { "stageKey", "goal", "competencyIds" },
            properties = new
            {
                stageKey = new { type = "STRING" },
                goal = new { type = "STRING" },
                competencyIds = new { type = "ARRAY", items = new { type = "STRING" } }
            }
        };

    private static object AssessAnswerSchema() =>
        new
        {
            responseMimeType = "application/json",
            responseSchema = new
            {
                type = "OBJECT",
                required = new[] { "score", "summary", "strengths", "gaps", "evidence", "confidence" },
                properties = new
                {
                    score = new { type = "INTEGER" },
                    summary = new { type = "STRING" },
                    strengths = new { type = "ARRAY", items = new { type = "STRING" } },
                    gaps = new { type = "ARRAY", items = new { type = "STRING" } },
                    evidence = new
                    {
                        type = "ARRAY",
                        items = new
                        {
                            type = "OBJECT",
                            required = new[] { "claim", "evidenceQuote", "polarity" },
                            properties = new
                            {
                                claim = new { type = "STRING" },
                                evidenceQuote = new { type = "STRING" },
                                polarity = new { type = "STRING" }
                            }
                        }
                    },
                    confidence = new { type = "NUMBER" }
                }
            }
        };

    private static object GenerateInterviewerMessageSchema() =>
        new
        {
            responseMimeType = "application/json",
            responseSchema = new
            {
                type = "OBJECT",
                required = new[] { "messageText", "intent" },
                properties = new
                {
                    messageText = new { type = "STRING" },
                    intent = new { type = "STRING" },
                    competencyId = new { type = "STRING" }
                }
            }
        };
}
