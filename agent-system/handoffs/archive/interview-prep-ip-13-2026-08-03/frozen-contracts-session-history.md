# Interview Prep — frozen contracts (IP-13 Session history)

**Status:** PROPOSED (pending Principal acceptance of ADR-0016)  
**Issue:** [#79](https://github.com/PrimusInterParess/ApplyVault/issues/79) IP-13  
**ADR:** `docs/adr/0016-interview-prep-durable-session-history.md`  
**Supersedes:** ADR-0012 decision §1 (ephemeral-only); does **not** replace turn payload enums from IP-01 frozen contracts  
**Baseline turns contract:** `agent-system/handoffs/archive/interview-prep-ip-01-2026-08-02/frozen-contracts.md`  
**Audience:** `backend-engineer` then `frontend-engineer` (after Principal accepts ADR-0016)

---

## 0. Boundary notes

- **CV:** Never call `SaveStructuredAsync` or mutate Structured CV / scrape entities from prep.
- **IP-15 / #44:** Prep notes and saved-job notes are **out of scope**. Session history stores coach transcript + scorecards (+ optional modelAnswer on coach messages) only.
- **Sharing:** Out of scope.
- **Ephemeral path:** `POST /api/interview-prep/turns` without `sessionId` remains as today (no DB write). IP-13 UI uses the durable path.

---

## 1. Endpoints

| Operation | Method / path | Persistence | Notes |
|---|---|---|---|
| Create session | `POST /api/interview-prep/sessions` | Session row | Status `in_progress` |
| List sessions | `GET /api/interview-prep/sessions` | Read | Current user only; newest `UpdatedAt` first |
| Get session | `GET /api/interview-prep/sessions/{id}` | Read | Metadata + ordered messages |
| Delete session | `DELETE /api/interview-prep/sessions/{id}` | Hard delete | Cascade messages |
| Coach turn (durable) | `POST /api/interview-prep/turns` + `sessionId` | Append messages; update session | Same AI response shape as IP-01 |
| Coach turn (ephemeral) | `POST /api/interview-prep/turns` without `sessionId` | None | Unchanged MVP path |

**Auth:** `[Authorize]` + `IAppUserService.GetRequiredUserAsync` (ADR-0009 / ADR-0010).  
**Controller:** `InterviewPrepController` — route prefix `api/interview-prep`.  
**Do not** nest under `CvDocumentsController`.

---

## 2. Status / phase enums

### Session `status`

| Value | Meaning |
|---|---|
| `in_progress` | Open for resume + durable turns |
| `completed` | Read-only; set when a persisted turn returns `phase=debrief` |

### Message / turn `phase` (unchanged)

`interview` | `debrief`

### Message `role` (unchanged)

`user` | `coach`

Mode / `languageMix` / `hiringMarket` catalogs: unchanged from IP-01 + ADR-0013.

---

## 3. Data model (EF / SQL Server)

### `InterviewPrepSession`

| Column | Type | Notes |
|---|---|---|
| `Id` | guid PK | |
| `UserId` | guid FK → AppUser | Required; index with `UpdatedAt` |
| `Mode` | string | Mode catalog id |
| `LanguageMix` | string | `en` \| `da` \| `mixed` |
| `HiringMarket` | string | `general` \| `dk` |
| `ScrapeResultId` | guid? FK | Optional; **ON DELETE SET NULL** |
| `JobTitle` | string? | Snapshot at create (list metadata) |
| `CompanyName` | string? | Snapshot at create |
| `Status` | string | `in_progress` \| `completed` |
| `Phase` | string | Latest phase |
| `InferenceJson` | nvarchar(max)? | Latest inference object JSON |
| `LatestScorecardJson` | nvarchar(max)? | Latest scorecard JSON |
| `LatestOverallScore` | int? | Denormalized 0–100 for list |
| `CreatedAt` | DateTimeOffset | |
| `UpdatedAt` | DateTimeOffset | |
| `CompletedAt` | DateTimeOffset? | Set when completed |

### `InterviewPrepSessionMessage`

| Column | Type | Notes |
|---|---|---|
| `Id` | guid PK | |
| `SessionId` | guid FK | Cascade delete with session |
| `Sequence` | int | 0-based or 1-based; unique per session |
| `Role` | string | `user` \| `coach` |
| `Text` | nvarchar(max) | Chat text (`userMessage` / `coachMessage`) |
| `Phase` | string | Phase **at message time** |
| `ScorecardJson` | nvarchar(max)? | Coach turns only |
| `FollowUpsJson` | nvarchar(max)? | Coach turns; JSON string array |
| `DebriefBulletsJson` | nvarchar(max)? | Coach turns |
| `ModelAnswer` | nvarchar(max)? | Coach turns; ADR-0015 |
| `InferenceJson` | nvarchar(max)? | Coach turns |
| `CreatedAt` | DateTimeOffset | |

**AI context:** load messages ordered by `Sequence`, map to `priorTurns` `{ role, text, phase }`, then apply existing `MaxPriorTurns` / `MaxPriorTurnChars` caps. Do **not** put `modelAnswer` into AI prior turns.

**Abuse caps (recommended defaults; options OK):** max messages per session (e.g. 200); reject further turns with `400` when exceeded. Full transcript retained up to that cap (unlike AI truncation).

---

## 4. `POST /api/interview-prep/sessions`

### Request

```json
{
  "mode": "behavioral",
  "languageMix": "en",
  "hiringMarket": "general",
  "scrapeResultId": null
}
```

| Field | Type | Required | Rules |
|---|---|---|---|
| `mode` | string | yes | Mode catalog |
| `languageMix` | string | no | Default from options / `en` |
| `hiringMarket` | string | no | Default `general` |
| `scrapeResultId` | guid \| null | no | Owned scrape or 404 |

### Response — `201 Created`

```json
{
  "id": "…",
  "mode": "behavioral",
  "languageMix": "en",
  "hiringMarket": "general",
  "scrapeResultId": null,
  "jobTitle": null,
  "companyName": null,
  "status": "in_progress",
  "phase": "interview",
  "latestOverallScore": null,
  "createdAt": "2026-08-03T12:00:00Z",
  "updatedAt": "2026-08-03T12:00:00Z",
  "completedAt": null
}
```

---

## 5. `GET /api/interview-prep/sessions`

### Query

| Param | Type | Default | Notes |
|---|---|---|---|
| `take` | int | 20 | Clamp 1–50 |
| `skip` | int | 0 | Offset pagination |

### Response — `200 OK`

```json
{
  "items": [
    {
      "id": "…",
      "mode": "behavioral",
      "languageMix": "en",
      "hiringMarket": "dk",
      "scrapeResultId": "…",
      "jobTitle": "Pediatric nurse",
      "companyName": "Example Hospital",
      "status": "completed",
      "phase": "debrief",
      "latestOverallScore": 78,
      "createdAt": "…",
      "updatedAt": "…",
      "completedAt": "…"
    }
  ],
  "totalCount": 1
}
```

List is **metadata only** (no messages). Filter: `UserId == current user`. Order: `UpdatedAt` descending.

---

## 6. `GET /api/interview-prep/sessions/{id}`

### Response — `200 OK`

Session metadata (same fields as create/list item) **plus**:

```json
{
  "messages": [
    {
      "id": "…",
      "sequence": 0,
      "role": "user",
      "text": "Let's start.",
      "phase": "interview",
      "scorecard": null,
      "followUps": [],
      "debriefBullets": [],
      "modelAnswer": null,
      "inference": null,
      "createdAt": "…"
    },
    {
      "id": "…",
      "sequence": 1,
      "role": "coach",
      "text": "…",
      "phase": "interview",
      "scorecard": null,
      "followUps": ["…"],
      "debriefBullets": [],
      "modelAnswer": "…",
      "inference": {
        "role": "Pediatric nurse",
        "seniority": "mid",
        "interviewStyle": "competency_behavioral",
        "isTechnicalContext": false
      },
      "createdAt": "…"
    }
  ]
}
```

Scorecard object shape: unchanged from IP-01 frozen contracts §5.  
404 if id missing or other user’s session (no existence leak).

---

## 7. `DELETE /api/interview-prep/sessions/{id}`

- `204 No Content` on success.
- Hard-deletes session + messages.
- `404` if missing/foreign.

---

## 8. Durable turn — `POST /api/interview-prep/turns` delta

Additive request field:

| Field | Type | Required | Rules |
|---|---|---|---|
| `sessionId` | guid \| null | no | When set → durable path |

### When `sessionId` is set

1. Load session by `(id, user.Id)` → 404 if miss/foreign.
2. If `status=completed` → `409 Conflict` (read-only).
3. Validate mode/languageMix/hiringMarket/scrape **against session** (request must not silently retarget another job/mode). Recommended: durable turns **ignore** request `mode` / `languageMix` / `hiringMarket` / `scrapeResultId` and use session values; still require `userMessage`. Invalid/mismatched client overrides → `400` if sent and differ (implementer choice: prefer **session wins**, document in service).
4. Build AI `priorTurns` from stored messages (ordered); apply caps; **do not use client `priorTurns`**.
5. Run existing AI turn pipeline (CV load, optional job from session.ScrapeResultId, Gemini).
6. Persist: append user message (`userMessage`, current phase), append coach message (response fields); update session `Phase`, inference/scorecard denorm, `UpdatedAt`; if response `phase=debrief` → `status=completed`, set `CompletedAt`.
7. Return existing `InterviewPrepTurnResponseDto` (+ optional additive `sessionId` echo — recommended).

### When `sessionId` omitted

Unchanged ephemeral behavior (IP-01). Client `priorTurns` used as today.

### Response additive (recommended)

| Field | Type | Notes |
|---|---|---|
| `sessionId` | guid \| null | Echo when durable; null/omit when ephemeral |

Turn body fields otherwise unchanged (including nullable `modelAnswer`).

---

## 9. Error mapping (session-specific)

| Condition | HTTP |
|---|---|
| Session not found / other user | `404` |
| Turn on `completed` session | `409` |
| Invalid create enums / empty userMessage | `400` |
| Foreign/unknown `scrapeResultId` on create | `404` |
| Message cap exceeded | `400` |
| Missing Structured CV (durable turn) | `404` / `400` same as ephemeral |
| Unauthorized | `401` |

---

## 10. FE binding expectations (non-API but contract-adjacent)

| UI action | API |
|---|---|
| Start new practice | `POST /sessions` then durable `POST /turns` with `sessionId` (bootstrap `Let's start.` unchanged) |
| History list on `/interview-prep` | `GET /sessions` → date (`updatedAt`/`createdAt`), mode, optional job title, status |
| Open completed | `GET /sessions/{id}` → read-only chat/scorecards; no send |
| Resume in-progress | `GET /sessions/{id}` → hydrate messages/`priorTurns`/phase/scorecard/modelAnswer; durable turns |
| Delete | `DELETE /sessions/{id}` |
| Deep-link `?jobId=` | Pass as create-session `scrapeResultId` |

---

## 11. Retention policy (product)

- User hard-delete of own sessions is the MVP retention control.
- No automatic TTL in IP-13.
- No sharing / public links.

---

## 12. Contract registry

- Proposed id: `interview-prep-sessions` (this document + ADR-0016).
- Existing `interview-prep-turns` remains APPROVED; additive `sessionId` / optional response `sessionId` once implemented.
