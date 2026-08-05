# ADR-0024: Interview Prep Full-loop interviewer-led pacing and handoff

## Status

Accepted (2026-08-05)

Extends [ADR-0023](0023-interview-prep-full-loop-orchestration.md). Supersedes ADR-0023 consequence that clients must drive stage advance via `POST .../full-loop/next-stage` for the happy path.

## Context

Full-loop sessions were ending after a short Screening Stage (~4 main questions) because per-mode `MaxQuestions` force-closed into Candidate questions / Close. Completing the Stage when Close was *emitted* left no active Stage for the seeker’s reply, and `canAdvanceFullLoop` never enabled “Next stage.” Product intent is one continuous interview day guided by the interviewer, not seeker stage gates.

## Decision

1. **Interviewer-led Full loop:** runtime auto-advances Stages. `full-loop/next-stage` remains for recovery/manual use, not the primary path.
2. **No per-Stage Close in Full loop:** mid-loop end is a short outgoing-persona **Stage handoff**, then next Stage opening. **Candidate questions** and **Close** occur once at the **end of the whole loop**. Standalone modes keep Candidate questions + Close at session end.
3. **Close completes after the seeker’s reply** to Close (not when Close is emitted).
4. **Pacing:** soft target **~8–12** main questions; hard safety **~15–18**. Only hard safety (or coverage/AI handoff rules) force Stage end. Probes/clarifications do not end the Stage. Same bands for standalone modes.
5. **Full-loop turn ceiling:** higher `MaxSessionTurns` for Full loop (~80–100); standalone may keep ~40.
6. **Handoff wait UX:** UI status banner while Stage assessment + factual handoff run before the next interviewer speaks (ADR-0023: no prior Stage scores in later interviewer context).
7. **Live AI:** `EvaluateStage` and `SummarizeConversation` are implemented on the live Gemini transport (not fake-only).

## Consequences

- Adaptive runtime must distinguish soft target vs hard safety and Full-loop mid-stage handoff vs loop-final Close.
- Clients show a Stage-transition busy state; seeker does not click through Stages in the happy path.
- ADR-0023 orchestration, factual handoffs, and panel debrief remain in force.
