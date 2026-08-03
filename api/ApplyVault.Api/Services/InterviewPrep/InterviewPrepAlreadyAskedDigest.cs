using System.Text.Json;
using ApplyVault.Api.Models;

namespace ApplyVault.Api.Services;

/// <summary>
/// Builds the compact already-asked digest for Interview Prep AI turns (ADR-0017).
/// Source is the full prior transcript before MaxPriorTurns truncation; members are
/// coach + interview texts outside the retained priorTurns tail.
/// </summary>
internal static class InterviewPrepAlreadyAskedDigest
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static IReadOnlyList<string> Build(
        IReadOnlyList<InterviewPrepPriorTurnDto>? priorTurns,
        int maxPriorTurns,
        int maxItems,
        int maxItemChars,
        int maxTotalChars)
    {
        if (priorTurns is null
            || priorTurns.Count == 0
            || maxItems <= 0
            || maxTotalChars <= 0
            || priorTurns.Count <= maxPriorTurns)
        {
            return [];
        }

        var outsideCount = priorTurns.Count - maxPriorTurns;
        var candidates = new List<string>();

        for (var i = 0; i < outsideCount; i++)
        {
            var turn = priorTurns[i];
            if (!string.Equals(turn.Role, InterviewPrepTurnRoles.Coach, StringComparison.Ordinal))
            {
                continue;
            }

            var phase = string.IsNullOrWhiteSpace(turn.Phase)
                ? InterviewPrepPhases.Interview
                : turn.Phase;

            if (!string.Equals(phase, InterviewPrepPhases.Interview, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(turn.Text))
            {
                continue;
            }

            var text = turn.Text.Trim();
            if (text.Length > maxItemChars)
            {
                text = text[..maxItemChars];
            }

            candidates.Add(text);
        }

        if (candidates.Count == 0)
        {
            return [];
        }

        // Prefer the most recently dropped questions when over the item cap.
        if (candidates.Count > maxItems)
        {
            candidates = candidates.Skip(candidates.Count - maxItems).ToList();
        }

        // Fill newest-first under the total-char budget so recently fallen-out
        // questions survive when MaxAlreadyAskedTotalChars binds below maxItems.
        var newestFirst = new List<string>(candidates.Count);
        for (var i = candidates.Count - 1; i >= 0; i--)
        {
            var item = candidates[i];
            var trialCount = newestFirst.Count + 1;
            var trial = new string[trialCount];
            newestFirst.CopyTo(trial);
            trial[newestFirst.Count] = item;

            var serializedLength = JsonSerializer.Serialize(trial, SerializerOptions).Length;
            if (serializedLength > maxTotalChars)
            {
                break;
            }

            newestFirst.Add(item);
        }

        // Return oldest→newest for prompt readability after selection.
        newestFirst.Reverse();
        return newestFirst;
    }
}
