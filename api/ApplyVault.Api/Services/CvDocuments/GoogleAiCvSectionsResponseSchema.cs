namespace ApplyVault.Api.Services;

internal static class GoogleAiCvSectionsResponseSchema
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
                            required = new[] { "heading", "sectionType", "entries" },
                            properties = new
                            {
                                heading = new { type = "STRING" },
                                sectionType = new { type = "STRING" },
                                entries = new
                                {
                                    type = "ARRAY",
                                    items = new
                                    {
                                        type = "OBJECT",
                                        required = new[] { "title", "summary", "bullets", "techStack" },
                                        properties = new
                                        {
                                            title = new { type = "STRING" },
                                            subtitle = new { type = "STRING" },
                                            dateRange = new { type = "STRING" },
                                            summary = new { type = "STRING" },
                                            bullets = new
                                            {
                                                type = "ARRAY",
                                                items = new { type = "STRING" }
                                            },
                                            techStack = new { type = "STRING" }
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
