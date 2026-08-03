# Interview Prep — frozen contracts (IP-01 / M0)

**Status:** FROZEN (ADR-0012 Accepted, 2026-08-02)  
**Issue:** [#67](https://github.com/PrimusInterParess/ApplyVault/issues/67)  
**ADR:** `docs/adr/0012-interview-prep-ephemeral-profession-agnostic.md`  
**Plan:** `agent-system/implementation-plan-interview-prep.md`  
**Canonical path:** `agent-system/handoffs/archive/interview-prep-ip-01-2026-08-02/frozen-contracts.md`  
**Audience:** M1 `backend-engineer` + `ai-llm-engineer` (then M2+ frontend)

Deltas from archive `architecture-design.md` §3 sketch are called out in §8.

---

## 1. Endpoints (MVP)

| Operation | Method / path | Persistence | Notes |
|---|---|---|---|
| Coach turn | `POST /api/interview-prep/turns` | None | Sole coach endpoint |
| Session create | — | — | **Not in MVP** |
| Session end / score-only | — | — | **Not in MVP** — scorecard returns on turn when model scores |
| Session history CRUD | — | — | Later IP-13 |

**Auth:** `[Authorize]` + `IAppUserService.GetRequiredUserAsync` (ADR-0009 / ADR-0010).  
**Controller:** `InterviewPrepController` — route prefix `api/interview-prep`.  
**Do not** nest under `CvDocumentsController`.

---

## 2. Mode catalog

Wire enum / JSON string ids (case-sensitive, snake_case):

| Mode id | Display label (UI) | Intent |
|---|---|---|
| `screening` | Screening / motivation | Why this role/org; career story; motivation |
| `behavioral` | Behavioral / culture | Competency / values / collaboration stories |
| `role_domain` | Role & domain depth | Domain craft for the inferred profession (not coding-by-default) |
| `problem_solving` | Problem-solving / case | Structured reasoning on realistic cases for that profession |
| `process_systems` | Process & systems | How work gets done end-to-end; **technical system design only when `inference.isTechnicalContext` is true** |
| `language_practice` | Language practice (EN / DA) | Spoken/written interview fluency; honor `languageMix` |
| `full_loop` | Full loop | Multi-round sequence stitching the above (**IP-12**; id in contract now; orchestration may ship later) |

**Rejected archive ids (do not use):** `practice`, `technical`, `mixed` (as mode).

---

## 3. `languageMix`

| Value | Meaning |
|---|---|
| `en` | English interview practice |
| `da` | Danish interview practice |
| `mixed` | Alternate / mix English and Danish as appropriate |

**Frozen:** use `mixed`, **not** `en+da`.  
Default when omitted: server options `InterviewPrepAi:DefaultLanguageMix` (recommended `en`).

---

## 3b. `hiringMarket` (ADR-0013)

| Value | Meaning |
|---|---|
| `general` | Market-agnostic coaching (unless DK job signal or languageMix da/mixed) |
| `dk` | Explicit Danish hiring-market coaching bias; independent of languageMix |

Default when omitted: `general` (or `InterviewPrepAi:DefaultHiringMarket`).  
**Orthogonal to languageMix** — `en` + `dk` is English practice with Danish-market cues.

---

## 4. Request — `POST /api/interview-prep/turns`

```json
{
  "mode": "behavioral",
  "languageMix": "en",
  "hiringMarket": "general",
  "userMessage": "Let's start.",
  "scrapeResultId": null,
  "priorTurns": [
    { "role": "user", "text": "…", "phase": "interview" },
    { "role": "coach", "text": "…", "phase": "interview" }
  ]
}
```

| Field | Type | Required | Rules |
|---|---|---|---|
| `mode` | string enum | yes | One of §2 mode ids |
| `languageMix` | string enum | no | `en` \| `da` \| `mixed`; default from options |
| `hiringMarket` | string enum | no | `general` \| `dk`; default `general` (ADR-0013) |
| `userMessage` | string | yes | Non-empty after trim; server-capped (`MaxUserMessageChars`) |
| `scrapeResultId` | guid \| null | no | When set, server loads owned scrape; never trust client job text |
| `priorTurns` | array | no | Client-held history; server truncates to `MaxPriorTurns` / per-turn char caps |

### `priorTurns[]` item

| Field | Type | Required | Values |
|---|---|---|---|
| `role` | string | yes | `user` \| `coach` |
| `text` | string | yes | Truncated server-side if over cap |
| `phase` | string | no | `interview` \| `debrief` (default `interview`) |

### Deep-link (FE only)

| UI query | API field |
|---|---|
| `/interview-prep?jobId=<scrapeResultGuid>` | `scrapeResultId` |

---

## 5. Response — `200 OK`

```json
{
  "phase": "interview",
  "inference": {
    "role": "Pediatric nurse",
    "seniority": "mid",
    "interviewStyle": "competency_behavioral",
    "isTechnicalContext": false
  },
  "coachMessage": "…",
  "scorecard": null,
  "followUps": [],
  "debriefBullets": [],
  "modelAnswer": "In my last role I…"
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `phase` | string | yes | `interview` \| `debrief` |
| `inference` | object | yes | Always populated after a successful AI turn |
| `inference.role` | string | yes | Free-text role; **must not** default to software engineer |
| `inference.seniority` | string | yes | Free-text or short token (e.g. `junior` / `mid` / `senior` / `lead` / `unknown`) |
| `inference.interviewStyle` | string | yes | Free-text style hint for coach/UI (e.g. screening, competency, case) |
| `inference.isTechnicalContext` | boolean | yes | Gates Process & systems technical specialization |
| `coachMessage` | string | yes | Coach reply for chat |
| `scorecard` | object \| null | no | Present when scoring an answer / ending a round; null on setup |
| `followUps` | string[] | yes | Normalize missing → `[]` |
| `debriefBullets` | string[] | yes | Normalize missing → `[]` |
| `modelAnswer` | string \| null | no | Additive (ADR-0015). Sample spoken answer for the current coach question; distinct from tip-style `followUps`. Prefer non-null in `interview` when a question is posed; **must be `null` in `debrief`**; may be `null` on setup / unused. Normalize missing / whitespace-only → `null`. Not included in `priorTurns`. |

### Scorecard object

| Field | Type | Required | Notes |
|---|---|---|---|
| `overall` | number | yes | Integer 0–100 |
| `summary` | string | no | Short narrative feedback for the round |
| `dimensions` | array | yes | Exactly the five fixed ids below (order stable) |

### Scorecard dimension ids (fixed)

| Dimension id | Meaning |
|---|---|
| `clarity` | Clear, understandable answers |
| `evidence` | Concrete examples / proof from CV or experience |
| `structure` | Organized response (e.g. STAR-like where appropriate) |
| `role_fit` | Relevance to inferred role / optional job |
| `language` | Fluency / appropriateness for `languageMix` |

### Dimension item

| Field | Type | Required |
|---|---|---|
| `id` | string | yes — one of the five ids |
| `score` | number | yes — integer 0–100 |
| `note` | string | yes — model-written note (may be short) |

Model must not invent additional dimension ids in MVP; server normalizes unknown ids out / maps to fixed set in `GoogleAiInterviewPrepClient` normalization.

---

## 6. Server load rules (each turn)

1. Resolve AppUser (JWT).
2. `ICvStructuredDocumentService.GetStructuredAsync(user)` — required.
3. If `scrapeResultId` set → `IScrapeResultStore.GetByIdAsync(id, user.Id)`; inject company/title/location/summary/`jobDescription` into AI context.
4. Call `IInterviewPrepAiClient` / `GoogleAiInterviewPrepClient` with Structured CV ± job + request.
5. **Never** `SaveStructuredAsync`; **never** mutate scrape rows.

Job context fields (read-only from store): company, title, location, position summary, `jobDescription` Markdown (requirements live in description — no new column).

---

## 7. Error mapping

Mirror CV evaluation / GoogleAi patterns (`KeyNotFoundException` → 404, `InvalidOperationException` → 400):

| Condition | HTTP | Typical message family |
|---|---|---|
| Missing / no Structured CV | `404` | Structured CV not found |
| Structured CV present but empty / unusable sections | `400` | Empty or invalid Structured CV for prep |
| Unknown / foreign / other-user `scrapeResultId` | `404` | Not found (do not leak existence across tenants) |
| Invalid `mode` / `languageMix` / empty `userMessage` | `400` | Validation |
| `GoogleAi:Enabled=false` or missing ApiKey/Model | `400` | AI unavailable (clear, non-secret message) |
| Gemini / parse / schema failure | `400` | Prefer existing InvalidOperation → BadRequest pattern used by other GoogleAi clients |
| Unauthorized / missing JWT | `401` | Framework default |

**Out of contract MVP:** 403 for foreign scrape (use **404**), SSE/streaming, session CRUD, CV mutation endpoints.

---

## 8. Deltas vs archive design sketch

| Archive sketch | Frozen (this doc + ADR-0012) |
|---|---|
| Modes `practice` \| `behavioral` \| `technical` \| `mixed` | Role-agnostic catalog §2 |
| `languageMix` `en+da` allowed | **`mixed` only** |
| Top-level `inferredRole` string | Structured `inference` object (role, seniority, interviewStyle, isTechnicalContext) |
| Scorecard dims `clarity` / `relevance` / `depth` | Fixed `clarity`, `evidence`, `structure`, `role_fit`, `language` + notes |
| Endpoint path | Unchanged: `POST /api/interview-prep/turns` |
| Ephemeral / no sessions table | Unchanged (locked) |

---

## 9. Options sketch — `InterviewPrepAi`

Bind `InterviewPrepAiOptions` beside other `*AiOptions` (ADR-0008). Shared `GoogleAi` owns Enabled / ApiKey / Model / TimeoutSeconds.

| Property | Purpose |
|---|---|
| `SystemPrompt` | Profession-agnostic coach rules (ADR-0012 § Decision 2) |
| `UserPromptTemplate` | Placeholders for mode, languageMix, CV JSON, job JSON, priorTurns, userMessage |
| `MaxPriorTurns` | e.g. 12 — truncate oldest |
| `MaxUserMessageChars` / `MaxPriorTurnChars` | Prompt budget |
| `DefaultLanguageMix` | e.g. `en` |
| `TimeoutSeconds` (optional) | Override falling back to `GoogleAi:TimeoutSeconds` |

---

## 10. Open product questions (resolved defaults)

| Question | Locked default |
|---|---|
| Ephemeral MVP (no session DB)? | **Yes** |
| Ship IP-12 full loop in first cut? | **May slip** — mode id still in contract |
| Scorecard: fixed dims vs model criteria? | **Fixed ids + model notes** |
| Deep-link param? | **`jobId`** → `scrapeResultId` |
| `languageMix` `mixed` vs `en+da`? | **`mixed`** |
| IP-15 vs saved-job notes (#44)? | Deferred (Later) |

---

## 11. Non-goals (do not implement under this contract)

- Durable sessions / multi-device resume
- Streaming coach replies
- Mutating Structured CV or scrape from prep
- New scrape `requirements` column
- Extension UI; new LLM vendor / Gemini SDK
- Voice/video; payments
