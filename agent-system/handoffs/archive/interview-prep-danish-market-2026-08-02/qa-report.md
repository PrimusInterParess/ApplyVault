# Interview Prep Danish-market — QA smoke (M2)

**Task:** `interview-prep-danish-market-2026-08-02`  
**Agent:** `qa-engineer`  
**Date:** 2026-08-02  
**Evidence mode:** **static / code review only** — no builds, no test runs, no live Gemini  
**Overall:** **PASS**

---

## 1. Executive Summary

M1 prompt + M1b FE copy meet the approved plan gate under static evidence. Conditional Danish hiring-market bias is present and correctly gated; four UX after-strings match exactly; frozen mode ids and turn request/response shape are unchanged. Live coach behavior was **not** executed — residual risk documented.

---

## 2. Scope Confirmation

| In | Out |
| --- | --- |
| `InterviewPrepAiOptions.DefaultSystemPrompt` DK block | Live Gemini / coach turns |
| Four FE strings in `interview-prep.model.ts` vs UX §6 | `*.spec.ts` / unit tests |
| No new mode ids / API request fields | `dotnet` / `ng` build or test |
| Engineer handoffs (ai-llm, frontend, ui-ux) READY | Manual browser smoke |

**Upstream handoffs (dependencies):** `handoff-ai-llm-engineer.yaml` READY (M1); `handoff-frontend-engineer.yaml` READY (M1b); `handoff-ui-ux-designer.yaml` READY (copy source).

---

## 3. Checklist

| # | Check | Result | Evidence |
| --- | --- | --- | --- |
| Q1 | Conditional DK bias present in `DefaultSystemPrompt` | **PASS** | `InterviewPrepAiOptions.cs` L30–43: “Danish hiring-market bias (conditional — do NOT apply by default)” |
| Q2 | Gate: clear DK job signals **OR** `languageMix` da\|mixed | **PASS** | L31–32: apply ONLY when (a) job context clearly indicates Denmark/Danish market (location, company, or job text), OR (b) languageMix is da or mixed |
| Q3 | Ambiguous Remote/Nordics + `en` stays agnostic | **PASS** | L33–34: Prefer clear DK signals; if ambiguous (e.g. “Remote”, “Nordics”) and languageMix is en, stay market-agnostic |
| Q4 | Off-path / when NOT applied → market-agnostic | **PASS** | L43: When NOT applied, remain fully market-agnostic (ADR-0012); never invent employers or DK facts |
| Q5 | Profession-agnostic / no-coding-default still present | **PASS** | L19–28 intact above DK block: no software/leetcode default; process_systems gated by `isTechnicalContext`; never default role to software engineer |
| Q6 | FE four after-strings match `ux-danish-market-hints.md` §6 exactly | **PASS** | See §4 string table |
| Q7 | No new mode ids | **PASS** | `InterviewPrepMode` still seven frozen ids (`screening` … `full_loop`); no `danish_market` |
| Q8 | No new API request fields | **PASS** | FE `InterviewPrepTurnRequest`: mode, userMessage, languageMix?, scrapeResultId?, priorTurns?. API record: Mode, UserMessage, LanguageMix, ScrapeResultId, PriorTurns — no `market` / locale field |
| Q9 | Live coach DK bias behavior | **NOT EXECUTED** | Authorized skip; residual risk §7 |

---

## 4. FE string exact-match (UX §6 after-table)

| Key | Expected (UX §6 After) | Observed (`interview-prep.model.ts`) | Match |
| --- | --- | --- | --- |
| `language_practice.description` | `Best when English, Danish, or switching between them is the hard part.` | L138 | **PASS** |
| `language_practice.detail` | `Use this when you already know the job content, but want cleaner interview phrasing — including bilingual EN↔DA practice common in Danish hiring. The coach focuses on fluency, clarity, and natural answers in the language mix you pick below — with less pressure on deep domain expertise.` | L139–140 | **PASS** |
| `mixed.description` | `The coach mixes English and Danish — common in Danish interviews.` | L168 | **PASS** |
| `mixed.detail` | `Useful when Danish interviews switch between English and Danish. Practice answering comfortably in both.` | L169–170 | **PASS** |

Labels / examples for those options unchanged (per UX §6). Other modes and `en`/`da` left market-agnostic in UI copy.

---

## 5. Verified Facts

- Plan §3 conceptual DK block is reflected in shipped `DefaultSystemPrompt`, plus explicit ambiguous-signal guidance (Remote/Nordics + en).
- `appsettings.example.json` sync: ai-llm handoff notes no SystemPrompt override in example — QA did not re-open that file; accepted from M1 READY notes.
- Contracts: ADR-0012 profession-agnostic + plan non-goals (no new mode/API field) respected in static sources reviewed.

---

## 6. Assumptions

- Prompt text alone is the product mechanism for MVP market bias (no server `IsDanishMarket` helper) — matches plan §3 / out-of-MVP table.
- Static string equality is sufficient for M1b acceptance; selection-note / popover wiring was not re-verified in browser.

---

## 7. Validation evidence status

| Activity | Status |
| --- | --- |
| Static review of `InterviewPrepAiOptions.cs` | **Done** |
| Static review of `interview-prep.model.ts` vs UX §6 | **Done** |
| Static review of FE/API turn request shape | **Done** |
| Engineer handoffs read | **Done** (ai-llm, frontend, ui-ux READY) |
| `dotnet` / `ng` build or test | **Not run** (authorized) |
| Live Gemini / coach turn with DK job or da\|mixed | **Not run** (authorized) |

---

## 8. Risks / gaps (residual)

| Risk | Severity | Notes |
| --- | --- | --- |
| Live coach may ignore or over-apply DK cues | Medium | Prompt-only gating; model compliance unproven without a live turn |
| Operators with custom production `SystemPrompt` must merge DK block | Low | Documented in ai-llm handoff; example config does not override |
| Manual UI smoke of mode-hint / language-hint not run | Low | Copy matches source; binding assumed unchanged from prior MVP |

---

## 9. Deliverables

- This report: `qa-report.md`
- Handoff: `handoff-qa-engineer.yaml` (**READY**)

---

## 10. Status

**PASS** — M2 static smoke complete. Evidence mode: **static only**. Residual: live coach behavior not executed.
