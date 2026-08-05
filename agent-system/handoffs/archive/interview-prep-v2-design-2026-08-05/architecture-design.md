## Architecture design summary

- Request: redesign (Interview Preparation → bounded backend module v2)
- Status: COMPLETE (design-only; human approval gate before Milestone 1)
- Recommendation: Replace the removed MVP (ephemeral/durable coach turns under ADRs 0012–0019) with a new **InterviewPrep** bounded context that owns session/stage/turn state machines, immutable CV/job snapshots, Loop Guard, and an AI-proposal gateway — AI proposes; application owns truth.

## Current state

- ASP.NET Core **net10.0** API (`api/ApplyVault.Api/`), EF Core SQL Server (+ InMemory tests), Supabase JWT → local `AppUser`, Gemini raw HTTP (ADR-0008), optional Redis locks/cache.
- Interview Prep MVP source and ADRs 0012–0019 are being / have been removed from the working tree; treat as **superseded historical context**, not the target shape.
- CV and saved-job (scrape) modules remain the integration points; no separate `Candidate` entity exists.

## Target state

- New bounded module under `api/ApplyVault.Api` (controller + `Services/InterviewPrep/` + EF entities) with rich lifecycle, snapshots, Loop Guard, and extension points for interview kinds / delivery modes.
- Milestone 1 ships domain + persistence + lifecycle + fixed-question test interview (no full AI product loop required in M1).

## Options considered

- Recommended: **Greenfield module** on cleared tables (after `DropInterviewPrepTables`), new schema/contracts; cite 0012–0019 as superseded history.
- Rejected: Incremental patch of MVP message/session model — product requires stages, evidence ledger, coverage, Loop Guard, and AI-owned-vs-app-owned separation the old shape cannot carry cleanly.
- Rejected: Nest under `cv-documents` — violates dedicated surface pattern and CV mutation boundary.

## Impacted contracts

- **Proposed (new):** `interview-prep-v2-sessions` public REST under `/api/interview-prep/*`.
- **Approved existing (reuse, no mutation):** `supabase-jwt`, `cv-section-catalog` / Structured CV reads, scrape-results ownership reads, `google-ai-gemini-http` (later milestones).
- **Historical removed:** prior `interview-prep-turns` / sessions contracts from MVP — do not revive.

## Migration / sequencing

1. Finish MVP deletion (migration drop applied; no leftover DI/tests).
2. Human approve this design + Principal writes numbered supersession ADRs.
3. Milestone 1: domain/lifecycle/persistence/fixed-question path + tests.
4. Later: AI gateway, briefs/plans, adaptive questioning, assessment, evidence/coverage, coaching/reports.

## Ownership recommendations

- Primary implementation: `backend-engineer` for module/API/EF.
- AI client/prompts (post-M1): `ai-llm-engineer` secondary.
- Design author: `architecture-engineer` (this handoff); ADRs + domain memory: Principal after approval.
- Update ownership-matrix row for Interview Prep v2 after approval.

## Risks and open decisions

- Public enum wire format (PascalCase vs snake); concurrency token strategy; snapshot freshness policy; retention; whether M1 exposes public AI endpoints or internal fixed-question only; background job necessity for Preparing stage.
- See §11.

## Next actions for implementers

- Wait for human design approval.
- Principal: supersession ADRs for 0012–0019 (and note deleted-from-repo status).
- Backend: Milestone 1 file list in §10; TDD via project skills when tasked.
- Do not implement until approval gate clears.

---

# Interview Preparation v2 — Architecture Design

**Task:** `interview-prep-v2-design-2026-08-05`  
**Agent:** architecture-engineer  
**Mode:** BROWNFIELD / BRIDGE — design only  
**Status:** READY for human approval (not implementation)

**WIP note:** Working tree is actively removing the old Interview Prep MVP (controller/services/entities gone from disk; `20260805120000_DropInterviewPrepTables` present; ADRs 0012–0019 deleted in `7815e13`). This design is the **replacement** module.

