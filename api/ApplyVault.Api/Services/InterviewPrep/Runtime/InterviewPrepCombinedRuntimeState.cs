using System.Text.Json;
using System.Text.Json.Serialization;
using ApplyVault.Api.Data;

using ApplyVault.Api.Services.InterviewPrep.FullLoop;

namespace ApplyVault.Api.Services.InterviewPrep.Runtime;

public sealed class InterviewPrepCombinedRuntimeState
{
    public int MainQuestionCount { get; set; }

    public int ConsecutiveNoProgress { get; set; }

    public int FollowUpsForCurrentIntent { get; set; }

    public string? CurrentIntentId { get; set; }

    public string? CurrentCompetencyId { get; set; }

    public InterviewPrepCaseRuntimeState? Case { get; set; }

    public IReadOnlyList<InterviewPrepLoopGuardRevisitApproval>? ApprovedRevisits { get; set; }

    public IReadOnlyList<InterviewPrepStageHandoffArtifact>? StageHandoffs { get; set; }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static InterviewPrepCombinedRuntimeState Read(string? runtimeStateJson)
    {
        if (string.IsNullOrWhiteSpace(runtimeStateJson))
        {
            return new InterviewPrepCombinedRuntimeState();
        }

        return JsonSerializer.Deserialize<InterviewPrepCombinedRuntimeState>(runtimeStateJson, SerializerOptions)
            ?? new InterviewPrepCombinedRuntimeState();
    }

    public static void Write(InterviewPrepSessionEntity session, InterviewPrepCombinedRuntimeState state) =>
        session.RuntimeStateJson = JsonSerializer.Serialize(state, SerializerOptions);

    public static void WriteCase(InterviewPrepSessionEntity session, InterviewPrepCaseRuntimeState caseState)
    {
        var combined = Read(session.RuntimeStateJson);
        combined.Case = caseState;
        Write(session, combined);
    }
}
