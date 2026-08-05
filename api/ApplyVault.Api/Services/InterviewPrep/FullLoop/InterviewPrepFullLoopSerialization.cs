using System.Text.Json;
using System.Text.Json.Serialization;
using ApplyVault.Api.Services.InterviewPrep.Planning;

namespace ApplyVault.Api.Services.InterviewPrep.FullLoop;

internal static class InterviewPrepFullLoopSerialization
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string SerializeOrchestration(InterviewPrepFullLoopOrchestration orchestration) =>
        JsonSerializer.Serialize(orchestration, Options);

    public static InterviewPrepFullLoopOrchestration? DeserializeOrchestration(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<InterviewPrepFullLoopOrchestration>(json, Options);

    public static string SerializeStageBundle(InterviewPrepStagePlanBundle bundle) =>
        JsonSerializer.Serialize(bundle, Options);

    public static InterviewPrepStagePlanBundle? DeserializeStageBundle(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<InterviewPrepStagePlanBundle>(json, Options);

    public static InterviewPlan? DeserializeLegacyStageQuestions(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var bundle = DeserializeStageBundle(json);
            if (bundle is not null)
            {
                return bundle.InterviewPlan;
            }
        }
        catch (JsonException)
        {
            // Legacy fixed-bank array — not an adaptive plan.
        }

        return null;
    }

    public static string SerializePanelDebrief(InterviewPrepPanelDebriefArtifact artifact) =>
        JsonSerializer.Serialize(artifact, Options);

    public static InterviewPrepPanelDebriefArtifact? DeserializePanelDebrief(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<InterviewPrepPanelDebriefArtifact>(json, Options);

    public static string SerializeStageAssessments(IReadOnlyList<InterviewPrepStageAssessmentEntry> entries) =>
        JsonSerializer.Serialize(entries, Options);

    public static IReadOnlyList<InterviewPrepStageAssessmentEntry> DeserializeStageAssessments(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<InterviewPrepStageAssessmentEntry>>(json, Options) ?? [];
    }
}