**Recommended ADR supersession (PROPOSED — Principal writes numbered ADRs after human approval):** Explicitly supersede historical Interview Prep ADRs **0012–0019** (ephemeral coach, hiring-market bias, model-answer reveal, durable message sessions, already-asked digest, answer-guide orientation, real-interviewer agenda/turnState). Preserve non-Interview-Prep ADRs (0001, 0008, 0009, 0010, etc.). Profession-agnostic grounding and CV/job non-mutation intent should be **re-stated** in the new ADR rather than left as “still in force from 0012.”

---

## 1. Summary of existing repository architecture

### Solution / versions

| Item | Evidence |
|---|---|
| Solution | `api/ApplyVault.Api/ApplyVault.Api.sln` |
| API project | `api/ApplyVault.Api/ApplyVault.Api.csproj` — **net10.0**, nullable, ImplicitUsings |
| Unit tests | `api/ApplyVault.Api.Tests/` — net10.0, xunit, EF InMemory/Sqlite packages |
| Integration tests | `api/ApplyVault.Api.IntegrationTests/` — `WebApplicationFactory<Program>`, TestAuth scheme |
| Shared CV catalog | `shared/cv-section-catalog/cv-section-catalog.json` linked into API output |

### Architectural patterns

- Thin `Program.cs` + extension methods: `AddApplyVault*` / `UseApplyVaultPipeline` (`Infrastructure/`).
- Controllers: `[ApiController]`, `[Route("api/...")]`, `[Authorize]` for user data; camelCase JSON.
- Services: interface + scoped implementation; feature folders (`Services/CvDocuments/`, `Services/Eures/`, …).
- Options pattern with `ValidateOnStart` for critical config (`GoogleAi`, OAuth, storage).
- Persistence: single `ApplyVaultDbContext`; entities colocated under `Data/` (and some in `ScrapeResultEntity.cs` file for AppUser/CV).
- AI: dedicated `GoogleAi*` HTTP clients per feature (ADR-0008) — no Gemini SDK.
- Auth: Supabase JWT validation → upsert local `AppUserEntity` (`IAppUserService`) — ADR-0009.
- Tenancy: all owned reads/writes filter by `user.Id` — ADR-0010.
- Errors: `AddProblemDetails` + `UseExceptionHandler`; specialized `IExceptionHandler`s; controllers often map `InvalidOperationException` → 400, miss → 404, conflicts → 409.
- Background: `BackgroundService` for Gmail sync (optional); Puppeteer `IHostedService` for HTML CV export.
- Distributed: optional Redis cache + `IDistributedLockProvider` (`DistributedInfrastructureExtensions.cs`).
- Rate limiting: ASP.NET rate limiter policies on selected endpoints (`RateLimitingExtensions.cs`).
- **No** ASP.NET API versioning package — versioning is by resource path stability.

### DB provider + persistence conventions

- Production/dev relational: **EF Core SQL Server** via `ConnectionStrings:ApplyVault` (`ApplyVaultDatabaseExtensions.cs`).
- Tests: `Testing:UseInMemoryDatabase`.
- Migrations under `api/ApplyVault.Api/Migrations/`; snapshot `ApplyVaultDbContextModelSnapshot.cs`.
- FK delete behaviors explicit (e.g. scrape→user Restrict; CV cascade with user).
- String status/enum columns common (`nvarchar` max lengths); JSON blobs in `nvarchar(max)` for flexible AI payloads.

### Auth / current-user

- `ConfigureSupabaseJwtBearerOptions.cs`, `SupabaseClaimTypes`, `AppUserService.GetRequiredUserAsync` / `TryGetCurrentUserAsync`.
- Controllers call `GetRequiredUserAsync()` then pass `AppUserEntity` or `user.Id` into stores/services.

### Existing AI integrations (reuse pattern, not MVP code)

Examples still registered in `ServiceCollectionExtensions.AddApplyVaultApplicationServices`:

- `GoogleAiScrapeResultClient`, CV import/update/suggestions/evaluation/summary/export clients, `GoogleAiGitHubProjectClient`.
- Gate: `GoogleAi:Enabled` + per-feature options sections.

### Background processing

