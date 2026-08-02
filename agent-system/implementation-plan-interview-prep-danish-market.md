# Implementation plan — Interview Prep Danish hiring-market bias

**Status:** `REQUEST COMPLETE WITH DOCUMENTED LIMITATIONS` — M1 + M1b + M2 + **IP-DK-05** (2026-08-02)  
**ADR:** `docs/adr/0013-interview-prep-hiring-market.md`  


**Task id:** `interview-prep-danish-market-2026-08-02`  
**Author:** principal-software-architect (operate option **A**)  
**Date:** 2026-08-02  
**Approved:** 2026-08-02 — M1 (prompt) + M1b (UI hint copy); Later stubs not filed unless requested  
**Mode:** BROWNFIELD / BRIDGE  
**Spec:** `agent-system/project-specification.md` (APPROVED) — extends Interview Prep (ADR-0012)  
**Library:** 0.5.4 (pack aligned)  
**Depends on:** Interview Prep MVP complete (`implementation-plan-interview-prep.md`, ADR-0012)

**Does not replace** `agent-system/implementation-plan.md` or `implementation-plan-interview-prep.md`.

---

## 1. Intent

Add an **explicit Danish hiring-market coaching bias** to Interview Prep — beyond `languageMix` (EN / DA / mixed) — so seekers targeting Denmark get practice that reflects common DK interview norms (motivation, culture, bilingual loops), while remaining **profession-agnostic** and **not** forcing Denmark on every session.

### MVP success

With a Structured CV and (typically) a Denmark-linked saved job — or `languageMix` of `da` / `mixed` — coach turns include DK-market cues (why this company / Denmark, collaborative culture framing, bilingual comfort) without a new mode id and without changing the frozen request/response contract.

### Explicit non-goals (MVP)

- New mode id (e.g. `danish_market`) or mode-catalog ADR churn  
- New API field (`market`, `locale`, etc.)  
- Always-on “Denmark only” product default for all users/jobs  
- Live coding / LeetCode mode under a DK banner  
- Durable sessions, new UI market toggle (Later)  
- Changing profession-agnostic rules in ADR-0012

---

## 2. Product framing (approved design direction for this plan)

| Lever | MVP | Later |
|---|---|---|
| System prompt DK norms (conditional) | **Yes** | Refine wording from QA |
| Activate when job location / company / JD signals Denmark **or** `languageMix` ∈ {`da`,`mixed`} | **Yes** | Optional server-side heuristic helper |
| Stay market-agnostic when EN-only + no DK job signal | **Yes** | — |
| Default UI `languageMix` → `mixed` when linked job looks DK | No | Optional P2 |
| Explicit UI “Danish market” toggle + request field | **Yes (IP-DK-05)** | ADR-0013 `hiringMarket` |
| Mode chip copy mentioning DK bilingual loops | Optional thin FE polish | If UX asks |

**Grounding already in place (no rebuild):**

- Job context on each turn: `CompanyName`, `JobTitle`, `Location`, `PositionSummary`, `JobDescription` → `{{jobJson}}`  
- `languageMix`: `en` \| `da` \| `mixed`  
- Modes + scorecard frozen under ADR-0012 / `interview-prep-turns`

---

## 3. Technical shape

### Primary change (MVP)

**Owner:** `ai-llm-engineer`  
**File:** `api/ApplyVault.Api/Options/InterviewPrepAiOptions.cs` — `DefaultSystemPrompt`  
**Also sync if present:** `InterviewPrepAi:SystemPrompt` override in `appsettings.example.json` (document only; do not invent secrets).

Add a **conditional** block (conceptual text for approval):

```text
Danish hiring-market bias (conditional — do NOT apply by default):
- Apply ONLY when (a) optional job context clearly indicates Denmark/Danish market
  (location, company, or job text), OR (b) languageMix is da or mixed.
- When applied, prefer coaching cues common in Danish hiring:
  - Motivation: why this role/company, interest in Denmark / relocating / local context when relevant;
    do not invent visa/work-permit facts not in CV or job.
  - Culture: collaboration, constructive feedback, humility, sustainable pace —
    avoid US-style “crush it / hustle” framing unless the job text clearly uses that voice.
  - Language: for languageMix mixed, bilingual EN↔DA switching is normal; for da, prefer Danish;
    for en with DK job signal, keep English but allow DK-market content cues.
  - Process: do not assume LeetCode/live-coding; keep profession-agnostic mode behavior.
- When NOT applied: remain fully market-agnostic (ADR-0012). Never invent employers or DK facts.
```

No controller / DTO / FE contract changes for MVP.

### Optional thin polish (same milestone if approved)

**Owner:** `frontend-engineer` / `ui-ux-designer`  
**Files:** mode/language option `detail` strings in `interview-prep.model.ts`  
One short line that DK bilingual loops are supported via Language practice + Mixed — **not** a new mode.

### Out of MVP (document only)

