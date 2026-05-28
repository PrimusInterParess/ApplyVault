namespace ApplyVault.Api.Services;

internal static class GoogleAiCvStructuredSuggestionsResponseSchema
{
    public static object Create() =>
        new
        {
            responseMimeType = "application/json",
            responseSchema = new
            {
                type = "OBJECT",
                required = new[] { "suggestions" },
                properties = new
                {
                    suggestions = new
                    {
                        type = "ARRAY",
                        items = new
                        {
                            type = "OBJECT",
                            required = new[]
                            {
                                "title",
                                "rationale",
                                "suggestedInstruction",
                                "category",
                                "impact"
                            },
                            properties = new
                            {
                                id = new { type = "STRING" },
                                title = new { type = "STRING" },
                                rationale = new { type = "STRING" },
                                suggestedInstruction = new { type = "STRING" },
                                sectionId = new { type = "STRING" },
                                entryId = new { type = "STRING" },
                                category = new { type = "STRING" },
                                impact = new { type = "STRING" }
                            }
                        }
                    }
                }
            }
        };
}
