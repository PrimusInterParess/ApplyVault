# Implementation plan — Interview Prep (profession-agnostic coach)

**Status:** `APPROVED` (backlog filed; implementation not started)  
**Task id:** `interview-prep-2026-08-02`  
**Author:** principal-software-architect (operate option **A**)  
**Date:** 2026-08-02  
**Approved:** 2026-08-02 — human chose **all** IP-01…IP-15 → GitHub `#67`–`#81`  
**Mode:** BROWNFIELD / BRIDGE  
**Spec:** `agent-system/project-specification.md` (APPROVED) — extends journeys (jobs + Structured CV); new FR-candidate after ship  
**Library:** 0.5.4 (pack aligned)

**Does not replace** `agent-system/implementation-plan.md` or other feature-specific plans.

**Supporting artifacts (do not implement from this plan until approved):**

| Artifact | Path |
|---|---|
| Archive summary | `agent-system/handoffs/archive/interview-prep-2026-08-02/summary.yaml` |
| Product backlog | `agent-system/handoffs/archive/interview-prep-2026-08-02/proposed-backlog.md` |
| Architecture design | `agent-system/handoffs/archive/interview-prep-2026-08-02/architecture-design.md` |
| Filed issues map | `agent-system/handoffs/archive/interview-prep-2026-08-02/created-issues.json` |
| Issue bodies | On GitHub `#67`–`#81` (source bodies wiped after Close) |

---

## 1. Intent

Ship an **Interview Prep** product surface so authenticated seekers can practice interviews **inside ApplyVault**, grounded in their **Structured CV**, with an **optional** saved job (scrape result) for targeting.

The coach is **profession-agnostic**: it infers role, seniority, and interview style from CV ± job. It must **not** hardcode developer / full-stack / coding / “system design for engineers” defaults.

### MVP success

An authenticated user with a Structured CV can open `/interview-prep`, optionally attach a saved job (or arrive via `?jobId=`), pick a role-agnostic mode, complete a chat-style mock round, and see a scorecard — without mutating the CV.

### Explicit non-goals (MVP)

- Hardcoded single-profession question banks as product default  
- Mutating Structured CV or scrape records from coach turns  
- Persisting session history (refresh loses chat — same trade-off as CV evaluation)  
- Voice/video, live coding IDE, payments, extension UI  
- New AI vendor / SDK (ADR-0008 Gemini HTTP only)  
- New scrape `Requirements` column (use `jobDescription` Markdown)

---

## 2. Product framing

| Input | Rule |
|---|---|
| Structured CV | **Required** — empty → gate CTA to `/cv-builder` (ADR-0002) |
| Saved job | **Optional** — general prep from CV alone; targeted when selected / deep-linked |
| Modes | Screening; Behavioral; Role & domain; Problem-solving/case; Process & systems (*technical* system design only when CV/job is technical); Language EN/DA; Full loop |
| Persistence | Ephemeral client-held turns; server reloads CV ± owned scrape each turn |
| AI | New `GoogleAiInterviewPrepClient` + `InterviewPrepAiOptions`; JSON schema responses |

---

## 3. Technical shape (approved design direction)

### API

- **New** `POST /api/interview-prep/turns` on dedicated `InterviewPrepController` (do **not** nest under `cv-documents`)  
- Always: `ICvStructuredDocumentService.GetStructuredAsync(user)`  
- Optional: `scrapeResultId` → `IScrapeResultStore.GetByIdAsync(id, user.Id)` → company/title/location/summary/`jobDescription`  
- Request: `mode`, `languageMix`, `userMessage`, `priorTurns[]`, optional `scrapeResultId`  
- Response: `phase` (`interview` \| `debrief`), `inferredRole`, `coachMessage`, optional `scorecard`, follow-ups / debrief  
- Never `SaveStructuredAsync`

### Frontend

- Route `/interview-prep` + shell nav  
- Feature folder `features/interview-prep/`  
- Job detail CTA → `/interview-prep?jobId=<scrapeGuid>`  
- Chat + scorecard UI; ephemeral facade state

### ADR (on approval / M0)

Write `docs/adr/NNNN-interview-prep-ephemeral-profession-agnostic.md` (next free number):

- Ephemeral coach turns (no sessions table MVP)  
- Profession-agnostic grounding from CV ± job  
- Dedicated API surface (not CV Assist mutate path)

---

## 4. Milestones

| ID | Milestone | Outcome | Primary agents | Maps to issues |
|---|---|---|---|---|
| **M0** | Human approval + ADR | Plan + backlog locked; ADR accepted | Principal, architecture, PM | IP-01 |
| **M1** | Backend turn API + Gemini client | Auth’d turns, tenancy, AI-off path, unit tests | backend-engineer, ai-llm-engineer | IP-03, IP-04, IP-06, IP-08 (API) |
| **M2** | FE route + chat + scorecard | `/interview-prep`, CV gate, modes, chat, scorecard UI | frontend-engineer, ui-ux-designer | IP-02, IP-05, IP-07, IP-08, IP-10, IP-11 |
| **M3** | Optional job targeting | Picker + job-detail deep-link | frontend-engineer, backend-engineer | IP-09 |
| **M4** | QA evidence | Authz, no CV mutate, profession-agnostic smoke | qa-engineer | (validation across MVP) |
| **M5** | Full-loop stretch | Multi-round orchestration | ai-llm, frontend | IP-12 (P2 — may slip to Later) |
| **Later** | History / calendar nudge / notes | Durable sessions, interview-date CTA, prep notes | backend, frontend | IP-13, IP-14, IP-15 |

