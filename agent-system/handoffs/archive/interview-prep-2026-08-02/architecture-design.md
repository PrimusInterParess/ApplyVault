# Interview Prep — Architecture Design

**Task:** `interview-prep-2026-08-02`  
**Agent:** `architecture-engineer`  
**Mode:** BROWNFIELD / BRIDGE — design only (no application code)  
**Status:** COMPLETE  

## Architecture design summary

- **Request:** new-feature-shape (profession-agnostic Interview Prep coach)
- **Status:** COMPLETE
- **Recommendation:** Add a dedicated ephemeral Gemini coach turn API (`POST /api/interview-prep/turns`) that always loads Structured CV, optionally resolves a owned scrape job, and returns structured coach JSON; Angular `/interview-prep` shell route with client-held turn history; **no sessions table for MVP**.

---

## 1. Current state (evidence)

### Auth / tenancy

| Seam | Path / evidence |
| --- | --- |
| Supabase JWT + local AppUser | ADR-0009; `ConfigureSupabaseJwtBearerOptions`; controllers call `IAppUserService.GetRequiredUserAsync` |
| Strict per-user isolation | ADR-0010; scrape/CV queries scoped by `user.Id` |

### Structured CV (always-required context)

| Seam | Path / evidence |
| --- | --- |
| Read current Structured CV | `GET /api/cv-documents/current/structured` → `CvDocumentsController.GetStructured` |
| Service | `ICvStructuredDocumentService` / `CvStructuredDocumentService` (`Services/CvDocuments/CvStructuredDocumentService.cs`) |
| Catalog / schema | ADR-0001; `shared/cv-section-catalog/` |

### Ephemeral Gemini AI pattern (siblings)

| Feature | Endpoint | Persistence | Options | Client |
| --- | --- | --- | --- | --- |
| Quality evaluation | `POST .../structured/ai-evaluation` | None (ADR-0007) | `CvEvaluationAiOptions` | `GoogleAiCvStructuredEvaluationClient` |
| Summary propose | `POST .../structured/ai-summary-propose` | None (ADR-0004) | `CvSummaryProposeAiOptions` | `GoogleAiCvStructuredSummaryProposeClient` |
| Update propose | `POST .../structured/ai-update-propose` | None (ADR-0011) | (update options) | `GoogleAiCvStructuredUpdateClient` path via propose service |

Shared Gemini rules (ADR-0008):

- Raw HTTP to `generativelanguage.googleapis.com` via `GoogleAi*` + `HttpClient`
- Gate on `GoogleAi:Enabled` + ApiKey/Model
- Per-feature prompt options + JSON `responseSchema`
- DI: `ServiceCollectionExtensions` binds `*AiOptions` and `AddHttpClient<I*, GoogleAi*>`

Evaluation service shape to mirror for orchestration:

```
CvStructuredEvaluationService
  → GetStructuredAsync(user)
  → evaluationAiClient.EvaluateAsync(structured, …)
  → never SaveStructuredAsync
```

### Saved job context (optional)

| Seam | Path / evidence |
| --- | --- |
| Controller | `ScrapeResultsController` — `[Authorize]`, route `api/scrape-results` |
| Get by id (tenant-scoped) | `GET /api/scrape-results/{id}` → `IScrapeResultStore.GetByIdAsync(id, user.Id)` |
| Job fields | `JobDetailsDto`: `JobTitle`, `CompanyName`, `Location`, `JobDescription`, `PositionSummary` (`Models/ScrapeContracts.cs`) |
| Requirements | Live inside Markdown `JobDescription` (scrape enrichment prompt already formats requirements as bullets in description). **No separate requirements column exists** — do not add for MVP. |

Effective values after capture review already flow through store mapping (`EfCoreScrapeResultStore` resolve override → effective company/title/location/description).

### Frontend shell / routes

