using ApplyVault.Api.Data;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services.InterviewPrep.Domain;
using ApplyVault.Api.Services.InterviewPrep.FullLoop;
using ApplyVault.Api.Services.InterviewPrep.Planning;
using ApplyVault.Api.Services.InterviewPrep.Runtime;

namespace ApplyVault.Api.Tests.InterviewPrep;

public sealed class InterviewPrepM10FullLoopTests
{
    [Fact]
    public void Cross_stage_loop_guard_blocks_duplicate_without_revisit_approval()
    {
        var guard = new InterviewLoopGuard();
        var options = new InterviewPrepLoopGuardOptions { NearDuplicateThreshold = 0.85 };
        var text = "Tell me about a time you improved onboarding retention.";
        var signature = guard.BuildSignature(text);
        var history = new List<InterviewLoopGuardHistoryItem>
        {
            new(signature, text, "motivation", "intent-1", "evidence:motivation")
        };

        var decision = guard.Evaluate(text, history, options);
        Assert.False(decision.Accepted);
        Assert.True(decision.IsExactDuplicate);
    }

    [Fact]
    public void Loop_guard_revisit_approval_persists_on_session_runtime()
    {
        var session = new InterviewPrepSessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Mode = InterviewPrepEnumNames.ToWire(InterviewPrepMode.FullLoop),
            Persona = InterviewPrepEnumNames.ToWire(InterviewPrepPersona.HiringManager),
            Language = InterviewPrepEnumNames.ToWire(InterviewPrepLanguage.English),
            Market = InterviewPrepEnumNames.ToWire(InterviewPrepMarket.General),
            ExperienceType = InterviewPrepEnumNames.ToWire(InterviewPrepExperienceType.RealisticSimulation),
            InteractionType = InterviewPrepEnumNames.ToWire(InterviewPrepInteractionType.Text),
            Status = InterviewPrepEnumNames.ToWire(InterviewPrepSessionStatus.InProgress),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var guard = new InterviewLoopGuard();
        var signature = guard.BuildSignature("Why this company?");
        var runtime = InterviewPrepCombinedRuntimeState.Read(session.RuntimeStateJson);
        runtime.ApprovedRevisits =
        [
            new InterviewPrepLoopGuardRevisitApproval(signature, "Candidate requested intentional revisit.")
        ];
        InterviewPrepCombinedRuntimeState.Write(session, runtime);

        var persisted = InterviewPrepCombinedRuntimeState.Read(session.RuntimeStateJson);
        Assert.Single(persisted.ApprovedRevisits!);
        Assert.Equal(signature, persisted.ApprovedRevisits![0].QuestionSignature, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Full_loop_catalog_defines_six_operational_stages()
    {
        var slots = InterviewPrepFullLoopCatalog.StandardStageSlots();
        Assert.Equal(6, slots.Count);
        Assert.Contains(slots, (slot) => slot.Mode == InterviewPrepMode.ProblemSolvingCase);
        Assert.Contains(slots, (slot) => slot.Persona == InterviewPrepPersona.BarRaiser);
    }

    [Fact]
    public void Panel_debrief_artifact_includes_evidence_not_score_average()
    {
        var artifact = new InterviewPrepPanelDebriefArtifact(
            "Evidence-backed panel summary.",
            [
                new InterviewPrepPanelPerspectiveDto("Recruiter", "Clear motivation story.", 72),
                new InterviewPrepPanelPerspectiveDto("Bar raiser", "Leveling signal mixed.", 58)
            ],
            ["ownership: shipped onboarding fix"],
            ["motivation strong vs impact thin"],
            [new InterviewPrepPanelMissingEvidenceDto("roleDepth", "Not enough depth")],
            "medium",
            InterviewPrepArtifactSources.Ai,
            false,
            DateTimeOffset.UtcNow);

        Assert.NotEmpty(artifact.EvidenceHighlights);
        Assert.NotEmpty(artifact.Contradictions);
        Assert.Contains("ownership", artifact.EvidenceHighlights[0], StringComparison.OrdinalIgnoreCase);
    }
}
