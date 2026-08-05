# ADR-0021: Interview Preparation v2 bounded module

## Status

Accepted (human-approved 2026-08-05 via `/operate`)

Supersedes historical Interview Prep ADRs **0012–0020** (ephemeral coach turns, hiring-market bias, model-answer reveal, durable message sessions, already-asked digest, answer-guide orientation, real-interviewer simulation, stateless context filter, and related follow-ons). Those decisions are retired; do not revive MVP routes or schemas.

## Context

ApplyVault already provides authentication, Structured CV, and saved-job (scrape) context. An earlier Interview Prep MVP used ephemeral/durable coach turns without stage machines, evidence ledger, or application-owned Loop Guard. That MVP is removed from the API surface.

Product requires a bounded backend module that runs a realistic, stateful adaptive interview: immutable CV/job snapshots, explicit session/stage state machines, competency/evidence tracking (later milestones), Interview Loop Guard, and an AI gateway where AI proposes and the application owns truth.

## Decision

1. **Bounded module** under `api/ApplyVault.Api` with dedicated REST prefix `/api/interview-prep/*`. Do not nest under `cv-documents`. Do not duplicate Candidate, CV, Job, User, or Auth entities.

2. **Adapters + immutable snapshots.** Candidate context comes from Structured CV (`ICvStructuredDocumentService`); selected job from scrape store (`IScrapeResultStore`). Snapshots are captured at **Prepare** only; in-flight sessions do not change when the CV or job is later edited. Optional `scrapeResultId` uses ON DELETE SET NULL while retaining snapshot columns.

3. **Configuration dimensions** (separate enums; M1 exposes only supported values as operational):
   - Mode, Persona, Language, Market, ExperienceType, InteractionType
   - Initial operational set: `ScreeningAndMotivation`, `BehavioralAndCulture`; `Recruiter`, `HiringManager`; `English`; `General`; `RealisticSimulation`, `GuidedCoaching`; `Text`
   - Wire format: camelCase JSON enum names (e.g. `screeningAndMotivation`)
   - Unsupported future values must not be advertised as supported

4. **Application owns state.** Session/stage/turn transitions, ownership, concurrency, idempotency, Loop Guard acceptance, and persistence are application logic. AI may propose next actions, wording, assessments, and reports; it must not directly mutate session state.

5. **Lifecycle (Milestone 1+).** Explicit session states (`Created` → `Preparing` → `Ready` → `InProgress` ⇄ `Paused` → `Completing` → `Completed`, plus `Cancelled` / `Failed`) and stage states through Planned…Completed. Illegal transitions return **409**. Optimistic concurrency via ETag/`If-Match` backed by an application `ConcurrencyStamp` (M1; SQL `rowversion` deferred for EF InMemory test compatibility). Turn idempotency via `clientTurnId`.

6. **Interview Loop Guard** is a first-class domain service (signatures, layered duplicate detection, configurable limits, deterministic fallback)—not prompt-only prevention.

7. **AI clients** (later milestones) follow ADR-0008 (Gemini raw HTTP, no SDK) behind a shared Interview Prep AI gateway with named operations, structured validation, timeouts, and bounded retries.

8. **Milestone 1** delivers domain + persistence + lifecycle + fixed-question test interview + unit/integration tests, without the full adaptive AI product loop.

9. **Retention (M1):** hard delete by owning user; no TTL policy.

10. **Prepare** is synchronous in M1 (local CV/scrape reads). Revisit if AI briefing enters prepare.

## Consequences

- New EF tables (`InterviewPrepSessions`, `InterviewPrepStages`, `InterviewPrepTurns`) after MVP drop migration.
- Contract registry entry `interview-prep-v2-sessions` (APPROVED after M1 lands).
- Frontend and voice reuse the same session engine later; out of scope for M1.
- Historical MVP contracts and ADRs must not be partially reapplied; implementers follow this ADR and the approved design handoff.
