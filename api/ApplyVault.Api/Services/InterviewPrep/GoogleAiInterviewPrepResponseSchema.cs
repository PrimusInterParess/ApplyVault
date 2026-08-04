namespace ApplyVault.Api.Services;

internal static class GoogleAiInterviewPrepResponseSchema
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
                    "phase",
                    "inference",
                    "coachMessage",
                    "followUps",
                    "debriefBullets",
                    "turnState"
                },
                properties = new
                {
                    phase = new { type = "STRING" },
                    inference = new
                    {
                        type = "OBJECT",
                        required = new[]
                        {
                            "role",
                            "seniority",
                            "interviewStyle",
                            "isTechnicalContext"
                        },
                        properties = new
                        {
                            role = new { type = "STRING" },
                            seniority = new { type = "STRING" },
                            interviewStyle = new { type = "STRING" },
                            isTechnicalContext = new { type = "BOOLEAN" }
                        }
                    },
                    coachMessage = new { type = "STRING" },
                    scorecard = new
                    {
                        type = "OBJECT",
                        nullable = true,
                        required = new[] { "overall", "dimensions" },
                        properties = new
                        {
                            overall = new { type = "INTEGER" },
                            summary = new { type = "STRING" },
                            dimensions = new
                            {
                                type = "ARRAY",
                                items = new
                                {
                                    type = "OBJECT",
                                    required = new[] { "id", "score", "note" },
                                    properties = new
                                    {
                                        id = new { type = "STRING" },
                                        score = new { type = "INTEGER" },
                                        note = new { type = "STRING" }
                                    }
                                }
                            }
                        }
                    },
                    followUps = new
                    {
                        type = "ARRAY",
                        items = new { type = "STRING" }
                    },
                    debriefBullets = new
                    {
                        type = "ARRAY",
                        items = new { type = "STRING" }
                    },
                    modelAnswer = new
                    {
                        type = "STRING",
                        nullable = true
                    },
                    turnState = new
                    {
                        type = "OBJECT",
                        required = new[]
                        {
                            "interviewMove",
                            "questionType",
                            "pressureLevel",
                            "interviewerIntent",
                            "agendaStep",
                            "listeningNotes"
                        },
                        properties = new
                        {
                            interviewMove = new { type = "STRING" },
                            questionType = new { type = "STRING" },
                            pressureLevel = new { type = "STRING" },
                            interviewerIntent = new { type = "STRING" },
                            agendaStep = new { type = "STRING" },
                            nextAgendaStep = new
                            {
                                type = "STRING",
                                nullable = true
                            },
                            memorySummary = new
                            {
                                type = "STRING",
                                nullable = true
                            },
                            listeningNotes = new
                            {
                                type = "ARRAY",
                                items = new { type = "STRING" }
                            }
                        }
                    }
                }
            }
        };
}