| Item | Why deferred |
|---|---|
| Server heuristic `IsDanishMarket(job)` | Prompt inference is enough for v1; can harden later |
| Default `languageMix=mixed` on DK job select | UX product decision; needs FE facade change |
| `market: dk \| general` request field | Contract + ADR |

### ADR

- **MVP:** No new ADR. Behavior is an allowed specialization of ADR-0012 (job ± language grounded; still profession-agnostic).  
- **If Later adds a request field or always-on market default:** write next free ADR under `docs/adr/` and update contract registry.

---

## 4. Milestones

| ID | Milestone | Outcome | Primary agents |
|---|---|---|---|
| **M0** | Human approval of this plan | Scope locked (MVP prompt-only ± optional copy) | Principal |
| **M1** | Prompt bias | `DefaultSystemPrompt` updated; example appsettings note if needed | ai-llm-engineer |
| **M1b** | Optional UI copy | Language/mode hint lines mention DK bilingual reality (no new controls) | frontend-engineer, ui-ux-designer |
| **M2** | QA smoke | DK-job + `mixed` shows market cues; EN + non-DK job stays agnostic; profession-agnostic still holds | qa-engineer |
| **Later** | Toggle / auto-mixed | Explicit market control or auto `languageMix` | frontend, backend, architecture |

**Approved ship cut:** M0 → **M1 + M1b** → M2. Later (IP-DK-04/05) not in this cut.

---

## 5. Proposed work items (file as GH issues only after approval)

| ID | Title | Horizon |
|---|---|---|
| IP-DK-01 | Interview Prep: Danish hiring-market system prompt (conditional) | MVP P0 |
| IP-DK-02 | Interview Prep: optional mode/language hint copy for DK bilingual loops | MVP P2 optional |
| IP-DK-03 | QA: DK-market bias smoke (on/off paths) | MVP P0 |
| IP-DK-04 | Later: auto `languageMix=mixed` when linked job looks DK | Later |
| IP-DK-05 | Explicit market toggle + API field + ADR-0013 | **Done** |

**When filing (Principal only, after human approval):** labels `enhancement` + `needs-triage` (or project triage defaults).

---

## 6. Dependencies & risks

| Risk | Mitigation |
|---|---|
| Over-steering non-DK English interviews | Conditional activation only; QA off-path with non-DK job + `en` |
| Inventing visa / relocation facts | Prompt: never invent; only use CV/job evidence |
| Weak/ambiguous location strings (“Remote”, “Nordics”) | Prefer clear DK signals; if unclear, stay agnostic unless `da`/`mixed` |
| Prompt drift vs profession-agnostic rule | Keep “no coding-by-default” and non-tech profession adaptation |
| Operators override SystemPrompt in deployed config | Document that production overrides must include the new block if customized |
| Scope creep into new mode / coding pad | Explicit non-goals |

---

## 7. Approval gates

1. **This plan** — **APPROVED** (2026-08-02)  
2. **Ship cut** — **M1 + M1b** (human chose **2**); Later stubs not included  
3. **Implementation** — operate **C** next (paths known; skip **B**); or **D** if human wants a single combined request without issue filing  
4. **ADR** — only if Later request-field path is chosen later

### Resolved / remaining

| Question | Resolution |
|---|---|
| MVP = conditional prompt bias (no new API field) | **Yes** |
| Include M1b UI copy | **Yes** (ship cut 2) |
| File GitHub issues vs implement directly | **Open** — ask before C/D |
| Extra DK norms (salary, work permit) | Default plan text; no extra bans unless human adds them |

---

## 8. Completion criteria (plan option A)

- [x] Intent, non-goals, and technical shape documented  
- [x] Milestones + risks + approval gates recorded  
- [x] Human approval recorded on this plan (**M1 + M1b**)  
- [x] Implementation via operate **C** (M1 + M1b + M2)  
- [x] Close archived: `handoffs/archive/interview-prep-danish-market-2026-08-02/summary.yaml`  
- [x] IP-DK-05: ADR-0013 + `hiringMarket` API/FE; archive `handoffs/archive/interview-prep-hiring-market-2026-08-02/`

---

## Progress

| Milestone | Status |
|---|---|
| M0 | **DONE** — approved ship cut M1 + M1b |
| M1 | **DONE** — `InterviewPrepAiOptions.DefaultSystemPrompt` DK bias |
| M1b | **DONE** — Language practice + Mixed hint copy |
| M2 | **DONE** — static QA PASS (live Gemini not executed) |
| Later / IP-DK-05 | **DONE** — `hiringMarket` + ADR-0013; archive `handoffs/archive/interview-prep-hiring-market-2026-08-02/` |
| IP-DK-04 | Still backlog (auto `languageMix=mixed` on DK job) |

## Next operate step

Optional: live Gemini smoke (EN + `hiringMarket=dk`). Backlog: IP-DK-04.