**Suggested ship cut:** M0–M4 (+ IP-11 language). IP-12 optional. IP-13–15 backlog only.

---

## 5. Proposed GitHub issues (file only after approval)

| ID | Title | Horizon |
|---|---|---|
| IP-01 | Architecture: Interview Prep session & scoring contracts | MVP P0 |
| IP-02 | Structured CV gate + `/interview-prep` route shell | MVP P0 |
| IP-03 | Backend: assemble prep context (CV ± optional scrape job) | MVP P0 |
| IP-04 | Profession-agnostic role / seniority / style inference | MVP P0 |
| IP-05 | Mode catalog UI + conditional Process & systems | MVP P0 |
| IP-06 | Mock interview chat-turn API (Gemini HTTP, ephemeral) | MVP P0 |
| IP-07 | Chat-style mock interview UI | MVP P0 |
| IP-08 | Round scorecard | MVP P0 |
| IP-09 | Optional saved-job picker + deep-link from job detail | MVP P1 |
| IP-10 | Shell nav + empty / error / AI-off states | MVP P1 |
| IP-11 | Language practice mode (EN / DA) | MVP P1 |
| IP-12 | Full-loop mode | MVP P2 stretch |
| IP-13 | Session history | Later |
| IP-14 | Calendar interview nudge → prep | Later |
| IP-15 | Save prep notes | Later |

**When filing (Principal only, after human approval):** labels `enhancement` + `needs-triage`; bodies from `issue-bodies/IP-XX.md`.

---

## 6. Dependencies & risks

| Risk | Mitigation |
|---|---|
| Model defaults to software interviews | System prompt + QA checklist with non-tech CV fixtures |
| Prompt size / cost with full CV + long JD | Truncation caps in `InterviewPrepAiOptions`; compact `priorTurns` |
| Tenancy leak via `scrapeResultId` | Always load by `(id, user.Id)`; 404 on miss |
| Scope creep into CV Assist | Dedicated controller; no structured PUT from prep |
| Mode enum drift FE/BE | Freeze in IP-01 ADR / contract before M1 |

---

## 7. Approval gates

1. **This plan** — human approves (or revises) scope / milestones  
2. **Backlog filing** — human chooses: **all IP-01…15** / **MVP only (IP-01…11 or …12)** / **custom subset** → then Principal files GH issues  
3. **Implementation** — operate **C** on next unblocked milestone (starts IP-01 / M0 ADR) after issues exist or with explicit D authorization  
4. **ADR write** — on M0 accept (per workspace ADR rule)

### Open questions for human (from PM)

1. Confirm ephemeral MVP (no session DB) — recommended **yes**  
2. Ship IP-12 (full loop) in first cut, or Later?  
3. Scorecard: fixed agnostic rubrics vs model-generated criteria? (Arch lean: fixed dimension ids + model notes)  
4. Deep-link param: `jobId` (recommended) vs `savedJobId`  
5. IP-15 later coordination with saved-job notes (#44) — defer until Later

---

## 8. Completion criteria (plan option A)

- [x] Product backlog + issue bodies drafted  
- [x] Architecture design + milestones documented  
- [x] Human approval recorded (**all**)  
- [x] GitHub issues filed: `#67`–`#81` (`enhancement` + `needs-triage`)  
- [ ] ADR accepted (M0 / IP-01)  
- [ ] Implementation (operate **C** / **B**)

### Filed issues

| ID | GitHub |
|---|---|
| IP-01 | [#67](https://github.com/PrimusInterParess/ApplyVault/issues/67) |
| IP-02 | [#68](https://github.com/PrimusInterParess/ApplyVault/issues/68) |
| IP-03 | [#69](https://github.com/PrimusInterParess/ApplyVault/issues/69) |
| IP-04 | [#70](https://github.com/PrimusInterParess/ApplyVault/issues/70) |
| IP-05 | [#71](https://github.com/PrimusInterParess/ApplyVault/issues/71) |
| IP-06 | [#72](https://github.com/PrimusInterParess/ApplyVault/issues/72) |
| IP-07 | [#73](https://github.com/PrimusInterParess/ApplyVault/issues/73) |
| IP-08 | [#74](https://github.com/PrimusInterParess/ApplyVault/issues/74) |
| IP-09 | [#75](https://github.com/PrimusInterParess/ApplyVault/issues/75) |
| IP-10 | [#76](https://github.com/PrimusInterParess/ApplyVault/issues/76) |
| IP-11 | [#77](https://github.com/PrimusInterParess/ApplyVault/issues/77) |
| IP-12 | [#78](https://github.com/PrimusInterParess/ApplyVault/issues/78) |
| IP-13 | [#79](https://github.com/PrimusInterParess/ApplyVault/issues/79) |
| IP-14 | [#80](https://github.com/PrimusInterParess/ApplyVault/issues/80) |
| IP-15 | [#81](https://github.com/PrimusInterParess/ApplyVault/issues/81) |

Also: `agent-system/handoffs/archive/interview-prep-2026-08-02/created-issues.json`

---

## Next operate step

Recommend **C** (implement next milestone) starting **M0 / IP-01** (ADR + contracts), or **B** if you want a full repository task mapping first. Plan-task Close done (`REQUEST COMPLETE`).