| Seam | Path / evidence |
| --- | --- |
| Routes under auth shell | `frontend/applyvault-jobs-ui/src/app/app.routes.ts` — children: `jobs`, `search`, `cv-builder`, `cv-projects`, `settings` |
| Primary nav | `shared/layout/app-shell.component.html` — Jobs / Search / CV Builder / Projects / Settings |
| CV AI HTTP client pattern | `features/cv-projects/data-access/cv-document-api.service.ts` (`ai-evaluation`, `ai-summary-propose`, …) |
| Job detail surface | `features/job-results/…/job-result-detail` — interview **calendar** editors only today; **no Interview Prep entry** yet |

### Ownership (governance)

| Concern | Primary | Secondary |
| --- | --- | --- |
| `api/ApplyVault.Api/` | backend-engineer | ai-llm-engineer, platform-engineer, qa-engineer |
| Gemini / GoogleAi* | ai-llm-engineer | backend-engineer |
| Angular UI | frontend-engineer | ui-ux-designer, qa-engineer |

---

## 2. Target state

### Product shape (MVP)

1. Authenticated user opens `/interview-prep` (shell nav) or deep-links `/interview-prep?jobId=<scrapeResultGuid>` from a saved job.
2. Page ensures Structured CV exists (reuse `GET current/structured`; empty → clear empty-state CTA to CV Builder — do not invent CV).
3. Optional job context: if `jobId` present, FE may show job chips; **server always re-loads** scrape by id + AppUser tenancy on each coach turn (never trust client-supplied job text as sole source of truth).
4. Multi-turn coach: FE holds turns in memory; each request sends `mode`, `languageMix`, compact `priorTurns` summary, and `userMessage`.
5. Response JSON drives UI: `phase` (`interview` \| `debrief`), `inferredRole`, scorecard / feedback fields, coach reply — **never mutates Structured CV**.
6. Refresh loses history (same product trade-off as ADR-0007 evaluation).

### Recommended module boundaries

```
api/ApplyVault.Api/
  Controllers/InterviewPrepController.cs          # NEW — thin Authorize controller
  Services/InterviewPrep/
    IInterviewPrepService.cs                      # NEW — load CV ± job, call AI, normalize
    InterviewPrepService.cs
    InterviewPrepAiContracts.cs                   # IInterviewPrepAiClient + DTOs
    GoogleAiInterviewPrepClient.cs                # NEW — ADR-0008 HTTP client
    GoogleAiInterviewPrepResponseSchema.cs        # NEW — structured JSON schema
  Options/InterviewPrepAiOptions.cs               # NEW — SystemPrompt + UserPromptTemplate (+ caps)

frontend/applyvault-jobs-ui/src/app/features/interview-prep/
  pages/interview-prep-page/                      # NEW route component
  data-access/interview-prep-api.service.ts
  data-access/interview-prep.facade.ts            # ephemeral turn state
  models/…
```

**Do not** hang the coach endpoint under `CvDocumentsController` AI siblings: Interview Prep is a product surface (job±CV coach), not a Structured CV mutate/propose path. Reuse **services** (`ICvStructuredDocumentService`, `IScrapeResultStore`), not the CV documents route prefix.

### Target sequence

```mermaid
sequenceDiagram
  participant UI as Angular /interview-prep
  participant API as InterviewPrepController
  participant Auth as AppUserService
  participant CV as ICvStructuredDocumentService
  participant Jobs as IScrapeResultStore
  participant AI as GoogleAiInterviewPrepClient
  participant G as Gemini HTTP

  UI->>API: POST /api/interview-prep/turns (JWT, mode, languageMix, priorTurns, userMessage, scrapeResultId?)
  API->>Auth: GetRequiredUserAsync
  API->>CV: GetStructuredAsync(user)
  alt no structured / empty sections
    API-->>UI: 404 / 400 (same family as evaluation)
  end
  opt scrapeResultId provided
    API->>Jobs: GetByIdAsync(id, user.Id)
    alt missing / other user
      API-->>UI: 404
    end
  end
  API->>AI: CoachTurnAsync(structured, jobContext?, request)
  AI->>G: generateContent (InterviewPrepAiOptions + responseSchema)
  G-->>AI: JSON
  AI-->>API: normalized InterviewPrepTurnResponse
  API-->>UI: 200 JSON (phase, inferredRole, scorecard, …)
  Note over UI: Append to in-memory turns; never PUT structured CV
```