- `GmailMailSyncBackgroundService` + `GmailMailSyncWorker` with distributed lock.
- HTML export browser host. Not a general job queue — prefer request-scoped work unless long-running.

### Test conventions

- Unit: xunit in `ApplyVault.Api.Tests`; `InternalsVisibleTo` from API.
- Integration: in-memory DB, GoogleAi/Mail off, TestAuth handler (`ApplyVaultWebApplicationFactory.cs`).
- Tenancy coverage pattern: `ScrapeResultsTenancyIntegrationTests.cs`.

### API response conventions

- Direct DTO records in `Models/`; `Ok`/`Created`/`NoContent`/`BadRequest(new { message })`/`NotFound`/`Conflict`.
- No global envelope; camelCase property names.

### Interview Prep MVP (historical — superseded)

Former shape (from deleted ADRs / drop migration Down()): user-owned `InterviewPrepSessions` + `InterviewPrepSessionMessages`; modes/languageMix/hiringMarket; later interviewerProfile/agenda/turnState; ephemeral + durable turns; already-asked digest. **Do not extend.** Design v2 cleanly after drop.

---

## 2. Existing CV and candidate integration points

There is **no** separate Candidate aggregate. The seeker identity is `AppUserEntity`; profile content is the **Structured CV** on the single per-user CV document.

| Concern | Path / type | Integration rule for Interview Prep |
|---|---|---|
| App user | `Data/ScrapeResultEntity.cs` → `AppUserEntity`; `IAppUserService` | Session `UserId` FK; never duplicate user rows |
| CV document | `UserCvDocumentEntity` / sections / entries | Read-only via service |
| Structured CV API | `ICvStructuredDocumentService.GetStructuredAsync` → `CvStructuredDocumentDto` | Adapter loads at session prepare; snapshot immutable copy |
| Catalog | ADR-0001 + `ICvSectionCatalog` / `shared/cv-section-catalog/` | Snapshot may store catalog version id; validate shape against catalog vocabulary |
| Mutation ban | `SaveStructuredAsync` / import / update propose | **Forbidden** from Interview Prep module |

**Adapter (proposed):** `IInterviewPrepCandidateContextAdapter` reading `GetStructuredAsync(user)`; fails create/prepare if no Structured CV (product gate: prep starts after CV/profile exists).

**Snapshot:** Persist `CvSnapshotJson` (+ `CvDocumentId`, `StructuredImportedAt`, `CatalogVersion`, `CapturedAt`) on the session (or dedicated snapshot table). Later CV edits do not mutate in-flight prep.

---

## 3. Existing selected-job integration points

Saved jobs are **scrape results**, not a separate Job entity.

| Concern | Path / type | Integration rule |
|---|---|---|
| Store | `IScrapeResultStore.GetByIdAsync(id, userId)` | Owned read only; 404 if foreign/missing |
| DTO | `SavedScrapeResult` / `JobDetailsDto` (`Models/ScrapeContracts.cs`) | Title, company, location, job description, capture quality |
| HTTP | `ScrapeResultsController` `GET api/scrape-results/{id}` | Do not call controller; use store |
| Calendar interview event | `InterviewEventEntity` on scrape | Optional context only; not Interview Prep session state |
| Public search save | EURES/Jobnet save → scrape store | Same scrape id deep-link as before (`jobId` → scrape GUID) |

**Adapter (proposed):** `IInterviewPrepJobContextAdapter` → optional scrape by id; snapshot `JobTitle`, `CompanyName`, `JobDescription` (effective values), `ScrapeResultId?`, `CapturedAt`.

**FK policy (recommended):** optional `ScrapeResultId` with **ON DELETE SET NULL**, retaining snapshot columns so history survives job delete (same spirit as historical ADR-0016).

**Mutation ban:** never `UpdateDescriptionAsync` / capture review / delete from prep.

---

## 4. Proposed Interview Preparation module boundary

### In scope (module owns)

