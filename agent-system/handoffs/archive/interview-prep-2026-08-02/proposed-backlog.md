# Interview Prep — proposed backlog

**Task:** `interview-prep-2026-08-02`  
**Author:** product-manager  
**Date:** 2026-08-02  
**Status:** PROPOSED — awaiting human approval  

---

## APPROVAL REQUIRED

**Do not file GitHub issues until a human explicitly approves this backlog list** (whole set, subset, or revised set).

Mirror prior improvement operate closes (e.g. job-search-improvements): PM drafts → human approves → Principal runs `gh issue create` with labels `enhancement` + `needs-triage`.

This pack must **not** run `gh issue create`.

---

## 1. Problem statement

ApplyVault already helps seekers **capture jobs**, **build a Structured CV**, and **schedule interview events**. It does **not** help them **practice** for interviews using that same context.

Without Interview Prep, users leave the product for generic coach tools that:

- Ignore their Structured CV and saved scrape/job details
- Often assume software-engineering interviews (coding drills, “system design” for every role)
- Do not reconnect practice outcomes to the job they are applying for

**Product intent:** an adaptive, **profession-agnostic** mock-interview coach on `/interview-prep`:

| Rule | Detail |
|---|---|
| Structured CV | **Required** — gate to `/cv-builder` if missing |
| Saved job | **Optional** — general prep from CV alone; job-targeted when a scrape/saved job is selected or deep-linked |
| Adaptation | Infer **role, seniority, and interview style** from CV ± job — never hardcode developer / full-stack / .NET banks |
| Modes | Role-agnostic labels (see below) |
| UX | Chat-style mock interview + **scorecard after each round** |
| AI vendor | Gemini via existing raw HTTP clients only (ADR-0008); CV remains unchanged by default (ephemeral propose pattern) |

### Mode catalog (role-agnostic labels)

| Mode | Intent |
|---|---|
| Screening / motivation | Why this role/org; career story; motivation |
| Behavioral / culture | Competency / values / collaboration stories |
| Role & domain depth | Domain craft for the inferred profession (not coding-by-default) |
| Problem-solving / case | Structured reasoning on realistic cases for that profession |
| Process & systems | How work gets done end-to-end; **technical system design only when CV/job is technical** |
| Language practice (EN / DA) | Spoken/written interview fluency in English or Danish |
| Full loop | Multi-round sequence stitching the above |

---

## 2. Journeys in scope

1. **CV-ready, general prep** — Authenticated user with Structured CV opens `/interview-prep`, skips job, picks a mode, completes a chat round, sees a scorecard.
2. **CV-ready, job-targeted prep** — User selects a saved scrape/job (or arrives via deep-link from job detail) → coach uses CV + job description.
3. **CV missing** — User hits Interview Prep → clear gate → CTA to `/cv-builder` (Blank CV / PDF import); no mock session starts.
4. **Deep-link from Jobs** — From a saved job surface, “Prepare for interview” → `/interview-prep?jobId=…` (or equivalent) with job preselected.
5. **AI unavailable** — `GoogleAi:Enabled=false` or provider failure → honest empty/error state; no invented questions or scores.

### Related verified journeys (context only)

- Scrape → save → Jobs dashboard (FR-01 / FR-02)
- Structured CV edit/export (FR-05 / FR-06; ADR-0001 / ADR-0002)
- Calendar interview events on saved jobs (FR-08) — **nudge into prep is Later**, not MVP

---

## 3. Non-goals (this milestone)

- Hardcoded developer / full-stack / .NET (or any single-profession) question banks as the product default
- Mutating the Structured CV or scrape result as a side effect of prep (no silent AI write-back)
- Voice / video interviews; live coding IDE / whiteboard as MVP surfaces
- Payment / billing; org / coach multi-seat
- Browser-extension Interview Prep UI
- Replacing Supabase identity
- Claiming GDPR DPIA completeness in issue text
- Filing GitHub issues before human approval

**Later (explicit stretch, not MVP):** session history / resume; calendar interview nudge into prep; save prep notes to job or personal notes.

---

## 4. Prioritized backlog

Suggested delivery sequence (MVP): **IP-01 → IP-02 + IP-03 → IP-04 + IP-05 → IP-06 → IP-07 + IP-08 → IP-09 + IP-10 → IP-11 → IP-12**. Later items wait on MVP chat + scorecard.

