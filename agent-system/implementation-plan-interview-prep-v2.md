# Implementation plan — Interview Prep v2

**Status:** APPROVED (design + §11 defaults, 2026-08-05)  
**ADR:** `docs/adr/0021-interview-prep-v2-bounded-module.md`  
**Design handoff:** `agent-system/handoffs/archive/interview-prep-v2-design-2026-08-05/` (after Close)  
**Task id (M1):** `interview-prep-v2-m1-2026-08-05` (DONE)  
**Task id (M2):** `interview-prep-v2-m2-2026-08-05` (DONE)  
**Task id (M3):** `interview-prep-v2-m3-2026-08-05` (DONE)  
**Task id (M4):** `interview-prep-v2-m4-2026-08-05` (DONE)  
**Task id (M5):** `interview-prep-v2-m5-2026-08-05` (DONE)  
**Task id (M6):** `interview-prep-v2-m6-2026-08-05` (DONE)  
**Task id (M7):** `interview-prep-v2-m7-2026-08-05` (DONE)  
**Task id (M8):** `interview-prep-v2-m8-2026-08-05` (DONE)  
**Task id (M9):** `interview-prep-v2-m9-2026-08-05` (DONE)  
**Task id (M10):** `interview-prep-v2-m10-2026-08-05`  

## Approved §11 defaults

1. Enum JSON: camelCase names (`screeningAndMotivation`)
2. Concurrency: SQL `rowversion` + ETag / If-Match
3. Snapshots: Prepare-only immutability
4. Prepare: synchronous in M1
5. Retention: hard delete by owner
6. M1 ships full lifecycle routes; AI later
7. Accept ExperienceType values; InteractionType `Text` only for behavior until later
8. Single primary ADR-0021 superseding 0012–0020
9. Light per-user rate limit on prepare/turns in M1
10. API param `scrapeResultId`
11. Separate config dimensions: Mode, Persona, Language, Market, ExperienceType, InteractionType

## Milestones

| Id | Focus | Status |
|---|---|---|
| M1 | Domain + lifecycle + snapshots + Loop Guard seam + fixed-question interview + tests | DONE (2026-08-05) |
| M2 | Shared AI gateway, prompts, fake provider | DONE (2026-08-05) |
| M3 | Context builder + planner + competency defs | DONE (2026-08-05) |
| M4 | Adaptive runtime + Loop Guard AI path | DONE (2026-08-05) |
| M5 | Reporting | DONE (2026-08-05) |
| M6 | Coaching | DONE (2026-08-05) |
| M7 | Expanded modes/personas (RoleAndDomainDepth, ProcessAndSystems, SeniorPeer) | DONE (2026-08-05) |
| M8 | Cases + BarRaiser | DONE (2026-08-05) |
| M9 | Language/market (Danish, mixed, Danish market) | DONE (2026-08-05) |
| M10 | Full loop | DONE (2026-08-05) |
| M11 | Angular UI (Interview Prep v2) | IN PROGRESS |

## M1 definition of done

- Authenticated user can create/prepare/start/pause/resume/complete/cancel a session
- CV (+ optional job) snapshots persisted at prepare
- Fixed-question test interview advances turns with idempotent `clientTurnId`
- State machines + ownership + concurrency enforced
- Loop Guard runs on interviewer questions (fixed bank)
- Unit + integration tests for lifecycle, tenancy, idempotency, illegal transitions
- No Gemini Interview Prep client required in M1 (port stub OK)

## Non-goals (M1)

Frontend, voice, live AI adaptive loop, evidence ledger UI exposure, billing, retention TTL.
