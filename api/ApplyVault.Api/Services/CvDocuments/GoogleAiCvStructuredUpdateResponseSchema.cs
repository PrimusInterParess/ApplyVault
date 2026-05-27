namespace ApplyVault.Api.Services;

internal static class GoogleAiCvStructuredUpdateResponseSchema
{
    public static object Create() =>
        new
        {
            responseMimeType = "application/json",
            responseSchema = new
            {
                type = "OBJECT",
                required = new[] { "sections" },
                properties = new
                {
                    sections = new
                    {
                        type = "ARRAY",
                        items = new
                        {
                            type = "OBJECT",
                            required = new[] { "heading", "sectionType", "sortOrder", "entries" },
                            properties = new
                            {
                                id = new { type = "STRING" },
                                heading = new { type = "STRING" },
                                sectionType = new { type = "STRING" },
                                sortOrder = new { type = "INTEGER" },
                                entries = new
                                {
                                    type = "ARRAY",
                                    items = new
                                    {
                                        type = "OBJECT",
                                        required = new[]
                                        {
                                            "title",
                                            "summary",
                                            "bullets",
                                            "techStack",
                                            "source",
                                            "sortOrder"
                                        },
                                        properties = new
                                        {
                                            id = new { type = "STRING" },
                                            title = new { type = "STRING" },
                                            subtitle = new { type = "STRING" },
                                            dateRange = new { type = "STRING" },
                                            summary = new { type = "STRING" },
                                            bullets = new
                                            {
                                                type = "ARRAY",
                                                items = new { type = "STRING" }
                                            },
                                            techStack = new { type = "STRING" },
                                            source = new { type = "STRING" },
                                            sourceSummaryId = new { type = "STRING" },
                                            sortOrder = new { type = "INTEGER" }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
}