| ID | Title | Horizon | Priority | Owning agents | Dependencies |
|---|---|---|---|---|---|
| IP-01 | Architecture: Interview Prep session & scoring contracts | MVP | P0 | architecture-engineer | — |
| IP-02 | Structured CV gate + `/interview-prep` route shell | MVP | P0 | frontend-engineer, ui-ux-designer | IP-01 (route/query contracts) |
| IP-03 | Backend: assemble prep context (Structured CV ± optional scrape job) | MVP | P0 | backend-engineer | IP-01 |
| IP-04 | Profession-agnostic role / seniority / interview-style inference | MVP | P0 | ai-llm-engineer, backend-engineer | IP-01, IP-03 |
| IP-05 | Mode catalog UI + conditional Process & systems eligibility | MVP | P0 | frontend-engineer, ai-llm-engineer, ui-ux-designer | IP-01, IP-04 |
| IP-06 | Mock interview chat-turn API (Gemini HTTP, ephemeral session) | MVP | P0 | backend-engineer, ai-llm-engineer | IP-01, IP-03, IP-04, IP-05 |
| IP-07 | Chat-style mock interview UI | MVP | P0 | frontend-engineer, ui-ux-designer | IP-02, IP-06 |
| IP-08 | Round scorecard (criteria + feedback) | MVP | P0 | backend-engineer, frontend-engineer, ui-ux-designer | IP-06, IP-07 |
| IP-09 | Optional saved-job picker + deep-link from job detail | MVP | P1 | frontend-engineer, backend-engineer | IP-02, IP-03 |
| IP-10 | Shell nav + empty / error / AI-off states | MVP | P1 | frontend-engineer | IP-02 |
| IP-11 | Language practice mode (EN / DA) | MVP | P1 | ai-llm-engineer, frontend-engineer | IP-05, IP-06, IP-07 |
| IP-12 | Full-loop mode (multi-round orchestration) | MVP | P2 | ai-llm-engineer, frontend-engineer | IP-05–IP-08, IP-11 |
| IP-13 | Session history (list / resume past prep) | Later | P3 | backend-engineer, frontend-engineer | IP-06–IP-08 |
| IP-14 | Calendar interview nudge → start targeted prep | Later | P3 | frontend-engineer, backend-engineer | IP-09; calendar interview event on scrape |
| IP-15 | Save prep notes (job-linked or personal) | Later | P3 | backend-engineer, frontend-engineer | IP-08; coordinates with SJ notes (#44) if overlapping |

**Suggested labels when Principal files (after approval):** `enhancement`, `needs-triage`  
(Same as job-search / settings / saved-jobs improvement packs. Promote to `ready-for-agent` only after `/to-spec` deepening if needed.)

**Proposed FR note for Principal (not auto-written to project-specification):** new FR-candidate — “Mock interview prep from Structured CV with optional saved-job targeting” — for a later `/update-context` / spec amend after ship.

---

## 5. Domain & contract alignment

| Constraint | How backlog respects it |
|---|---|
| ADR-0001 section catalog | Prep **reads** Structured CV; does not invent section types |
| ADR-0002 CV builder sole CV surface | Missing CV → gate to `/cv-builder`; prep is not a second CV editor |
| ADR-0008 Gemini HTTP only | New `GoogleAi*` HTTP client + options section; no SDK / second vendor |
| Ephemeral AI propose pattern | Prep transcripts/scores may be session-scoped in MVP; must not silently mutate CV |
| NFR-01 tenancy | All prep APIs scoped to authenticated user; job ids must belong to caller |
| NFR-03 rate limiting | Interview chat endpoints participate in API rate limits |
| CONTEXT.md vocabulary | Use **Structured CV**, **CV builder**, **Section** / **Entry** — not “parsed CV” / “My CV editor” |

---

## 6. Open questions for human

1. **MVP persistence:** Is in-memory / short-lived server session enough for chat + scorecard (IP-06/IP-08), with durable history deferred to IP-13?
2. **Full loop in first ship:** Keep IP-12 as MVP P2, or move Fully to Later if capacity is tight?
3. **Danish language practice:** Target spoken-interview fluency only, or also allow Danish UI chrome later?
4. **Scorecard dimensions:** Fixed profession-agnostic rubrics (clarity, evidence, structure, role-fit, language) vs fully model-generated criteria each round?
5. **Deep-link param name:** Prefer `jobId` = scrape-result id (matches Jobs) vs a neutral `savedJobId` alias?
6. **Coordination with #44 (per-job notes):** Should IP-15 explicitly extend that model, or stay a separate prep-notes store until SJ Phase 4 lands?

---

## 7. Issue body index

| ID | File |
|---|---|
| IP-01 | `issue-bodies/IP-01.md` |
| IP-02 | `issue-bodies/IP-02.md` |
| IP-03 | `issue-bodies/IP-03.md` |
| IP-04 | `issue-bodies/IP-04.md` |
| IP-05 | `issue-bodies/IP-05.md` |
| IP-06 | `issue-bodies/IP-06.md` |
| IP-07 | `issue-bodies/IP-07.md` |
| IP-08 | `issue-bodies/IP-08.md` |
| IP-09 | `issue-bodies/IP-09.md` |
| IP-10 | `issue-bodies/IP-10.md` |
| IP-11 | `issue-bodies/IP-11.md` |
| IP-12 | `issue-bodies/IP-12.md` |
| IP-13 | `issue-bodies/IP-13.md` |
| IP-14 | `issue-bodies/IP-14.md` |
| IP-15 | `issue-bodies/IP-15.md` |

---

## 8. Assumptions

- Primary user is a solo authenticated job seeker (project-specification assumption).
- “Saved job” means an owned **scrape result** in ApplyVault Jobs (not a transient EURES/Jobnet search hit unless already saved).
- General prep without a job is a first-class path, not an edge case.
- Process & systems “technical system design” is a **conditional specialization**, not a default mode label rename.
