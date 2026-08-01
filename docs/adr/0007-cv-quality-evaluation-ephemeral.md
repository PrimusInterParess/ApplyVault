# ADR-0007: CV quality evaluation is ephemeral

## Status

Accepted (2026-08-01 — operate `cv-quality-evaluation-2026-08-01`; plan D2/D7 promoted)

## Context

Assist “Evaluate CV” scores Structured CV content for advisory feedback. A durable evaluation history or job-description matching would imply new storage, UX, and product scope. Plan D7 deferred an optional ADR; the feature shipped with locked decisions and needs domain-memory citation.

ADR-0001 / ADR-0002 remain in force. This does not change Assist **Update with instructions** (immediate persist) or ADR-0004 Summary propose-then-approve.

## Decision

1. `POST /api/cv-documents/current/structured/ai-evaluation` returns scores, findings, and optional self-check questions and **never persists** — no evaluation table, history, or durable Apply path.
2. UI keeps results in **session / Assist panel state only**.
3. Evaluation axes are **content / structure / format of structured fields** (catalog-backed), not Template CSS or export layout aesthetics.
4. **No job-description matching in v1** — request shape may leave room for later JD, but v1 rejects unused JD scope.
5. Placement: Assist panel Evaluate block; findings do not auto-edit the Structured CV. “Use in Assist” (when present) copies into Update-with-instructions only — does not auto-run Update.

## Consequences

- Users lose evaluations on refresh; re-run Evaluate as needed.
- JD-aware scoring is a separate milestone/plan, not a silent extension of this endpoint.
- Plan: `agent-system/implementation-plan-cv-quality-evaluation.md`.
