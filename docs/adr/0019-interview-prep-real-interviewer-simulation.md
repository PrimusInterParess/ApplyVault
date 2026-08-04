# ADR-0019: Interview Prep real-interviewer simulation

## Status

Accepted (2026-08-04 — user-approved implementation plan `real interviewer prep`).

## Context

Interview Prep has durable session history, profession-agnostic grounding, answer guides, and anti-repeat guards. The remaining product gap is realism: the flow can still feel like a generic AI coach asking one question at a time instead of a real interviewer with a role, agenda, pacing, probes, and a coherent close.

The existing durable session model is the right base. Replacing it with voice, streaming, embeddings, or multi-agent panels would add complexity before solving the core interview-flow problem.

## Decision

1. Add a durable `interviewerProfile` to each Interview Prep session. Supported profiles are `recruiter`, `hiring_manager`, `senior_peer`, and `bar_raiser`.
2. Add server-built durable agenda state to each session: `AgendaJson` and `CurrentAgendaStep`. The agenda is mode-aware and profession-agnostic.
3. Extend the Gemini turn contract with `turnState`, including `interviewMove`, `questionType`, `pressureLevel`, `interviewerIntent`, `agendaStep`, optional `nextAgendaStep`, optional `memorySummary`, and `listeningNotes`.
4. Store turn state on coach messages and update session metadata after each successful durable turn: latest move, current agenda step, and compact interviewer memory.
5. Keep coaching aids (`followUps`, `modelAnswer`) available, but let the UI present the active session as a live interview first. Candidate aids can be hidden behind a Coach mode toggle.
6. Keep prior decisions in force: Structured CV and saved jobs are read-only inputs, `modelAnswer` remains an orientation guide rather than a fabricated script, and the coach remains profession-agnostic.

## Consequences

- Sessions have a more realistic arc: opening, focused probes, transitions, close, and debrief.
- The server has durable state for pacing instead of relying only on prompt history and anti-repeat checks.
- The public API gains additive fields, so existing clients that ignore unknown JSON remain compatible.
- Prompt overrides must include the new profile, agenda, memory, and `turnState` instructions.
- Existing sessions receive migration defaults and continue to load.
