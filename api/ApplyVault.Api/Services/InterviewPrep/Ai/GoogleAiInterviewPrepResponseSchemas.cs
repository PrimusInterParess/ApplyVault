namespace ApplyVault.Api.Services.InterviewPrep.Ai;

internal static class GoogleAiInterviewPrepResponseSchemas
{
    public static object For(InterviewPrepAiOperation operation) =>
        operation switch
        {
            InterviewPrepAiOperation.AssessAnswer => AssessAnswerSchema(),
            InterviewPrepAiOperation.GenerateInterviewerMessage => GenerateInterviewerMessageSchema(),
            _ => throw new InvalidOperationException($"No response schema for {operation}.")
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
