# Implementation plan — CV quality evaluation (starter)

**Status:** `CLOSED — REQUEST COMPLETE WITH DOCUMENTED LIMITATIONS` (2026-08-01)  
**Task id:** `cv-quality-evaluation-2026-08-01`  
**Author:** principal-software-architect (operate option A → D)  
**Date:** 2026-08-01  
**Approved:** 2026-08-01  
**Closed:** 2026-08-01 — archive `handoffs/archive/cv-quality-evaluation-2026-08-01/summary.yaml`  
**Mode:** BROWNFIELD / BRIDGE  
**Spec:** `agent-system/project-specification.md` (APPROVED) — FR-07 AI optional; ADR-0001 / ADR-0002  
**Library:** 0.5.4 (pack aligned)

**Does not replace** `agent-system/implementation-plan.md` (stable CV builder v1) or  
`agent-system/implementation-plan-cv-builder-edit-architecture.md`.

---

## 1. Intent

Ship a **starter CV quality evaluation**: the user asks ApplyVault to review their **Structured CV alone** (no job description) for **content, structure, and format**, and gets a clear scored report with findings.

**Later (explicitly out of v1):** JD-vs-CV recruiter match (match score vs JD, hire Interview/Pass, JD-gap interview questions). Design the API/DTO so that extension can add optional JD input and extra report sections without rewriting the starter path.

### v1 success

On `/cv-builder`, an authenticated user with a Structured CV can run **Evaluate CV**, see a structured report (scores + findings for content / structure / format), and optionally jump into existing Assist suggestions/update flows to act on findings. Evaluation does not mutate the CV by itself.

---

## 2. Product framing (vs what already exists)

| Capability | Today | This plan (v1) |
|---|---|---|
| **AI suggestions** | Assist → “Generate suggestions” → actionable tips with `suggestedInstruction` for update | Keep as-is |
| **AI update** | Assist → free-text / apply selected suggestions | Keep as-is |
| **Check export** | Preview / download fidelity | Keep as-is (not an AI review) |
| **CV quality evaluation** | Missing | New: diagnostic report (content / structure / format) |

Evaluation = **diagnose**. Suggestions = **prescribe edits**. Do not merge them into one endpoint in v1.

---

## 3. Scope

### In (v1)

| Item | Notes |
|---|---|
| Server-side Gemini evaluate | Same HTTP/`GoogleAi*` pattern as suggestions; new options section e.g. `CvEvaluationAi` |
| Input | Current user’s Structured CV only (serialize like suggestions; no JD) |
| Output (typed JSON) | Overall score + dimension scores (**content**, **structure**, **format**) + short summary + findings list + optional top interview-style self-check questions (CV-only, not JD) |
| Finding shape | Severity, dimension, title, detail; optional `sectionId` / `entryId` when grounded |
| API | Auth’d `POST …/current/structured/ai-evaluation` (name finalizable at mapping) |
| UI | On CV builder only (ADR-0002); presentational report surface |
| Fail-safe | `GoogleAi:Enabled` false / missing key → clear BadRequest/disabled UX; no fake scores |
| Tests | Unit tests for parse/schema + service guards; FE smoke/spec for report render |
| Extensibility hook | Request/response leave room for later `jobDescription` + JD sections (documented, unused in v1) |

### Out (v1)

- JD paste / saved-job match / hire verdict / Interview vs Pass
- Persisting evaluation history in DB (default: **ephemeral** response; see §7)
- Auto-applying fixes to the Structured CV
- Extension / non-CV surfaces
- Replacing Assist suggestions
- Pixel/layout QA of export HTML vs Puppeteer (that remains Check export / ADR-0003)
- New AI provider or SDK

### Later (v2+ — backlog only)

- Optional JD input + match score, missing skills vs JD, red flags vs JD, hire recommendation
- Persist evaluations per job application
- Deep-link from Jobs detail → evaluate with that JD

---

## 4. Recommended technical shape

### 4.1 Backend / AI

Mirror `CvStructuredSuggestionsService` + `GoogleAiCvStructuredSuggestionsClient`:

1. **Options:** `CvEvaluationAi` with system + user prompt templates (config-tunable; defaults in code).
2. **Client:** Gemini `generateContent` + **response schema** (JSON) — not free-form markdown.
3. **Service:** Load Structured CV for user; reject empty sections; call client; map to DTO; never invent employers/dates (same grounding rule as suggestions).
4. **Controller:** New POST under `CvDocumentsController` next to `ai-suggestions`.
5. **DI:** Register HttpClient + scoped service like other CV AI clients.

**Prompt role (v1):** expert resume reviewer (not JD recruiter). Axes:

- **Content** — clarity, outcomes, specificity, buzzword/empty phrasing, contact completeness.
- **Structure** — section order/coverage, missing expected sections for a general professional CV, entry density, chronology/gaps *as visible in the CV*.
- **Format** — scanability of entry fields, length, consistency of dates/titles, ATS-hostile patterns in *structured field content* (not template CSS).

**Format note:** v1 evaluates **structured field content**, not the selected export Template’s CSS. Template choice stays presentation-only (ADR-0002). Label the UI “format of your content,” not “template design review.”

### 4.2 Contract sketch (illustrative)

```text
Request (v1): { } or { maxFindings?: 1–20 }
  // reserved later: jobDescription?: string | null

Response:
  overallScore: 0–100
  summary: string
  dimensions: [
    { id: "content"|"structure"|"format", score: 0–100, summary: string }
  ]
  findings: [
    { id, dimension, severity: "info"|"warning"|"critical",
      title, detail, sectionId?, entryId? }
  ]
  selfCheckQuestions: string[0–3]   // optional probing questions about the CV itself
  // reserved later: jobFit?: { matchScore, strengths, gaps, redFlags, verdict, interviewQuestions }
```

### 4.3 Frontend

- Entry point on `/cv-builder` (Assist panel section **or** dedicated Evaluate action — §7).
- Call API; show scores + findings; loading/error/disabled-when-no-CV.
- Optional: “Use in Assist” copies a finding into update instructions (nice-to-have; not required for M2 DoD).
- Do not invent a second CV surface.

### 4.4 Agents (when implementing via C/D)

| Milestone | Primary | Support |
|---|---|---|
| M1 contracts + API shell | backend-engineer | architecture-engineer if contract disputed |
| M1 prompts + schema client | ai-llm-engineer | backend-engineer |
| M2 UI | frontend-engineer | ui-ux-designer if layout contested |
| M3 validation | qa-engineer | code-review-engineer on PR |

BRIDGE delivery after approval: GitHub Issue → `/to-tickets` → `/implement` (+ `/tdd`) → `/code-review`.

---

## 5. Milestones

### M0 — Plan approval (no code)

**Owner:** Principal + human  
**DoD:**
- [x] This plan marked approved (or rejected with direction)
- [x] Open decisions in §7 resolved or explicitly deferred (**D2 locked: no persist**)
- [ ] GitHub Issue filed (`ready-for-agent`) with milestone checklist — BRIDGE (deferred; implement-first)

**Dependencies:** none  
**Risks:** Scope creep back into JD matching — gate with §3 Out list  
**Completed:** 2026-08-01 (human GO IMPLEMENT + D2)  

### M1 status

**Completed:** 2026-08-01 — handoff `handoffs/active/cv-quality-evaluation-2026-08-01/handoff-m1-ai-backend.yaml` (READY; reconciled)

### M2 status

**Completed:** 2026-08-01 — handoff `handoffs/active/cv-quality-evaluation-2026-08-01/handoff-m2-frontend.yaml` (READY; reconciled)

---

### M1 — Evaluation API + Gemini client

**Owners:** backend-engineer + ai-llm-engineer  
**DoD:**
- [ ] `CvEvaluationAi` options + defaults for content/structure/format review
- [ ] AI client with JSON response schema + parse/validation tests
- [ ] Service loads Structured CV; empty CV → clear error
- [ ] `POST …/structured/ai-evaluation` auth’d; tenancy = current user only
- [ ] AI disabled / misconfigured → fail safe (no invented scores)
- [ ] Prompt does **not** require or accept JD in v1
- [ ] DTO documents reserved fields / comments for v2 JD extension

