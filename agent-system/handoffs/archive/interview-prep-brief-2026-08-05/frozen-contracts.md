# Frozen contracts — Interview Prep brief (v1)

**Task:** `interview-prep-brief-2026-08-05`  
**Agent:** architecture-engineer  
**Status:** ACCEPTED by Principal (2026-08-05 — operate C; U1–U4 defaults accepted)  
**Sources:** ADR-0025, CONTEXT.md (Interview Prep brief / Brief topic gap), implementation plan §4.4, session DTO patterns in `InterviewPrepContracts.cs` / `InterviewPrepEnums.cs`  
**Auth:** `[Authorize]` + Supabase JWT; ADR-0010 per-user tenancy on every route  
**Route prefix:** `/api/interview-prep` (same bounded module as sessions; ADR-0021 / ADR-0025)

---

## 0. Naming collision (CLR vs product)

| Concept | Product / CONTEXT | Existing CLR | New study-artifact CLR (recommended) |
|---|---|---|---|
| Session prepare brief (AI themes/risks inside a session) | (session detail nested `brief`) | `InterviewPrepBriefDto` | **Do not reuse** |
| Durable study Interview Prep brief | Interview Prep brief | — | `InterviewPrepStudyBriefDto` (+ request/list types below) |

Wire resource path remains `/briefs`. JSON field names below are authoritative for FE/OpenAPI. Implementers must not rename or overload existing session `InterviewPrepBriefDto`.

---

## 1. REST endpoints

| Method | Path | Success | Request body | Response | Rate limit |
|---|---|---|---|---|---|
| `POST` | `/api/interview-prep/briefs` | `201 Created` + `Location` → get-by-id | `InterviewPrepGenerateStudyBriefRequest` | `InterviewPrepStudyBriefDto` | Yes (`PolicyInterviewPrep`) |
| `POST` | `/api/interview-prep/briefs/{id:guid}/regenerate` | `200 OK` | `InterviewPrepRegenerateStudyBriefRequest` | `InterviewPrepStudyBriefDto` | Yes (`PolicyInterviewPrep`) |
| `GET` | `/api/interview-prep/briefs` | `200 OK` | — (query optional) | `InterviewPrepStudyBriefListResponseDto` | No |
| `GET` | `/api/interview-prep/briefs/{id:guid}` | `200 OK` | — | `InterviewPrepStudyBriefDto` | No |
| `DELETE` | `/api/interview-prep/briefs/{id:guid}` | `204 No Content` | — | empty | No |

**Not in v1:** PATCH/PUT body edit, version history, session seed, auto-regen, idempotency key on generate.

**List query (optional, recommended for job deep-link):**

| Query | Type | Semantics |
|---|---|---|
| `scrapeResultId` | `guid` | Return at most the owned brief bound to that scrape (empty list if none). |
| `cvOnly` | `bool` | When `true`, return at most the CV-only brief (`scrapeResultId` null). Ignore if `scrapeResultId` also set (prefer scrape filter). |

Default list: all owned briefs, newest `generatedAt` first.

---

## 2. Enums (wire = camelCase JSON)

Reuse session parsers (`InterviewPrepEnumNames` / `JsonStringEnumConverter` camelCase).

### 2.1 Language (`InterviewPrepLanguage`)

| Wire | CLR |
|---|---|
| `english` | `English` |
| `danish` | `Danish` |
| `mixedEnglishDanish` | `MixedEnglishDanish` |

### 2.2 Market (`InterviewPrepMarket`)

| Wire | CLR |
|---|---|
| `general` | `General` |
| `danish` | `Danish` |

### 2.3 Brief topic gap (`InterviewPrepBriefTopicGap` — **new**)

| Wire | Meaning (CONTEXT) |
|---|---|
| `alreadyStrong` | already-strong |
| `mustStudy` | must-study |
| `niceToHave` | nice-to-have |
| `unclear` | unclear (not enough CV/job evidence) |

### 2.4 Outdated reason (`InterviewPrepBriefOutdatedReason` — **new**)

| Wire | When set |
|---|---|
| `structuredCvChanged` | Structured CV change token at read ≠ token persisted at generate |
| `boundJobMissing` | Brief had a non-null `scrapeResultId` at generate and the owned scrape is now missing/deleted (FK SET NULL and/or store lookup fails) |

`outdated` is `true` iff `outdatedReasons.length > 0`. Never auto-regenerate.

---

## 3. Request bodies

### 3.1 `POST /briefs` — `InterviewPrepGenerateStudyBriefRequest`

| Field | Type | Required | Notes |
|---|---|---|---|
| `scrapeResultId` | `guid \| null` | No | Omit/null = CV-only brief. If set: must be **owned** scrape (same adapter as sessions). |
| `language` | Language enum | **Yes** | Same wire set as practice sessions. |
| `market` | Market enum | **Yes** | Same wire set as practice sessions. |
| `focusNote` | `string \| null` | No | Free-text steer for this generate only; not editable brief body. Trim; reject if empty-after-trim when provided; max length **2000** chars. |