- Interview prep **sessions**, **stages**, **turns**, **plans/briefs** (later), **assessments**, **evidence ledger**, **coverage**, **Loop Guard**, **coaching/report** aggregates (later milestones).
- State machines, idempotency, pause/resume, concurrency.
- Immutable CV/job snapshots at prepare time.
- AI **gateway ports** (interfaces); application decides what to persist after proposals.
- Public REST under `/api/interview-prep/*` (dedicated surface — not under `cv-documents`).

### Out of scope / adapters only

- Auth IdP, AppUser CRUD, Structured CV editing, scrape ingest, calendar/mail/GitHub, payments.
- Frontend (design is backend-only; FE contracts deferred).

### Logical packages (Milestone 1+)

```
api/ApplyVault.Api/
  Controllers/InterviewPrepController.cs
  Models/InterviewPrep/          # public DTOs
  Services/InterviewPrep/
    Domain/                      # enums, state machines, Loop Guard pure logic
    Adapters/                    # CV + scrape adapters
    Persistence/                 # repositories or service using DbContext
    Ai/                          # ports + (later) GoogleAiInterviewPrep* clients
  Data/InterviewPrep*.cs         # EF entities
  Options/InterviewPrepOptions.cs
```

### AI vs application ownership

| AI proposes | Application owns |
|---|---|
| Next question text, probes, brief/plan drafts, scores, coaching tips, report narratives | Session/stage/turn status, allowed transitions, snapshots, Loop Guard decisions, persistence, idempotency keys, final assessment acceptance |

---

## 5. Proposed domain model

### Configuration enums (initial release; design extension points)

