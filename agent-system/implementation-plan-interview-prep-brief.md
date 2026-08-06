# Implementation plan — Interview Prep brief

**Status:** `CLOSED — REQUEST COMPLETE WITH DOCUMENTED LIMITATIONS` (2026-08-05)  
**Operate:** A/B/ADR → C → Close  
**Author:** principal-software-architect  
**Mode:** BROWNFIELD / BRIDGE  
**Domain source:** `CONTEXT.md` — **Interview Prep brief**, **Brief topic gap**  
**ADR:** `docs/adr/0025-interview-prep-brief.md` (Accepted)  
**Parent module:** ADR-0021 Interview Prep v2  
**Mapping:** `agent-system/repository-task-mapping-interview-prep-brief.md`  
**Task id:** `interview-prep-brief-2026-08-05`  
**Archive:** `agent-system/handoffs/archive/interview-prep-brief-2026-08-05/`  
**Library:** 0.5.4 (pack aligned)

**Does not replace** `implementation-plan-interview-prep-v2.md` (session/Full loop). Additive study surface only.

---

## 1. Intent

Ship a durable **Interview Prep brief**: a read-only study pack generated from the seeker’s **Structured CV** and optional **saved job**, independent of practice **Interview Prep sessions**.

### v1 success

An authenticated seeker with a Structured CV can:

1. Generate a CV-only brief or a job-bound brief (Language + Market as for practice).
2. Optionally supply a free-text **focus note** on first generate and on regenerate.
3. Open the brief later (one per job binding + one CV-only); regenerate replaces in place (no history).
4. See an **outdated** label when the Structured CV changed since generate, or the bound job is gone — and regenerate manually (never auto).
5. Enter from Interview Prep (sibling to practice) **and** from a saved job deep-link/action.

---

## 2. Product framing (vs what already exists)

| Capability | Today | This plan (v1) |
|---|---|---|
| **Interview Prep session** | Practice Stages / Full loop (ADR-0021+) | Unchanged; does **not** read briefs |
| **Interview Prep brief** | Missing | New durable study artifact |
| **Assist / CV evaluation** | CV builder AI | Unrelated; do not nest under CV builder |

Glossary avoids: conspect, prep section (CV **Section** collision), session seed, technologies (as the whole topic list).

---

## 3. Scope

### In (v1)

| Item | Notes |
|---|---|
| Persistence | One row per `(user, scrapeResultId?)`; regenerate overwrite |
| Structured body | Topics + Brief topic gap; sample questions; CV talking points; short notes per item |
| Gap tags | `alreadyStrong` \| `mustStudy` \| `niceToHave` \| `unclear` |
| Generate inputs | CV required; optional `scrapeResultId`; optional focus note; Language + Market |
| Outdated | CV change token drift and/or bound job missing — label only |
| API | Auth’d `/api/interview-prep/briefs` (list/get/generate/regenerate/delete) |
| AI | Gateway operation + JSON schema; fake deterministic provider for local |
| UI | Sibling study surface on `/interview-prep`; job deep-link (`jobId` → scrape) |
| Tenancy | ADR-0010; owned scrape only |

### Out (v1)

- Feeding brief into session planner / Loop Guard
- Editable brief body or version history
- Auto-regenerate on CV/job change
- Progress checklists / “studied” marks
- Extension surface
- New AI provider/SDK (ADR-0008)

---

## 4. Recommended technical shape

### 4.1 Backend

1. **Entity** `InterviewPrepBrief` — `UserId`, nullable `ScrapeResultId` (ON DELETE SET NULL), Language, Market, focus-note snapshot (optional last note), structured body JSON, CV fingerprint, job title/company snapshots, timestamps.
2. **Unique** `(UserId, ScrapeResultId)` with filtered/null semantics for CV-only.
3. **Service** — load CV via `ICvStructuredDocumentService`; optional job via `IScrapeResultStore`; compute outdated on read; generate/regenerate via AI gateway; never mutate CV/job.
4. **Controller** — extend `InterviewPrepController` or dedicated brief endpoints under same route prefix.
5. **Options** — prompts/schema under Interview Prep AI options (or dedicated subsection); rate-limit generate like prepare/turns.

### 4.2 AI

- Named gateway operation (e.g. `GenerateInterviewPrepBrief`).
- Profession-agnostic system rules; topics not software-default.
- Validate structured parts before persist; reject/repair policy consistent with other Interview Prep AI ops.
- Fake provider returns fixed structured brief for Dev without Gemini.

### 4.3 Frontend

- Models + API client + facade signals under `features/interview-prep/`.
- Page: Study brief vs Practice (sibling); generate form (job picker optional, Language, Market, focus note); render structured parts; outdated banner; regenerate.
- Jobs: action/deep-link into `/interview-prep` with `jobId` + study mode (exact query param at mapping/implement).

### 4.4 Contract sketch (illustrative)

```text
POST /api/interview-prep/briefs
  { scrapeResultId?, language, market, focusNote? }
  → 201 BriefDto

POST /api/interview-prep/briefs/{id}/regenerate
  { focusNote?, language?, market? }
  → 200 BriefDto  (replace body; same identity)

GET  /api/interview-prep/briefs
GET  /api/interview-prep/briefs/{id}
DELETE /api/interview-prep/briefs/{id}

BriefDto: id, scrapeResultId?, jobTitle?, companyName?, language, market,
  outdated, outdatedReasons[], generatedAt, topics[], sampleQuestions[], talkingPoints[]
Topic: name, gap (alreadyStrong|mustStudy|niceToHave|unclear), note?, priority?
```

---

## 5. Milestones

| Id | Focus | Agents | Gate |
|---|---|---|---|
| **M0** | ADR-0025 + this plan + mapping | principal-software-architect | **DONE** |
| **M1** | Domain entity + EF + REST + outdated | architecture → backend | **DONE** (T1–T4) |
| **M2** | AI gateway generate + schema + prompts + wire T6 | ai-llm + backend | **DONE** (T5–T6) |
| **M3** | Angular sibling UI + job deep-link | frontend | **DONE** (T7–T9) |
| **M4** | Integration polish | frontend + backend | **DONE** (covered in M3; no separate QA run) |

BRIDGE after plan approval: GitHub Issue (`ready-for-agent`) via `/to-spec` → `/to-tickets` → implement (tests only when explicitly authorized).

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| Soft “technologies” bias in prompts | Profession-agnostic rules + review samples for non-eng CV |
| Unique index with null scrape | Explicit CV-only uniqueness pattern (filtered index / sentinel) |
| Stale fingerprint false positives | Document CV fingerprint source (e.g. structured updated-at / hash) |
| Scope creep into session seeding | ADR-0025 §1; defer to later ADR |

---

## 7. Approval

**Approved** 2026-08-05. GitHub Issue filing deferred (implement-first BRIDGE, same pattern as prior Interview Prep slices). Operate **C** started at M1.
