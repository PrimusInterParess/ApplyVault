# Interview Prep — Danish-market hint copy (M1b)

**Task:** `interview-prep-danish-market-2026-08-02`  
**Owner:** ui-ux-designer → frontend-engineer  
**Surface:** `/interview-prep` Practice mode + Language mix  
**File:** `frontend/applyvault-jobs-ui/src/app/features/interview-prep/models/interview-prep.model.ts`

## 1. Executive summary

Minimal string updates so **Language practice** + **Mixed** signal that bilingual EN↔DA loops (common in Danish hiring) are supported. No new chips, toggles, modes, or layout.

## 2. Scope confirmation

| In | Out |
| --- | --- |
| `language_practice` description + detail | New mode / market toggle |
| `mixed` description (+ light detail tighten) | `en` / `da` (already clear) |
| FE applies strings in model.ts only | Template / SCSS / facade changes |
| Preserve plain selection-note pattern | Prompt / API / ADR work (M1 owns prompt) |

## 3. Verified facts

- Selection note under chips renders **`label` + `description` only** (`mode-hint` / `language-hint`).
- Chip popover renders **`description` (lead) + `detail`** (+ `example` for modes).
- `mixed.detail` already mentions Denmark; **`mixed.description` does not** — so the always-visible note under-sells the DK bilingual case.
- Frozen mode / language ids stay unchanged (ADR-0012).

## 4. Assumptions

- Seekers discover DK bilingual support via existing Language practice + Mixed — not a dedicated market control (plan M1b / non-goals).
- Screening / behavioral motivation & culture cues stay prompt-side (M1); UI copy only clarifies language loops.
- Calm, short, one job per string — match prior `interview-prep-mode-hints-ux` tone.

## 5. Decisions

1. Put the DK bilingual signal in **`description`** for both options so selection notes carry it without hover.
2. Reinforce once in **`detail`** for popover readers; do not lengthen into marketing.
3. Leave labels, field help, `en`/`da`, and other modes unchanged.
4. Leave `language_practice.example` unchanged (already covers EN / DA / mixed).

## 6. Exact string proposals

### A. Mode `language_practice`

| Field | Before | After |
| --- | --- | --- |
| `label` | `Language practice (EN / DA)` | *(unchanged)* |
| `description` | `Best when English or Danish is the hard part.` | `Best when English, Danish, or switching between them is the hard part.` |
| `detail` | `Use this when you already know the job content, but want cleaner interview phrasing. The coach focuses on fluency, clarity, and natural answers in the language mix you pick below — with less pressure on deep domain expertise.` | `Use this when you already know the job content, but want cleaner interview phrasing — including bilingual EN↔DA practice common in Danish hiring. The coach focuses on fluency, clarity, and natural answers in the language mix you pick below — with less pressure on deep domain expertise.` |
| `example` | `Shorter interview answers in English, Danish, or mixed — with feedback on clarity and phrasing.` | *(unchanged)* |

### B. Language mix `mixed`

| Field | Before | After |
| --- | --- | --- |
| `label` | `Mixed (EN + DA)` | *(unchanged)* |
| `description` | `The coach mixes English and Danish.` | `The coach mixes English and Danish — common in Danish interviews.` |
| `detail` | `Useful for bilingual interviews in Denmark, where the conversation may switch languages. Practice answering comfortably in both.` | `Useful when Danish interviews switch between English and Danish. Practice answering comfortably in both.` |

### C. Do not change

- Modes: `screening`, `behavioral`, `role_domain`, `problem_solving`, `process_systems`, `full_loop`
- Language: `en`, `da`
- Field help under legends
- Chip / hint markup and tokens

## 7. Acceptance notes (frontend-engineer)

1. Replace only the four after-strings above in `INTERVIEW_PREP_MODES` / `INTERVIEW_PREP_LANGUAGE_MIXES`.
2. Keep ids, labels (except where marked unchanged), examples, and other options as-is.
3. No HTML/SCSS/component changes for this milestone.
4. Manual check:
   - Select **Language practice** → `mode-hint` reads the new description (mentions switching).
   - Hover Language practice → popover detail mentions bilingual EN↔DA / Danish hiring.
   - Select **Mixed** → `language-hint` mentions Danish interviews.
   - Hover Mixed → popover detail stays short and DK-switch focused.
5. Confirm EN-only / DA-only copy still market-agnostic in the UI (no forced Denmark default).

## 8. Contracts / bindings

- Facade mode / languageMix ids unchanged.
- `aria-describedby` / `aria-live` on hints unchanged.
- No API or ADR impact.

## 9. A11y / security

- Copy-only; preserve existing a11y wiring.
- No secrets; no dark patterns; no new destructive actions.

## 10. Validation

- UX acceptance = strings match this table exactly; selection notes show the DK bilingual cue without new UI chrome.
- Visual / QA smoke of coaching bias remains M2 / prompt M1 — not this handoff.

## 11. Risks

| Risk | Mitigation |
| --- | --- |
| Over-claiming “Denmark-only” product | Copy says “common in Danish …” — not a forced market mode |
| Selection note too long | Descriptions stay one calm sentence |
| Scope creep into market toggle | Explicitly out; Later in plan |

## 12. Handoffs

- Implement: frontend-engineer via this note
- Prompt bias: ai-llm-engineer (M1, separate)
- Status: READY
