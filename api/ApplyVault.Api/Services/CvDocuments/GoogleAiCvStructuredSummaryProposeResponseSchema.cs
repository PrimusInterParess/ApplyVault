namespace ApplyVault.Api.Services;

internal static class GoogleAiCvStructuredSummaryProposeResponseSchema
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
                    "proposedSummaryText",
                    "changeBullets"
                },
                properties = new
                {
                    proposedSummaryText = new { type = "STRING" },
                    changeBullets = new
                    {
                        type = "ARRAY",
                        items = new { type = "STRING" }
                    }
                }
            }
        };
}
