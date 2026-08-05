# ADR-0023: Interview Prep full-loop orchestration

## Status

Accepted (M10 operational delivery, 2026-08-05)

Extends [ADR-0021](0021-interview-prep-v2-bounded-module.md). Supersedes ADR-0022 item 8 (“FullLoop remains non-operational until M10”).

## Context

Interview Prep v2 requires a coordinated multi-stage “full loop” using existing operational mode×persona pairs (recruiter screening, hiring manager, behavioral, senior peer, problem-solving case, bar raiser) without duplicating session engines or leaking prior stage scores to later interviewers.

## Decision

1. **Single parent session, multiple stages:** `InterviewPrepMode.FullLoop` on one `InterviewPrepSession` with ordered `InterviewPrepStage` rows. No child session entities.

2. **Orchestration:** parent `PlanJson` holds `InterviewPrepFullLoopOrchestration` (ordered stage slots, loop summary, coverage goals). Each stage’s adaptive plan lives in that stage’s `PlanJson` (`InterviewPrepStagePlanBundle`: mode, persona, plan).

3. **Cross-stage Loop Guard:** session-wide turn history across stages. Intentional cross-stage revisits require approval via `POST /api/interview-prep/sessions/{id}/loop-guard/revisit` and a persisted reason.

4. **Stage handoff:** later stages receive factual summaries, competencies covered, stories discussed, and unresolved questions through private conversation summary context. **Prior stage scores are not** injected into adaptive runtime or interviewer wording before the later stage completes its own assessment.

5. **Panel debrief:** `GeneratePanelDebrief` via the AI gateway, enriched from evidence ledger (contradictions, missing evidence, confidence). Persist in `PanelDebriefJson`. No simple score averaging.

6. **API:** `POST .../full-loop/next-stage`, `POST .../loop-guard/revisit`, `GET .../panel-debrief`.

## Consequences

- Clients advance full loops with `full-loop/next-stage`; debrief after all stages complete.
- Stage assessments accumulate in session JSON fields as implemented; candidate report remains session-scoped.
- Migration adds panel debrief (and related) persistence as required.
