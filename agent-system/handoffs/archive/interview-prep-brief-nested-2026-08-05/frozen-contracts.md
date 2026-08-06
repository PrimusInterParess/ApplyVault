# Frozen contracts — Interview Prep brief nested body (amendment)

**Task:** `interview-prep-brief-nested-2026-08-05`  
**Agent:** architecture-engineer  
**Status:** READY for Principal accept (approval gate before implement)  
**Supersedes (body only):** archive `interview-prep-brief-2026-08-05/frozen-contracts.md` §§4.1–4.3, §9 (flat brief-root lists)  
**Unchanged from prior freeze / ADR-0025:** REST routes, auth, cardinality, outdated, regenerate semantics, request DTOs, error catalog (except body-stale note below), Language/Market/gap/outdated enums  
**Sources:** ADR-0025 Amended Decision 4; CONTEXT.md (Interview Prep brief, Coverage item, Brief topic gap); current flat code evidence listed in architecture-handoff

---

## 0. Scope of this freeze

| In scope | Out of scope |
|---|---|
| Nested wire/DTO/AI JSON body under each topic | New REST routes or path renames |
| Validation rules for Coverage items + per-topic Q/talking lists | Session lifecycle / `InterviewPrepBriefDto` |
| Persist shape + legacy flat-body migration policy | Progress checklists, edit-in-place, version history |
| Contract-registry description delta (Principal merge) | Dual-read adapter for old flat JSON |

**Wire names (authoritative):** under each topic — `coverageItems`, `sampleQuestions`, `talkingPoints` (camelCase).  
**Not on brief root:** `sampleQuestions`, `talkingPoints`.

---

## 1. REST / auth / cardinality (unchanged — reference)

| Method | Path | Success |
|---|---|---|
| `POST` | `/api/interview-prep/briefs` | `201` + `Location` |
| `POST` | `/api/interview-prep/briefs/{id}/regenerate` | `200` |
| `GET` | `/api/interview-prep/briefs` | `200` |
| `GET` | `/api/interview-prep/briefs/{id}` | `200` |
| `DELETE` | `/api/interview-prep/briefs/{id}` | `204` |

- Auth: `[Authorize]` + Supabase JWT; ADR-0010 per-user tenancy  
- Rate limit generate/regenerate: `PolicyInterviewPrep`  
- At most one brief per `(user, scrapeResultId)` + at most one CV-only; regenerate replaces row  
- Sessions never read briefs  

Request bodies (`InterviewPrepGenerateStudyBriefRequest` / `InterviewPrepRegenerateStudyBriefRequest`) and list query (`scrapeResultId`, `cvOnly`) — **unchanged** from prior freeze.

---

## 2. Enums (unchanged)

### Brief topic gap (`InterviewPrepBriefTopicGap`)

| Wire | Meaning |
|---|---|
| `alreadyStrong` | already-strong |
| `mustStudy` | must-study |
| `niceToHave` | nice-to-have |
| `unclear` | unclear |

Gap and numeric `priority` live **only on the topic** — never on Coverage items, sample questions, or talking points.

### Outdated reason (unchanged)

`structuredCvChanged` | `boundJobMissing` — application-owned; never auto-regen.

Language / Market — same session wire set (`english`/`danish`/`mixedEnglishDanish`; `general`/`danish`).

---

## 3. Response DTOs (nested body)

### 3.1 `InterviewPrepStudyBriefDto` (brief root)

| Field | Type | Notes |
|---|---|---|
| `id` | `guid` | Stable across regenerate |
| `scrapeResultId` | `guid \| null` | |
| `jobTitle` | `string \| null` | Snapshot |
| `companyName` | `string \| null` | Snapshot |
| `language` | Language wire | |
| `market` | Market wire | |
| `focusNoteSnapshot` | `string \| null` | Last supplied focus note |
| `outdated` | `bool` | |
| `outdatedReasons` | `string[]` | |
| `generatedAt` | `datetimeoffset` | |
| `updatedAt` | `datetimeoffset` | |
| `topics` | `InterviewPrepStudyBriefTopicDto[]` | Nested children live **here** |
| `usedAiFallback` | `bool` | |

**Removed from brief root (breaking vs flat v1):** `sampleQuestions`, `talkingPoints`.

### 3.2 `InterviewPrepStudyBriefTopicDto`

