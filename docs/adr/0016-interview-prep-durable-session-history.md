# ADR-0016: Interview Prep durable session history (supersedes ADR-0012 §1)

## Status

Accepted (2026-08-03 — operate `interview-prep-ip-13-2026-08-03` / GitHub [#79](https://github.com/PrimusInterParess/ApplyVault/issues/79) IP-13). Principal accepted with OQ defaults: session-wins on durable turn fields; FE always-durable after ship; max ~200 messages/session via options.

## Context

Interview Prep MVP (ADR-0012) chose **ephemeral** coach turns: no sessions table; Angular holds chat/`priorTurns`; refresh loses transcript and scorecards. That matched sibling AI features and deferred store cost.

IP-13 / [#79](https://github.com/PrimusInterParess/ApplyVault/issues/79) now requires durable per-user history: list past sessions, open completed sessions read-only, resume in-progress sessions, and user-initiated delete — without mutating Structured CV or sharing sessions externally.

ADR-0012 decisions **2–8** (profession-agnostic grounding, dedicated `/api/interview-prep/*` surface, inputs, mode catalog, `languageMix`, scorecard shape, Gemini HTTP client) remain in force. ADR-0013 (`hiringMarket`) and ADR-0015 (`modelAnswer`) remain additive turn-contract rules. Auth/tenancy: ADR-0009 / ADR-0010. Persistence provider: EF Core SQL Server / LocalDB (verified).

Current evidence: sole endpoint `POST /api/interview-prep/turns` (`InterviewPrepController`); DTOs in `InterviewPrepContracts.cs`; no prep entities in `ApplyVaultDbContext`; FE facade signals under `features/interview-prep/` hold all session state.

## Decision

1. **Supersede ADR-0012 decision point 1 only.** Durable Interview Prep sessions are in scope for IP-13. Profession-agnostic coaching, dedicated API surface, mode/`languageMix`/`hiringMarket`/scorecard/`modelAnswer` contracts, and CV non-mutation stay as in ADR-0012 / 0013 / 0015.

2. **EF entities (SQL Server).** Add user-owned tables (names illustrative; implementers may match project naming):
   - `InterviewPrepSession` — `Id`, required `UserId` (FK → `AppUser`, cascade delete with user), `Mode`, `LanguageMix`, `HiringMarket`, optional `ScrapeResultId` (FK → scrape, **ON DELETE SET NULL**), denormalized nullable `JobTitle` / `CompanyName` snapshots for list display, `Status` (`in_progress` | `completed`), `Phase` (latest `interview` | `debrief`), optional latest `InferenceJson` / `LatestScorecardJson` / `LatestOverallScore`, `CreatedAt`, `UpdatedAt`, `CompletedAt?`.
   - `InterviewPrepSessionMessage` — `Id`, `SessionId` (FK cascade), `Sequence` (monotone int), `Role` (`user` | `coach`), `Text`, `Phase`, optional coach-only `ScorecardJson`, `FollowUpsJson`, `DebriefBulletsJson`, `ModelAnswer`, `InferenceJson`, `CreatedAt`.
   - Index: `(UserId, UpdatedAt DESC)` on sessions; unique `(SessionId, Sequence)` on messages.
   - **Never** write Structured CV or mutate scrape content from prep services.

3. **Session lifecycle / status.**
   - Create via `POST /api/interview-prep/sessions` with mode / languageMix / hiringMarket / optional scrapeResultId (server validates owned scrape; snapshots job title/company). Status starts `in_progress`.
   - Append turns via durable turn path (below). When a persisted turn response has `phase=debrief`, set session `Status=completed`, `CompletedAt=now`. Completed sessions reject further turns (`409` or `400` — prefer `409 Conflict`).
   - **Resume** allowed only for `in_progress`. **Read-only open** for `completed` (GET full transcript + scorecards; no new turns).
   - Abandoned `in_progress` sessions remain listable until the user deletes them.

4. **API surface (additive; keep ephemeral turns).** Still on `InterviewPrepController` / `api/interview-prep`:
   - `POST /sessions` — create
   - `GET /sessions` — list current user’s sessions (metadata for UI: id, created/updated, mode, languageMix, hiringMarket, status, optional job title/company, optional latest overall score)
   - `GET /sessions/{id}` — full session + ordered messages (404 if missing/foreign)
   - `DELETE /sessions/{id}` — **hard delete** session + messages (user retention minimum); 404 if missing/foreign
   - Extend `POST /turns` with optional `sessionId`:
     - **Omitted / null** → existing ephemeral behavior (ADR-0012 path; no DB write). Kept for compatibility; IP-13 FE should not rely on it for history.
     - **Present** → load owned `in_progress` session; **server builds AI `priorTurns` from stored messages** (ignore client `priorTurns` when `sessionId` set, or treat as unused); after successful AI response, append user + coach messages; update session metadata; return the same turn response DTO shape as today (plus echo `sessionId` if useful — optional additive field).
   - Auth: `[Authorize]` + `GetRequiredUserAsync`; all queries filter `UserId == user.Id` (ADR-0010).

5. **Transcript vs AI context caps.** Persist the **full** message transcript (subject to a generous server max messages/chars per session for abuse control). When calling Gemini, continue truncating to existing `MaxPriorTurns` / `MaxPriorTurnChars` from the **tail** of stored messages. `modelAnswer` may be stored on coach messages for read-only replay / reveal; it is still **not** part of AI `priorTurns` (ADR-0015).

6. **Retention / deletion policy (MVP).**
   - Minimum: authenticated owner may hard-delete any of their sessions.
   - No automatic TTL / admin purge in IP-13.
   - No external sharing, export-to-coach, or cross-user access.
   - Session rows are personal practice data; delete is irreversible hard delete (not soft-delete).

7. **Frontend shape (contract-facing).** On `/interview-prep`: history list (date, mode, optional job title, status); open completed read-only; resume in-progress by hydrating facade from `GET /sessions/{id}` then continuing durable turns; delete action. New practice flow: create session → turns with `sessionId`. Deep-link `jobId` still maps to create-session `scrapeResultId`.

8. **Out of scope / boundary.** Sharing; IP-15 prep notes and #44 saved-job notes (separate features — history stores coach transcript/scorecards only); Redis session cache; nesting under `cv-documents`.

## Consequences

- ADR-0012 §1 is superseded; points 2–8 and sibling Interview Prep ADRs remain.
- Requires EF migration + DbContext registration; `MigrateAtStartup` path picks it up like other entities.
- Implementers land frozen contracts under the IP-13 handoff; registry entry `interview-prep-sessions` proposed until Principal accepts this ADR and code lands.
- FE can survive refresh / multi-device resume for in-progress sessions; completed sessions are reviewable.
- Storage and GDPR surface grow (user delete is the MVP control); empty or abandoned sessions may clutter the list until deleted.
- Ephemeral `POST /turns` without `sessionId` remains for backward compatibility but is not the IP-13 product path.

## Rejected alternatives

| Option | Why rejected |
| --- | --- |
| JSON blob-only session (no message rows) | Harder append/resume correctness; normalized messages match existing `priorTurns` shape |
| Redis / in-memory durable cache | Optional Redis is not a durable product store; tenancy/retention weaker |
| Always-on persistence only (remove ephemeral path) | Breaks “MVP ephemeral redesign out of scope” unless required; keep additive `sessionId` |
| Soft-delete sessions | AC asks user delete at minimum; hard delete is clearer for chat privacy MVP |
| Nest under `cv-documents` | Violates ADR-0012 dedicated surface |

## Links

- Issue: [#79](https://github.com/PrimusInterParess/ApplyVault/issues/79) IP-13
- Supersedes: ADR-0012 decision §1 only — `docs/adr/0012-interview-prep-ephemeral-profession-agnostic.md`
- Related: ADR-0013, ADR-0015, ADR-0002, ADR-0008, ADR-0009, ADR-0010
- Frozen contracts (design): `agent-system/handoffs/active/interview-prep-ip-13-2026-08-03/frozen-contracts-session-history.md`
- Plan: `agent-system/implementation-plan-interview-prep.md`