**Dependencies:** M0  
**Parallel:** FE can stub types after contract freeze mid-M1  
**Validation:** unit tests; optional integration happy-path if Issue requires

---

### M2 — CV builder Evaluate UI

**Owner:** frontend-engineer  
**DoD:**
- [ ] Evaluate control on `/cv-builder` per §7 UI decision
- [ ] Renders overall + dimension scores, findings, self-check questions
- [ ] Loading / error / AI-unavailable states
- [ ] Does not mutate Structured CV
- [ ] Basic component/facade tests

**Dependencies:** M1 contract stable (can mock until API lands)  
**Validation:** frontend-ci / targeted specs

---

### M3 — Hardening + close

**Owners:** qa-engineer (+ code-review on PR)  
**DoD:**
- [ ] Happy path + empty CV + AI-off cases verified
- [ ] No secret/prompt leakage in logs beyond existing AI patterns
- [ ] Issue acceptance checked; handoff READY; Principal Close

**Dependencies:** M1 + M2  
**Out:** JD evaluation E2E

---

## 6. Risks and mitigations

| Risk | Level | Mitigation |
|---|---|---|
| Overlaps Assist suggestions → user confusion | MED | Distinct copy: “Evaluate” vs “Suggest improvements”; evaluation does not auto-edit |
| Model invents CV facts | HIGH | Same grounding rules as suggestions; findings must cite existing content; schema validation |
| “Format” misread as Template design review | MED | UI copy + prompt: content-field format only |
| Cost / latency of another Gemini call | MED | Single call per Evaluate; optional `maxFindings`; reuse existing rate-limit patterns if present |
| Premature JD scope | HIGH | v1 request rejects unused JD until v2 milestone; plan Out list |
| Score inflation / false confidence | MED | Disclaimer: advisory only; not a hiring decision |

---

## 7. Open decisions (approval gate)

Defaults below are **proposed**. Approve, change, or defer before C/D.

| Id | Decision | Locked for implementation |
|---|---|---|
| **D1** | UI placement | **Assist panel** — Evaluate block below suggestions (default accepted with GO IMPLEMENT) |
| **D2** | Persist evaluations? | **LOCKED — No.** Ephemeral API response + session UI state only. No DB/table/history. |
| **D3** | Overall score required? | **Yes** — single 0–100 + three dimension scores |
| **D4** | Self-check questions | **Yes** — up to 3 CV-only probing questions |
| **D5** | Finding → Assist bridge | **LOCKED (follow-up)** — per finding “Use in Assist”: copy title+detail into Update-with-instructions; pre-select `sectionId` chip when present; do **not** auto-run Update CV with AI |
| **D6** | Domain term in UI | **“CV evaluation”** / “Evaluate CV” |
| **D7** | Optional ADR | **Defer** |

---

## 8. Approval gates

| Gate | Required before |
|---|---|
| **G0 — Human approves this plan** (+ §7) | Any C/D implementation |
| **G1 — Contract freeze** (request/response field names) | M2 UI binding / Issue tickets |
| **G2 — Smoke** (auth → Evaluate → report; AI-off path) | Calling M3 done |
| **G3 — JD v2** | Separate plan/milestone; not started under this task id |

---

## 9. Dependencies / non-goals reminder

- Reuse Gemini HTTP stack; do not add SDKs.
- Honor ADR-0001 (catalog / structured fields) and ADR-0002 (builder sole surface).
- Do not overwrite Assist suggestion prompts for this feature — new options section.
- Delivery via project BRIDGE chain after Issue filing.

---

## 10. After approval

1. Mark this file `APPROVED` and lock §7.
2. File GitHub Issue (`ready-for-agent`) with M1–M3 checklist.
3. Recommend **B** (repo task mapping) if path ownership needs confirmation; else **C** / **D** to implement M1.

---

## 11. Completion criteria (request-level)

`REQUEST COMPLETE` when:

- M1–M3 DoD met under approved decisions
- User can Evaluate CV for content/structure/format without JD
- JD matching remains explicitly deferred (documented limitation OK)

`REQUEST COMPLETE WITH DOCUMENTED LIMITATIONS` if AI-off environments skip live model proof but contract + fail-safe + UI are done.