| Field | Type | Required | Notes |
|---|---|---|---|
| `name` | `string` | Yes | Profession-agnostic topic label |
| `gap` | Brief topic gap wire | Yes | §2 |
| `priority` | `int` | Yes | Lower = higher priority; unique within brief; AI should return contiguous from 1 |
| `note` | `string \| null` | No | Short note on the topic |
| `coverageItems` | `InterviewPrepStudyBriefItemDto[]` | Yes | **≥1** Coverage item (leaf) |
| `sampleQuestions` | `InterviewPrepStudyBriefItemDto[]` | Yes (array) | May be **empty**; independent of coverage |
| `talkingPoints` | `InterviewPrepStudyBriefItemDto[]` | Yes (array) | May be **empty**; independent of coverage |

### 3.3 `InterviewPrepStudyBriefItemDto` (shared leaf shape)

Used for **Coverage items**, **sample questions**, and **CV talking points**.

| Field | Type | Required | Notes |
|---|---|---|---|
| `text` | `string` | Yes | Non-empty after trim |
| `note` | `string \| null` | No | Short optional note |

**CLR:** reuse one item record for all three lists (minimal). Do **not** add `gap`, `priority`, `id`, or parent/coverage refs on items.

**Product term:** Coverage item = leaf syllabus line under a topic (`CONTEXT.md`) — not a checklist, not a nested topic, not a second nesting level.

### 3.4 List response (unchanged)

`InterviewPrepStudyBriefListResponseDto.items` = full nested DTOs.

---

## 4. Nesting / independence rules (normative)

1. **Exactly one nesting level:** topic → Coverage items / sample questions / talking points. Coverage items are **leaves** (no children).
2. **Three sibling lists** under each topic: `coverageItems`, `sampleQuestions`, `talkingPoints`. No wire links (no `coverageItemIndex`, no shared ids, no “relatesTo”).
3. **≥1 Coverage item** per topic.
4. **Empty** `sampleQuestions` / `talkingPoints` arrays are valid.
5. **No brief-level** sample-question or talking-point lists.
6. **Not progress checklists** — no `done` / `checked` / progress fields on Coverage items (out of v1).
7. Topics remain profession-agnostic (skills/tools/domains/methods — avoid software-only “technologies” as the whole list framing).

---

## 5. Validation rules (application + AI gateway)

Reject before persist (`400` `interview_prep_brief_validation` or AI validation fail → same family):

| Rule | Detail |
|---|---|
| Topics present | `topics` non-null; `topics.length >= 1` |
| Topic fields | non-empty `name`; valid `gap`; `priority >= 1`; priorities unique within brief |
| Coverage | each topic: `coverageItems` non-null; `coverageItems.length >= 1`; each item non-empty `text` |
| Sample Q | each topic: `sampleQuestions` non-null array (may be `[]`); each present item non-empty `text` |
| Talking points | each topic: `talkingPoints` non-null array (may be `[]`); each present item non-empty `text` |
| No root lists | brief JSON must **not** require or persist root `sampleQuestions` / `talkingPoints` |
| No deeper nesting | item objects are `{ text, note? }` only |
| No cross-links | reject unknown link/ref properties if strictly validated; do not invent link fields |
| Focus note | unchanged: trim; reject empty-after-trim when provided; max **2000** |

Null arrays are invalid — use empty arrays for optional lists.

---

## 6. Persist body shape

Storage JSON (camelCase), e.g. column / `InterviewPrepStudyBriefBodyStorage`:

```json
{
  "topics": [
    {
      "name": "string",
      "gap": "mustStudy",
      "priority": 1,
      "note": "string or omit/null",
      "coverageItems": [{ "text": "string", "note": "string or omit/null" }],
      "sampleQuestions": [{ "text": "string", "note": null }],
      "talkingPoints": []
    }
  ]
}
```

**CLR target (recommended):**

- `InterviewPrepStudyBriefBodyStorage(IReadOnlyList<InterviewPrepStudyBriefTopicDto> Topics)` — **drop** root SampleQuestions/TalkingPoints  
- Topic DTO carries the three child lists  

---

## 7. AI JSON schema shape (`GenerateInterviewPrepStudyBrief`)

Named gateway op (existing): `GenerateInterviewPrepStudyBrief` — keep; **reshape response only**.

### 7.1 Root

| Field | Required | Notes |
|---|---|---|
| `topics` | Yes | Array, min length 1 |

**Remove from AI root:** `sampleQuestions`, `talkingPoints`.

### 7.2 Topic object

| Field | Required | Notes |
|---|---|---|
| `name` | Yes | string |
| `gap` | Yes | `alreadyStrong` \| `mustStudy` \| `niceToHave` \| `unclear` |
| `priority` | Yes | integer ≥ 1 |
| `note` | No | string \| null |
| `coverageItems` | Yes | array, **minItems: 1**, items = item schema |
| `sampleQuestions` | Yes | array, minItems: 0 allowed |
| `talkingPoints` | Yes | array, minItems: 0 allowed |