**Preconditions:** caller has a Structured CV (same adapters as sessions). Missing CV → `400` `interview_prep_brief_cv_required`.

**Uniqueness:** if a brief already exists for `(userId, scrapeResultId)` (null-safe CV-only), do **not** overwrite via POST → `409` `interview_prep_brief_exists` (client must `regenerate`).

### 3.2 `POST /briefs/{id}/regenerate` — `InterviewPrepRegenerateStudyBriefRequest`

| Field | Type | Required | Notes |
|---|---|---|---|
| `focusNote` | `string \| null` | No | Steers **this** regenerate only. Omit/null = no focus note (do **not** silently reuse prior note). Same trim/max rules as generate. |
| `language` | Language enum \| null | No | Omit/null = keep stored language. |
| `market` | Market enum \| null | No | Omit/null = keep stored market. |

**Semantics:** same brief `id` retained; structured body + fingerprints + snapshots replaced; `generatedAt` / `updatedAt` refreshed; binding (`scrapeResultId`) **immutable** on regenerate. To change job binding, delete + create.

**Bound job at regenerate:** if brief is job-bound and scrape is missing → still allow regenerate as **CV-only content** against current CV, but response must include `outdated` / `boundJobMissing` until binding cleared… **Frozen rule:** regenerate requires current Structured CV; if scrape missing, regenerate proceeds using CV only, persists `scrapeResultId` as null? **No** — binding stays as stored FK (may already be SET NULL). If FK already null and job snapshots empty, reasons may still include `boundJobMissing` only when we detect “was job-bound and job gone”. Persist a `wasJobBound` / keep last `jobTitle`/`companyName` snapshots; set `boundJobMissing` when scrape id null **and** job title/company snapshots were non-empty at last successful generate, **or** scrape id non-null but store lookup fails. Backend implements fingerprint + presence checks; wire reasons stay as §2.4.

---

## 4. Response DTOs

### 4.1 `InterviewPrepStudyBriefDto`

| Field | Type | Notes |
|---|---|---|
| `id` | `guid` | Stable identity across regenerate. |
| `scrapeResultId` | `guid \| null` | Current FK; null for CV-only or after SET NULL. |
| `jobTitle` | `string \| null` | Snapshot at last generate (may remain after job delete). |
| `companyName` | `string \| null` | Snapshot at last generate. |
| `language` | Language wire string | |
| `market` | Market wire string | |
| `focusNoteSnapshot` | `string \| null` | Last focus note **supplied** on generate/regenerate (null if omitted that run). Read-only. |
| `outdated` | `bool` | Computed on read. |
| `outdatedReasons` | `string[]` | Wire values from §2.4; empty if current. |
| `generatedAt` | `datetimeoffset` | Last successful generate/regenerate. |
| `updatedAt` | `datetimeoffset` | Same as `generatedAt` in v1 (no body edits). |
| `topics` | `InterviewPrepStudyBriefTopicDto[]` | Prioritized study topics. |
| `sampleQuestions` | `InterviewPrepStudyBriefItemDto[]` | Sample questions (+ optional note). |
| `talkingPoints` | `InterviewPrepStudyBriefItemDto[]` | CV talking points (+ optional note). |
| `usedAiFallback` | `bool` | True if fake/fallback path used (parity with session AI DTOs). |

### 4.2 `InterviewPrepStudyBriefTopicDto`

| Field | Type | Required | Notes |
|---|---|---|---|
| `name` | `string` | Yes | Profession-agnostic topic label (skill/tool/domain/method — not software-only “technologies”). |
| `gap` | Brief topic gap wire | Yes | §2.3. |
| `priority` | `int` | Yes | Lower = higher priority; unique-ish within brief; AI/schema should return contiguous from 1. |
| `note` | `string \| null` | No | Short note on the topic. |

### 4.3 `InterviewPrepStudyBriefItemDto` (sample questions & talking points)

| Field | Type | Required | Notes |
|---|---|---|---|
| `text` | `string` | Yes | Question or talking-point text. |
| `note` | `string \| null` | No | Short optional note. |

### 4.4 `InterviewPrepStudyBriefListResponseDto`

| Field | Type | Notes |
|---|---|---|
| `items` | `InterviewPrepStudyBriefDto[]` | Full DTOs (including body). v1 volume is tiny (≤ 1 + N jobs); no summary-only split. |

---

## 5. Uniqueness / cardinality

| Rule | Detail |
|---|---|
| Per job binding | At most **one** row per `(UserId, ScrapeResultId)` where `ScrapeResultId` is non-null. |
| CV-only | At most **one** row per `UserId` with `ScrapeResultId IS NULL`. |
| SQL shape | Filtered unique indexes (or equivalent): unique `(UserId, ScrapeResultId)` WHERE scrape NOT NULL; unique `(UserId)` WHERE scrape IS NULL. |
| Regenerate | Overwrites same row; **no** version history. |
| POST vs regenerate | POST creates only; conflict if binding occupied. |

