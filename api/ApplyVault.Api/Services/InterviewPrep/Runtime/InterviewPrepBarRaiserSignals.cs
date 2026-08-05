using ApplyVault.Api.Data;
using ApplyVault.Api.Services.InterviewPrep.Domain;

namespace ApplyVault.Api.Services.InterviewPrep.Runtime;

public sealed record InterviewPrepContradictionSignal(
    string CompetencyId,
    string Summary,
    IReadOnlyList<string> ConflictingClaims);

public static class InterviewPrepBarRaiserSignals
{
    public static IReadOnlyList<InterviewPrepContradictionSignal> DetectContradictions(
        InterviewPrepSessionEntity session)
    {
        var grouped = session.EvidenceItems
            .Where((item) => !string.IsNullOrWhiteSpace(item.CompetencyId))
            .GroupBy((item) => item.CompetencyId, StringComparer.Ordinal);

        var signals = new List<InterviewPrepContradictionSignal>();
        foreach (var group in grouped)
        {
            var polarities = group
                .Select((item) => item.Polarity?.Trim().ToLowerInvariant())
                .Where((value) => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (polarities.Contains("positive") && polarities.Contains("negative"))
            {
                var claims = group
                    .Select((item) => item.Claim)
                    .Where((claim) => !string.IsNullOrWhiteSpace(claim))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(4)
                    .ToArray();

                signals.Add(new InterviewPrepContradictionSignal(
                    group.Key,
                    "Evidence ledger shows conflicting claims for the same competency.",
                    claims));
            }
        }

        return signals;
    }

    public static bool ShouldPrioritizeConsistencyProbe(
        InterviewPrepPersona persona,
        InterviewPrepSessionEntity session) =>
        persona == InterviewPrepPersona.BarRaiser && DetectContradictions(session).Count > 0;
}