### 7.3 Item schema (Coverage / Q / talking)

| Field | Required |
|---|---|
| `text` | Yes |
| `note` | No (nullable) |

### 7.4 Prompt constraints (ai-llm)

- Produce durable study brief, not session prepare brief  
- Profession-agnostic topics; nest Coverage items + optional Q/talking under each topic  
- Independent sibling lists; no linking; one nesting level; Coverage items are not checklists  
- Map 1:1 into persist/REST topic DTO (no brief-root Q/talking piles)

Fake deterministic provider must return nested-valid bodies for Dev / AI-off.

---

## 8. Legacy flat persisted bodies (migration)

**Current code (evidence):** flat root `topics` + `sampleQuestions` + `talkingPoints` in  
`InterviewPrepStudyBriefContracts.cs`, `InterviewPrepStudyBriefBody.cs`, AI contracts/schema/validator/prompts/fake provider, FE `interview-prep.model.ts`.

**Recommended policy (Principal default — accept to unblock):**

1. **New shape only** — no dual-read / no in-place transform of flat → nested.  
2. Seeker **regenerates** (or deletes + creates) to replace the body.  
3. On deserialize/read: if JSON fails nested validation (legacy flat root lists, missing `coverageItems`, etc.) → treat as stale body.  
4. **GET by id:** `400` with code `interview_prep_brief_body_stale` (message: regenerate required).  
5. **List:** omit stale rows **or** include metadata-only stub — **recommended:** omit from list items (or return stub without inventing nested content). Prefer omit + FE regenerate CTA on empty list for that binding; if omit is too silent, return stub with `topics: []` and document FE “regenerate required when topics empty after known prior generate” — **Principal picks:**  
   - **Default A (recommended):** GET `400` `interview_prep_brief_body_stale`; list **omits** invalid bodies.  
6. Regenerate of a stale row: allowed; AI returns nested body; overwrite storage.

No EF schema migration required for nesting (JSON body column reshape only).

---

## 9. Error codes (delta)

| HTTP | `code` | When |
|---|---|---|
| `400` | `interview_prep_brief_body_stale` | **New.** Persisted body fails nested schema (legacy flat or corrupt). Client should regenerate. |
| (existing) | `interview_prep_brief_validation` | AI/request body validation before save |

All other codes from prior freeze remain.

---

## 10. Naming collision (unchanged)

| Concept | CLR |
|---|---|
| Session prepare brief | `InterviewPrepBriefDto` — **do not reuse** |
| Durable study brief | `InterviewPrepStudyBriefDto` (+ Topic/Item/requests) |

Wire path remains `/briefs`.

---

## 11. Contract-registry note (Principal merge)

Update existing `interview-prep-briefs` (APPROVED) description evidence to nested body:

```yaml
# Delta for agent-system/governance/contract-registry.yaml — interview-prep-briefs
description: >
  Durable Interview Prep brief REST under /api/interview-prep/briefs
  (generate, regenerate, list, get, delete). Independent of sessions.
  Topics nest coverageItems (≥1), sampleQuestions, talkingPoints
  (sibling lists; one nesting level). Brief topic gap on topic only;
  Language/Market as sessions; outdated labels; no version history;
  no session seed. ADR-0025 amended body.
evidence: >
  docs/adr/0025-interview-prep-brief.md;
  agent-system/handoffs/active/interview-prep-brief-nested-2026-08-05/frozen-contracts.md;
  (+ implementer paths after land)
```

---

## 12. Frozen vs Principal decisions

### Frozen (after Principal accept of this doc)

- Nested topic wire: `coverageItems`, `sampleQuestions`, `talkingPoints`  
- ≥1 Coverage item; empty Q/talking lists OK; no root Q/talking; one level; no cross-links  
- AI root = `topics` only; item schema shared  
- New-shape-only persist; regenerate replaces legacy flat  
- REST/auth/cardinality/outdated unchanged  

### Principal decisions

| Item | Decision | Decided |
|---|---|---|
| P1 — Accept nested freeze | **Accepted** | 2026-08-05 — user “go” |
| P2 — Legacy flat body read | **Default A:** GET `400` `interview_prep_brief_body_stale`; list omits invalid bodies | 2026-08-05 |
| P3 — Registry description update | **Merged** into `contract-registry.yaml` | 2026-08-05 |

No other product grill — locked decisions already in ADR-0025 / CONTEXT / task envelope.