---

## 3. API contract sketch (`proposed`)

### `POST /api/interview-prep/turns`

**Auth:** `[Authorize]` + `GetRequiredUserAsync` (ADR-0009 / ADR-0010).  
**Persistence:** none.

#### Request (JSON)

```json
{
  "mode": "practice",
  "languageMix": "en",
  "userMessage": "Let's start.",
  "scrapeResultId": "optional-guid-or-null",
  "priorTurns": [
    { "role": "user", "text": "…", "phase": "interview" },
    { "role": "coach", "text": "…", "phase": "interview" }
  ]
}
```

| Field | Notes |
| --- | --- |
| `mode` | Enum string MVP: `practice` \| `behavioral` \| `technical` \| `mixed` (exact set `ARCHITECT_PROPOSED` — product may trim). Mode guides coaching style, **not** a hardcoded profession. |
| `languageMix` | BCP-47-ish or simple codes (`en`, `da`, `en+da`) — prompt must honor; defaults from options. |
| `userMessage` | Required non-empty; length-capped server-side. |
| `scrapeResultId` | Optional; when set, server loads owned scrape and injects job context. |
| `priorTurns` | Client-held history (or a compact summary string — prefer structured list with hard max turns / chars). Server truncates to configured caps before prompt. |

#### Response (JSON) — `proposed` shape

```json
{
  "phase": "interview",
  "inferredRole": "Pediatric nurse (inferred from CV + job)",
  "coachMessage": "…",
  "scorecard": {
    "overall": 72,
    "dimensions": [
      { "id": "clarity", "score": 70, "note": "…" },
      { "id": "relevance", "score": 75, "note": "…" },
      { "id": "depth", "score": 68, "note": "…" }
    ]
  },
  "followUps": ["…"],
  "debriefBullets": []
}
```

| Field | Notes |
| --- | --- |
| `phase` | `interview` \| `debrief` |
| `inferredRole` | Free-text role inferred from CV±job — **must not** default to software engineer |
| `scorecard` | Present when scoring an answer; may be null on pure setup turns |
| `followUps` / `debriefBullets` | Optional arrays; normalize empties |

Error mapping (mirror evaluation):

- No Structured CV → `404` / `KeyNotFoundException`
- Empty sections → `400`
- AI disabled / missing key → `400` with clear message
- Unknown / foreign `scrapeResultId` → `404`
- Gemini failure → `400` or `502` family consistent with other GoogleAi clients (prefer existing InvalidOperation → BadRequest pattern)

**Out of contract for MVP:** session CRUD, SSE/streaming, CV mutation, new scrape columns.

---

## 4. Options / config sketch — `InterviewPrepAiOptions`

Section name: `InterviewPrepAi` (bind beside `CvEvaluationAi` in `ServiceCollectionExtensions`).

| Property | Purpose |
| --- | --- |
| `SystemPrompt` | Profession-agnostic interview coach. **Must** instruct: adapt to ANY profession from CV±job; never assume developer/coding interviews; ground questions in provided facts; do not invent employers/degrees/metrics; do not rewrite or return a mutated CV; return JSON only. |
| `UserPromptTemplate` | Placeholders e.g. `{{mode}}`, `{{languageMix}}`, `{{inferredContextHint}}`, `{{structuredCvJson}}`, `{{jobContextJson}}`, `{{priorTurnsJson}}`, `{{userMessage}}` |
| `MaxPriorTurns` | Hard cap (e.g. 12) — truncate oldest |
| `MaxUserMessageChars` / `MaxPriorTurnChars` | Prompt budget guards |
| `DefaultLanguageMix` | e.g. `en` |

Shared `GoogleAiOptions` still owns Enabled / ApiKey / Model / TimeoutSeconds (ADR-0008).  
Timeout: Interview Prep multi-turn prompts may be larger than evaluation — **ARCHITECT_PROPOSED:** allow optional `InterviewPrepAi:TimeoutSeconds` override falling back to `GoogleAi:TimeoutSeconds` (do not invent a second vendor timeout stack).