Sessions must **never** read or require these rows (ADR-0025).

---

## 6. Outdated computation (application-owned)

Persist at generate/regenerate (not exposed on wire unless needed later):

- Structured CV fingerprint / change token (source chosen by backend; e.g. structured updated-at + content hash — document in service).
- Job binding presence intent + title/company snapshots.

On every GET/list mapping:

1. Recompute CV fingerprint; if ≠ stored → add `structuredCvChanged`.
2. If brief is/was job-bound and scrape missing/deleted → add `boundJobMissing`.
3. Set `outdated = outdatedReasons.Any()`.

Never enqueue auto-regenerate.

---

## 7. Regenerate semantics (summary)

| Aspect | Behavior |
|---|---|
| Identity | Same `id` |
| Body | Full replace after AI validate |
| Language / market | Optional override; else keep |
| Focus note | Per-call only; omit = none |
| Binding | Immutable via regenerate API |
| Fingerprints | Re-captured from current CV (+ job if present) |
| HTTP | `200` + full DTO |

---

## 8. Error responses

Match session controller style: `{ message, code? }` (and extras where noted). Auth failures = framework `401`/`403`.

| HTTP | `code` (wire) | When |
|---|---|---|
| `400` | `interview_prep_brief_cv_required` | No Structured CV for user |
| `400` | `interview_prep_brief_invalid_language` | Unknown/unsupported language |
| `400` | `interview_prep_brief_invalid_market` | Unknown/unsupported market |
| `400` | `interview_prep_brief_invalid_focus_note` | Empty-after-trim or > 2000 chars |
| `400` | `interview_prep_brief_scrape_not_owned` | `scrapeResultId` not found or not owned |
| `400` | `interview_prep_brief_validation` | Catch-all schema/body validation (AI JSON failed validation before save, empty topics, etc.) |
| `404` | _(none / empty)_ | Brief id not found or not owned (same as sessions) |
| `409` | `interview_prep_brief_exists` | POST when binding already has a brief; include `existingBriefId` (guid) |
| `409` | `interview_prep_brief_ai_unavailable` | AI disabled / hard failure after retries (optional; may also map to `503`) |
| `429` | _(rate limiter)_ | Generate/regenerate throttled |
| `503` | `interview_prep_brief_ai_unavailable` | Preferred when AI provider down and no acceptable fallback |

**DELETE:** `204` if deleted; `404` if missing/not owned (no body).

---

## 9. AI contract boundary (for ai-llm-engineer)

Named gateway operation (suggested): `GenerateInterviewPrepBrief`.

AI proposes structured JSON matching topics / sampleQuestions / talkingPoints (+ gap enums). Application:

- Validates schema before persist
- Owns outdated, tenancy, uniqueness, fingerprints
- Persists profession-agnostic body (reject software-default-only bias in prompts)
- Fake deterministic provider returns fixed valid body for Dev

Exact prompt/schema files are M2 (T5); **wire DTO shape above is the persist/response contract** AI output must map into.

---

## 10. Proposed contract-registry entry

```yaml
# Principal merges into agent-system/governance/contract-registry.yaml
# under proposed_contracts: (or contracts: with status PROPOSED)
- id: interview-prep-briefs
  status: PROPOSED
  description: >
    Durable Interview Prep brief REST under /api/interview-prep/briefs
    (generate, regenerate, list, get, delete). Independent of sessions.
    Structured topics + Brief topic gap; Language/Market as sessions;
    outdated labels; no version history; no session seed. ADR-0025.
  owner: backend-engineer
  secondary: ai-llm-engineer
  evidence: >
    docs/adr/0025-interview-prep-brief.md;
    agent-system/handoffs/active/interview-prep-brief-2026-08-05/frozen-contracts.md
```

---

## 11. Decisions frozen vs UNDECIDED

### Frozen (implementers may proceed after Principal accept)

- Five REST routes + list filters above  
- DTO field tables + gap/outdated/language/market wire names  
- POST create-only + 409 if exists; regenerate replaces in place  
- Focus note max 2000; omit on regenerate ≠ reuse  
- CLR naming `InterviewPrepStudyBrief*` to avoid session `InterviewPrepBriefDto` collision  

### UNDECIDED (Principal / human if contested — defaults recommended)

| Item | Recommendation if Principal picks default |
|---|---|
| U1 — Expose `cvDocumentId` on study brief DTO? | **No** for v1 (sessions expose it; briefs don't need it on wire). |
| U2 — `503` vs `409` for AI hard-unavailable | Prefer **`503`** + `interview_prep_brief_ai_unavailable`. |
| U3 — List returns full body vs summary | **Full body** (v1 cardinality small). |
| U4 — Regenerate when scrape deleted | Keep row; recompute outdated; allow regenerate from CV; keep title/company snapshots. |

No blocker for M1 T2–T4 if Principal accepts defaults for U1–U4.