**InterviewKind** (wire names recommended PascalCase in C#; JSON camelCase via existing serializer — **open decision** if FE prefers snake):

- `ScreeningAndMotivation`
- `BehavioralAndCulture`
- `Recruiter`
- `HiringManager`
- `English`
- `General`

**DeliveryMode:**

- `Text`
- `RealisticSimulation`
- `GuidedCoaching`

### Core aggregates / entities

| Entity | Responsibility |
|---|---|
| `InterviewPrepSession` | Root; ownership; kind/mode; session status; snapshot refs; concurrency token; pause metadata |
| `InterviewPrepStage` | Ordered stages within session; stage status; agenda slot |
| `InterviewPrepTurn` | Question/answer pair (or system turn); signatures for Loop Guard; sequence |
| `InterviewPrepSnapshot` | Immutable CV + optional job JSON (+ metadata) — may be columns on session in M1 |
| `InterviewPrepEvidenceItem` | (post-M1) ledger entries linking claims ↔ turns |
| `InterviewPrepAssessment` | (post-M1) stage/session scores |
| `InterviewPrepCoverage` | (post-M1) competency coverage map |
| `InterviewPrepPlan` / `Brief` | (post-M1) AI-proposed, user/app-accepted artifacts |

### Milestone 1 minimal model

M1 persists Session + Stage + Turn + Snapshot fields enough to:

- Create session (kind, mode, optional scrapeResultId)
- Run prepare → ready (capture snapshots; build fixed stage plan)
- Start / pause / resume / complete / cancel
- Drive a **fixed-question test interview** (deterministic question bank per kind; no Gemini required)
- Enforce state machines + ownership + optimistic concurrency + idempotent commands

### Identity & tenancy

- `UserId` required FK → `Users`, cascade delete with user.
- All queries `UserId == currentUser.Id`.
- No shared/null-user rows (ADR-0010).

---

## 6. Proposed session and stage state machines

### Session states

`Created` → `Preparing` → `Ready` → `InProgress` ⇄ `Paused` → `Completing` → `Completed`

Also: `Cancelled` (from non-terminal except Completed/Failed), `Failed` (prepare/runtime hard failure).

| From | To | Trigger |
|---|---|---|
| Created | Preparing | `Prepare` / auto on create |
| Preparing | Ready | Snapshots + stage plan committed |
| Preparing | Failed | Unrecoverable prepare error |
| Ready | InProgress | `Start` |
| InProgress | Paused | `Pause` |
| Paused | InProgress | `Resume` |
| InProgress | Completing | Last stage closing / `Complete` |
| Completing | Completed | Assessments sealed (M1: may no-op seal) |
| * | Cancelled | `Cancel` if not Completed/Failed |
| InProgress/Preparing | Failed | Fatal error |

Illegal transitions → **409 Conflict** with stable error code.

### Stage states

`Planned` → `Opening` → `WarmUp` → `CoreAssessment` → `CandidateQuestions` → `Closing` → `AssessmentPending` → `Assessed` → `Completed`

M1 may collapse unused middle states for the fixed-question path (still store enum; transitions may skip WarmUp/CandidateQuestions when plan says so — **skip must be explicit in plan**, not ad-hoc).

Only one stage `Active` (in Opening…Closing) per session while `InProgress`.

### Turns

- Append-only within a stage; monotone `Sequence`.
- Roles: `System` | `Interviewer` | `Candidate` | `Coach` (Coach unused in M1 fixed path).
- Pause freezes accepting candidate turns; resume continues same stage.

---

## 7. Proposed Interview Loop Guard

Goal: prevent repetitive / stuck questioning. Evolves historical ADR-0017 into a first-class domain service.

### Components

1. **Question signature** — deterministic normalize: trim, collapse whitespace, lowercase, strip punctuation noise; optional stem hash; store `Signature` on each interviewer turn.
2. **Layered duplicate detection**
   - L1 Exact signature match against session (and optionally stage) history.
   - L2 Near-duplicate: token Jaccard / normalized Levenshtein threshold (config).
   - L3 Theme/competency repeat: same coverage tag beyond `MaxConsecutiveSameCompetency`.
3. **Configurable limits** (`InterviewPrep:LoopGuard` options): max exact retries, near-dup threshold, max questions per competency, max session turns, cooldown.
4. **Deterministic fallback** — if AI (later) or generator proposes a duplicate: regenerate with blocklist; after N failures, pick next unused fixed-bank question; if bank exhausted, force stage advance / closing turn. **Never** silently re-ask the same signature.

### M1 behavior

- Fixed-question bank already unique by construction; Loop Guard still runs as the enforcement seam (unit-tested) so later AI path plugs in without redesign.

### Ownership

- Pure domain service `InterviewLoopGuard` (no I/O); application service applies decisions before persist.

---

## 8. Proposed database changes

Assume MVP tables dropped via `20260805120000_DropInterviewPrepTables` (or equivalent) before v2 create migration.

### New tables (recommended)

**InterviewPrepSessions**

| Column | Notes |
|---|---|
| Id | Guid PK |
| UserId | FK Users CASCADE |
| ScrapeResultId | nullable FK ScrapeResults SET NULL |
| InterviewKind | nvarchar(64) |
| DeliveryMode | nvarchar(32) |
| Status | nvarchar(32) |
| CvDocumentId | nullable Guid (informational) |
| CvSnapshotJson | nvarchar(max) |
| JobSnapshotJson | nvarchar(max) nullable |
| JobTitle / CompanyName | denormalized list display |
| CatalogVersion | nvarchar(32) nullable |
| RowVersion | `rowversion` concurrency token (**preferred**) |
| IdempotencyPrepareKey | nvarchar(64) nullable unique per user |
| CreatedAt / UpdatedAt / PreparedAt / StartedAt / CompletedAt / CancelledAt | DateTimeOffset |
| FailureReason | nvarchar(1024) nullable |

**InterviewPrepStages**

| Column | Notes |
|---|---|
| Id | Guid PK |
| SessionId | FK CASCADE |
| SortOrder | int |
| StageType | nvarchar(64) (e.g. ScreeningBlock) |
| Status | nvarchar(32) |
| PlanJson | nvarchar(max) nullable — fixed questions for M1 |
| CreatedAt / UpdatedAt / CompletedAt | |

Unique `(SessionId, SortOrder)`.

**InterviewPrepTurns**

| Column | Notes |
|---|---|
| Id | Guid PK |
| SessionId | FK CASCADE |
| StageId | FK CASCADE |
| Sequence | int |
| Role | nvarchar(16) |
| Text | nvarchar(max) |
| QuestionSignature | nvarchar(128) nullable |
| CompetencyTag | nvarchar(64) nullable |
| ClientTurnId | nvarchar(64) nullable — idempotency |
| CreatedAt | |

Unique `(SessionId, Sequence)`; unique `(SessionId, ClientTurnId)` filtered where not null.

### Indexes

- `(UserId, UpdatedAt DESC)` on sessions
- `(SessionId, SortOrder)` stages
- `(SessionId, QuestionSignature)` for Loop Guard lookups

### Non-goals for schema

- Do not add Candidate/CV/Job duplicate tables.
- Do not store Gemini raw HTTP logs in relational tables by default.

---

## 9. Proposed internal and public contracts

### Public REST (proposed) — `/api/interview-prep`

All `[Authorize]`; tenancy enforced in service.

| Method | Path | Purpose | M1 |
|---|---|---|---|
| POST | `/sessions` | Create (`interviewKind`, `deliveryMode`, optional `scrapeResultId`, optional `idempotencyKey`) | Yes |
| GET | `/sessions` | List summaries | Yes |
| GET | `/sessions/{id}` | Detail + stages + turns | Yes |
| POST | `/sessions/{id}/prepare` | Preparing→Ready (idempotent) | Yes |
| POST | `/sessions/{id}/start` | Ready→InProgress | Yes |
| POST | `/sessions/{id}/pause` | Pause | Yes |
| POST | `/sessions/{id}/resume` | Resume | Yes |
| POST | `/sessions/{id}/cancel` | Cancel | Yes |
| POST | `/sessions/{id}/complete` | Completing→Completed | Yes |
| POST | `/sessions/{id}/turns` | Submit candidate answer / advance fixed interview (`clientTurnId` required) | Yes |
| DELETE | `/sessions/{id}` | Hard delete owner session | Yes |
| GET | `/sessions/{id}/report` | Report | Later |
| POST | `/sessions/{id}/ai/*` | Explicit AI operations | Later |

**Errors (align with existing style):** 400 validation; 401 auth; 404 miss/foreign; 409 illegal state / concurrency / duplicate idempotency conflict; 429 if rate-limited (recommend dedicated AI policy later).

**Concurrency:** clients send `If-Match` ETag derived from `RowVersion` **or** body `expectedUpdatedAt` — **open decision** (§11). Server returns 409 on mismatch.

**Idempotency:**

- Create/prepare: optional key stored on session.
- Turns: `clientTurnId` unique per session; replay returns prior result.

### Internal ports

```csharp
IInterviewPrepSessionService          // application API used by controller
IInterviewPrepCandidateContextAdapter // → CvStructuredDocumentDto snapshot
IInterviewPrepJobContextAdapter       // → SavedScrapeResult snapshot
IInterviewLoopGuard
IInterviewPrepQuestionBank            // M1 fixed questions
IInterviewPrepAiGateway               // later; no-op/null in M1
```

### Contract registry

- Add **proposed** `interview-prep-v2-sessions` after approval; do not revive MVP contract ids.

---

## 10. Files Milestone 1 will add or modify

### Add

- `api/ApplyVault.Api/Controllers/InterviewPrepController.cs`
- `api/ApplyVault.Api/Models/InterviewPrep/*.cs` (requests/responses/error codes)
- `api/ApplyVault.Api/Data/InterviewPrepSessionEntity.cs` (session/stage/turn entities — split files OK)
- `api/ApplyVault.Api/Services/InterviewPrep/**` (domain state machines, Loop Guard, adapters, session service, fixed question bank)
- `api/ApplyVault.Api/Options/InterviewPrepOptions.cs` (+ LoopGuard subsection)
- `api/ApplyVault.Api/Migrations/<timestamp>_AddInterviewPrepV2.cs` (+ Designer/snapshot updates)
- `api/ApplyVault.Api.Tests/InterviewPrep/*` (state machine, Loop Guard, service unit tests)
- `api/ApplyVault.Api.IntegrationTests/InterviewPrepLifecycleIntegrationTests.cs` (create→prepare→start→turns→pause→resume→complete; tenancy 404)

### Modify

- `api/ApplyVault.Api/Data/ApplyVaultDbContext.cs` — DbSets + Fluent config
- `api/ApplyVault.Api/Data/ScrapeResultEntity.cs` (or AppUser) — optional navigation collection **only if** needed; prefer no navigation clutter
- `api/ApplyVault.Api/Infrastructure/ServiceCollectionExtensions.cs` — options + DI registrations
- `api/ApplyVault.Api/appsettings.example.json` — `InterviewPrep` section (no secrets)
- `api/ApplyVault.Api/Migrations/ApplyVaultDbContextModelSnapshot.cs`
- After approval: `agent-system/governance/contract-registry.yaml`, `ownership-matrix.md`
- Principal: new `docs/adr/NNNN-interview-prep-v2-*.md` superseding 0012–0019

### Explicitly not in M1

- Frontend files
- Live Gemini Interview Prep client (stub/port only)
- Evidence ledger / coverage / report endpoints
- Background worker for prepare (default: sync prepare in request; see §11)

### Milestone sequencing (beyond M1)

| Milestone | Focus |
|---|---|
| M1 | Domain + lifecycle + snapshots + fixed-question interview + tests |
| M2 | AI gateway (ADR-0008 client), briefs/plans propose-accept |
| M3 | Adaptive questioning + Loop Guard AI path |
| M4 | Assessment + evidence ledger + coverage |
| M5 | Coaching aids + session report |

---

## 11. Decisions that require human input

1. **Wire enum format** for `interviewKind` / `deliveryMode` / statuses: PascalCase JSON (`screeningAndMotivation`) vs explicit snake (`screening_and_motivation`). Recommendation: camelCase Pascal enum names via default serializer (`screeningAndMotivation`).
2. **Concurrency API:** `RowVersion` + `ETag`/`If-Match` vs `expectedUpdatedAt` in body. Recommendation: SQL `rowversion` + ETag.
3. **Snapshot freshness:** always snapshot at Prepare only, vs optional “refresh snapshots” before Start. Recommendation: Prepare-only immutability; new session to pick up CV edits.
4. **Prepare execution:** synchronous in `POST .../prepare` vs background `Preparing` + poll. Recommendation: **sync** for M1 (CV/scrape reads are local); revisit if AI briefing enters prepare.
5. **Retention:** hard delete only (MVP-like) vs soft-delete / TTL. Recommendation: hard delete by owner; no TTL in M1.
6. **M1 public surface:** full lifecycle routes now vs internal-only fixed interview behind feature flag. Recommendation: ship lifecycle routes; gate AI later with options.
7. **DeliveryMode in M1:** accept all three enum values but only implement `Text` behavior, vs reject non-Text. Recommendation: accept all; behave as Text until later milestones.
8. **Supersession ADR packaging:** single ADR “Interview Prep v2” superseding 0012–0019, vs one ADR per concern. Recommendation: **one primary ADR** + optional Loop Guard ADR if detail warrants.
9. **Rate limiting:** new `PolicyInterviewPrep` now in M1 vs defer. Recommendation: add light per-user policy on turn/prepare endpoints in M1.
10. **Deep-link param name:** keep `jobId` = scrape GUID for FE continuity vs rename to `scrapeResultId`. Recommendation: API uses `scrapeResultId`; FE may keep `jobId` query alias later.

---

## Ownership recommendations (detail)

| Area | Primary | Secondary |
|---|---|---|
| Interview Prep v2 API/EF/domain | backend-engineer | architecture-engineer (boundaries) |
| Gemini Interview Prep clients/prompts | ai-llm-engineer | backend-engineer |
| Integration test evidence | qa-engineer | backend-engineer |
| ADRs / CONTEXT vocabulary | principal-software-architect | architecture-engineer |
| Angular UI (later) | frontend-engineer | ui-ux-designer |

Add an ownership-matrix row after approval: `Interview Prep v2 (api)` → backend-engineer.

---

## Status

**READY** — design complete for human approval.  
**Next action:** Human approves/amends §11 decisions → Principal authors supersession ADR(s) → delegate Milestone 1 to `backend-engineer` (skills: to-spec → to-tickets → implement/tdd).  
**Blockers:** none for design; implementation blocked on approval gate.