Default system prompt must explicitly forbid profession hardcoding (nursing, trades, product, academia, etc. equally valid). Label any sample question banks in comments as examples only — **not** in default prompt text as “ask about algorithms / system design”.

---

## 5. Persistence decision

| Option | Verdict |
| --- | --- |
| **A. Ephemeral turns (FE memory + priorTurns on request)** | **Recommended (MVP)** — matches ADR-0007 / propose siblings; zero migration; no CV side effects; simplest tenancy story. |
| B. New `InterviewPrepSessions` + turns tables | **Rejected for MVP** — unjustified store, GDPR retention, sync UX. Document as **v2 session history** later. |
| C. Redis session cache | **Rejected for MVP** — optional Redis is platform concern; adds ops without product requirement. |

**v2 (explicit later):** durable sessions keyed by `AppUserId` + optional `ScrapeResultId`, list/resume UI — requires separate ADR when product asks.

---

## 6. Frontend shape

1. Route child under auth shell: `path: 'interview-prep'` + `shellSubtitle` in `app.routes.ts`.
2. Nav link in `app-shell.component.html` (and optionally footer — product/UI call).
3. Query `jobId` → map to API `scrapeResultId`; resolve display via existing job-results facade/API `GET scrape-results/{id}` or props from navigation state (server still re-validates on turn).
4. Job detail CTA: “Prepare for interview” → `routerLink="/interview-prep" [queryParams]="{ jobId: selectedJob.id }"`.
5. Chat UI holds `priorTurns`; clear on leave/refresh; empty CV state links to `/cv-builder`.
6. Do not call structured PUT/ai-update from this feature.

UI polish ownership: `ui-ux-designer` for coach layout; `frontend-engineer` implements.

---

## 7. Milestone breakdown

| ID | Milestone | Owning agents | Outcome |
| --- | --- | --- | --- |
| **M0** | Lock plan + ADR | Principal Architect; architecture-engineer (done); product-manager acceptance | Human approves plan; ADR accepted or deferred with explicit note |
| **M1** | Backend coach turn API | **backend-engineer** (controller, service, DTOs, DI, scrape/CV load, tenancy, tests); **ai-llm-engineer** (GoogleAi client, response schema, profession-agnostic prompts, normalize) | `POST /api/interview-prep/turns` green with unit tests; GoogleAi disabled path fails clearly |
| **M2** | FE route + coach shell | **frontend-engineer**; **ui-ux-designer** (layout/copy) | `/interview-prep` + shell nav; ephemeral chat; CV empty-state |
| **M3** | Job deep-link entry | **frontend-engineer** | `?jobId=` + job-detail CTA; invalid job handled |
| **M4** | QA evidence | **qa-engineer** | Authz/tenancy, no CV mutation, AI-off, profession-agnostic smoke checklist |
| **M5** | Rate limit / ops knobs | **platform-engineer** (optional) | **ARCHITECT_PROPOSED:** dedicated AI turn rate-limit policy if GlobalApi insufficient — not blocking M1–M3 |

Suggested delivery: M0 → M1 → M2/M3 (can parallel after M1 contract freeze) → M4 → M5 if needed.

---

## 8. Options considered

| Option | Notes |
| --- | --- |
| **Recommended:** Dedicated `InterviewPrepController` + ephemeral Gemini client | Clear product boundary; reuses CV/scrape services; ADR-0008 compliant |
| Rejected: Nest under `CvDocumentsController` `…/ai-interview-prep` | Couples job coaching to CV document resource; muddies Assist semantics |
| Rejected: Persist sessions in MVP | Unneeded store; contradict brief preference |
| Rejected: New LLM vendor / Gemini SDK | Forbidden by ADR-0008 |
| Rejected: New `Requirements` scrape column | Requirements already in `JobDescription` Markdown |
| Rejected: Mutate Structured CV from coach | Explicit non-goal; Assist remains the edit path (ADR-0002 / propose-approve ADRs) |

---

## 9. Impacted contracts

