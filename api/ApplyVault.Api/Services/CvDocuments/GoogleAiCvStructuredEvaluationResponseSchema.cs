namespace ApplyVault.Api.Services;

internal static class GoogleAiCvStructuredEvaluationResponseSchema
{
    public static object Create() =>
        new
        {
            responseMimeType = "application/json",
            responseSchema = new
            {
                type = "OBJECT",
                required = new[]
                {
                    "overallScore",
                    "summary",
                    "dimensions",
                    "findings",
                    "selfCheckQuestions"
                },
                properties = new
                {
                    overallScore = new { type = "INTEGER" },
                    summary = new { type = "STRING" },
                    dimensions = new
                    {
                        type = "ARRAY",
                        items = new
                        {
                            type = "OBJECT",
                            required = new[] { "id", "score", "summary" },
                            properties = new
                            {
                                id = new { type = "STRING" },
                                score = new { type = "INTEGER" },
                                summary = new { type = "STRING" }
                            }
                        }
                    },
                    findings = new
                    {
                        type = "ARRAY",
                        items = new
                        {
                            type = "OBJECT",
                            required = new[]
                            {
                                "dimension",
                                "severity",
                                "title",
                                "detail"
                            },
                            properties = new
                            {
                                id = new { type = "STRING" },
                                dimension = new { type = "STRING" },
                                severity = new { type = "STRING" },
                                title = new { type = "STRING" },
                                detail = new { type = "STRING" },
                                sectionId = new { type = "STRING" },
                                entryId = new { type = "STRING" }
                            }
                        }
                    },
                    selfCheckQuestions = new
                    {
                        type = "ARRAY",
                        items = new { type = "STRING" }
                    }
                }
            }
        };
}