| Contract | Change |
| --- | --- |
| `api-rest-controllers` | **proposed** additive: `InterviewPrepController` |
| `google-ai-gemini-http` | **proposed** additive: `GoogleAiInterviewPrepClient` + `InterviewPrepAiOptions` |
| `supabase-jwt` / tenancy | No change — reuse |
| `scrape-ingest` | No change — read-only use of stored results |
| `cv-section-catalog` / ADR-0001 | No schema change — read-only Structured CV payload |
| Extension | **Out of scope** (delegation exclude) |

Mark in `contract-registry.yaml` `proposed_contracts` during implementation; promote when landed.

---

## 10. Risks and open decisions

| Risk / decision | Severity | Mitigation |
| --- | --- | --- |
| Prompt accidentally biases to SWE interviews | High | ai-llm + ADR prompt rules; QA profession fixtures (nurse, teacher, trades, …) |
| Prompt / priorTurns token blow-ups | Medium | Caps on turns/chars; truncate CV payload if needed (`ARCHITECT_PROPOSED` slim serializer) |
| Client-forged job text | Medium | Server loads scrape by id + userId; ignore client job body |
| Multi-turn quality without durable memory | Low (accepted) | Document refresh loss; v2 sessions later |
| Scorecard dimension set unsettled | Low | Product picks MVP dimension ids; schema enum in responseSchema |
| Rate abuse of Gemini | Medium | GlobalApi today; M5 dedicated policy **ARCHITECT_PROPOSED** |
| Empty CV users | Low | Empty-state → CV Builder |

### ADR proposal need

**Yes.** Recommend new ADR:

**Title (proposed):** `Interview Prep coach turns are ephemeral and profession-agnostic`

**Why:** Same class of decision as ADR-0007 (ephemeral AI, no durable history) plus an explicit product constraint that prompts must adapt to any profession from CV±job and must not mutate Structured CV. Prevents silent addition of sessions tables or SWE-default prompts.

Do **not** overwrite ADR-0007/0008; cross-link them. Number = next free under `docs/adr/` at write time (do not reuse ids).

---

## 11. Explicit out of scope (MVP)

- Durable interview session history / multi-device resume
- Streaming / SSE coach replies
- Mutating Structured CV or Assist propose-approve from coach turns
- New scrape `requirements` column or JD matching as evaluation extension
- Browser extension surface
- New LLM vendor or Google AI SDK
- Voice / video interview simulation
- Calendar interview-event auto-scheduling from coach (existing job-results interview editors remain separate)
- Payments / premium gating
- Automatic creation of Structured CV when missing

---

## 12. Security / privacy

- JWT required; all loads scoped to AppUser (ADR-0009/0010).
- Scrape and CV payloads contain PII — stay server-side in Gemini request; do not log full prompts/ApiKey.
- Coach must not instruct client to POST structured PUT.
- No secrets in design artifacts or default appsettings committed keys.

---

## 13. Ownership recommendations

| Concern | Primary | Note |
| --- | --- | --- |
| Interview Prep API + DTOs + tenancy | backend-engineer | New controller under `api/` |
| Gemini client + prompts + schema | ai-llm-engineer | Ownership matrix already: Gemini / GoogleAi* |
| Angular feature + job CTA | frontend-engineer | New `features/interview-prep/` |
| Coach UX layout | ui-ux-designer | Collaborate early on M2 |
| Test evidence | qa-engineer | After M1–M3 |
| Rate-limit policy (if added) | platform-engineer | M5 optional |

Recommend ownership-matrix row when shipping: `Interview Prep (API+UI)` → backend + frontend co-primary by path (existing path-based matrix already covers).

---

## 14. Next actions for implementers

1. Principal: human approval of this plan (approval gate).
2. Author ADR (next free number) from §10 title once approved — Principal or architecture follow-up.
3. Delegate **M1** to backend-engineer + ai-llm-engineer with this design + frozen request/response sketch.
4. Delegate **M2/M3** to frontend-engineer (+ ui-ux) against frozen contract.
5. Delegate **M4** qa-engineer; **M5** only if load testing or abuse warrants.

---

## Validation (design)

- Current seams cited from repo (controllers, GoogleAi*, options DI, routes, shell, scrape JobDetailsDto).
- No application code changed.
- No invented providers; ADR-0008 respected.
- Persistence preference documented with rejected alternatives.
