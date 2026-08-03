# UX brief — Job-search filter control placement (slice 2 / #6)

**Task:** `job-search-filters-slice2-2026-08-03`  
**Agent:** `ui-ux-designer`  
**Surface:** `frontend/applyvault-jobs-ui/src/app/features/job-search/pages/job-search-page/`  
**Mode:** Design only — BRIDGE; no application code  
**Tokens:** Prefer `--app-*` from `frontend/applyvault-jobs-ui/src/styles.scss`  
**Linked:** #6 (this slice) · #15 (IA preserved) · human accept 2026-08-03

---

## 1. Executive Summary

Place the accepted #6 discovery controls **without reopening** the post-#15 hierarchy: hero → filters (compose + one filled Search) → suggestions → **one** results summary chrome → list|detail.

**Locked placement:**

| Control | Where | Boards |
|---------|-------|--------|
| Sort | Results chrome | EURES only |
| Page size | Results chrome | EURES + Jobnet |
| Publication period | EURES filter row | EURES only |
| Schedule | EURES filter row | EURES only |

Multi-keyword keeps forced Best match with **auto-select + helper** (no silent “Most recent” lie). No occupation/contract chrome. No second admin panel. No dead controls.

---

## 2. Scope Confirmation

| In | Out |
|----|-----|
| Control placement, labels, states, mobile, a11y for accepted knobs | App/API code; builds; GitHub edits |
| Preserve #15 section jobs + one summary + one Search CTA | Reopen #15 IA / Remote·tags / Jobnet country honesty |
| Honest multi-kw sort UX | Occupation picker; contract facet |
| Jobnet: pageSize only in results chrome | Jobnet sort / period / schedule / WiD / radius UI |

---

## 3. Verified Facts

| Fact | Evidence |
|------|----------|
| Human accepted ship set + placement seed | `human-acceptance.yaml` |
| Post-#15 stack: hero → filters → suggestions → one summary + controls → list\|detail | Live `job-search-page.component.html` |
| Filters: Keywords + Country (EURES) / Board context (Jobnet); one filled Search | Same HTML |
| Results: `{Provider} results` + visible summary; controls = last-searched + Refresh | Same HTML |
| Product: sort+pageSize results chrome; period+schedule EURES filter row | `product-filters-brief.md` D6 + accept |
| Multi-kw → `BEST_MATCH` with auto-select + helper | Accept + product D5 |
| Arch: extend existing request/URL; no new facet API | `architecture-filters-design.md` |
| Prior UX forbade inventing sort/page-size until wired | Archived `ux-job-search-redesign.md` D4/Q3 — now unblocked |

---

## 4. Assumptions

- **A1:** Wire enum codes (period/schedule) are locked by BE before FE ships labels; UI copy below is seeker-facing and stable.
- **A2:** Filter-row facets (Published, Schedule) **compose** with Keywords/Country and apply on **Search** (same as #15 compose job).
- **A3:** Results-chrome knobs (Sort, Per page) **apply immediately** on change (re-search page 1) — same class of action as Refresh, not a second filled CTA.
- **A4:** Default page size **10**; omit default URL params per product D4.
- **A5:** Existing `--app-*` select/input styles in the filter row / secondary button language are reused — no new component library.

---

## 5. Decisions

### D1 — Hierarchy unchanged (desktop)

```
[ Hero: source · title · intro ]
[ Filters: keywords · country|board · Published+Schedule (EURES) · Search ]
[ Suggestions: collapsible helpers ]
[ Results chrome: H2 · ONE summary · last-searched · Refresh · Sort · Per page ]
[ List | Detail ]
```

Do **not** add a second bordered “Filters & facets” card. Do **not** duplicate the summary into the controls row.

### D2 — EURES filter row (compose)

**Order (LTR):** Keywords → Country → **Published** → **Schedule** → (actions row) **Search**.

| Control | Visible label | Options (UI) | Default | Apply when |
|---------|---------------|--------------|---------|------------|
| Published | `Published` | Any · Last week · Last month · Last 3 months | Any | Search |
| Schedule | `Schedule` | Any · Full-time · Part-time | Any | Search |

- Native `<select>` matching Keywords/Country control styling.
- **Any** = unset/null outbound; omit `published` / `schedule` from URL.
- Changing Published/Schedule alone does **not** fire search until Search (or Enter from keywords, existing path).
- Search label stays `Search` / `Update results` per existing `searchActionLabel()` — still the **only filled primary** in the filters section.
- Hint under Search (existing pattern) may mention narrowing by published/schedule when useful; do not add a second CTA.

**Jobnet filter row:** unchanged — Keywords + Board context only. **No** Published/Schedule.

### D3 — Results chrome (orient + refine listing)

**Structure (single chrome band):**

1. **Header block** (keep): H2 `{Provider} results` + **one** visible `resultsSummary()` (not repeated in controls).
2. **Controls row** (extend, still one `aria-label="Results controls"`):

```
[ last-searched text ]  [ Refresh results (ghost/outline) ]
[ Sort (EURES) ]        [ Per page (both) ]
```

| Control | Visible label | Options (UI) | Wire (FE→API) | Default | Boards |
|---------|---------------|--------------|---------------|---------|--------|
| Sort | `Sort` | Most recent · Best match | `MOST_RECENT` · `BEST_MATCH` | Most recent (single kw) | EURES only — **omit node on Jobnet** |
| Per page | `Per page` | 5 · 10 · 20 | `resultsPerPage` | 10 | Both |

- Sort / Per page: compact labeled `<select>`s on the **right** of the controls row on desktop; stack under Refresh on narrow widths (see D6).
- On change → re-run search at page 1; clear load-more append state (arch/FE contract).
- Refresh remains ghost/outline secondary — **not** filled.
- Visually hidden `aria-live` summary may stay; do not add a third visible summary string.
- No page-index control in URL (load-more stays as today).

### D4 — Multi-keyword sort honesty

When `keywords.length >= 2` (EURES):

1. **Auto-select** Best match (set `sortSearch` / URL `sort` to `BEST_MATCH` if not already).
2. Sort `<select>`: Best match selected; **Most recent** disabled (or select disabled with value Best match — prefer options: Most recent `disabled`, Best match selected).
3. Helper text immediately under Sort (or `aria-describedby` on the Sort control):

   > Multiple keywords use best match.

4. When keywords drop back to 0–1, re-enable Most recent; do **not** force-revert the user’s prior single-kw choice unless product later asks — preferred: keep current `sortSearch` if still valid; if was forced Best match and user never chose, restore Most recent when returning to single keyword **after** a multi-kw search only if sort was auto-forced this session (FE may simplify: restore Most recent when leaving multi-kw). **Minimum:** never show Most recent as active while ≥2 keywords apply.

Do **not** use a blocking banner that steals attention from results; helper under Sort is enough.

### D5 — Source switch

| From → To | UI |
|-----------|-----|
| EURES → Jobnet | Hide Sort, Published, Schedule; keep Per page; strip EURES-only URL keys |
| Jobnet → EURES | Show Sort + Published + Schedule at defaults (or restored URL if present) |

Board context / Country honesty from #15 unchanged.

### D6 — Mobile (≤ existing ~1080 breakpoint)

```
Hero (compact)
Filters stacked: Keywords, Country|Board, Published, Schedule, Search
Suggestions (collapsed after search — existing)
Results: H2 + one summary
Controls stacked:
  last-searched + Refresh
  Sort (EURES) + Per page (full width selects OK)
List XOR Detail (+ back) — #15 focus rules stay
```

- Do not put Sort/Per page into a sticky floating bar.
- Do not show list and detail stacked.
- Filter selects use full-width stack (existing `.eures-page__filter-row` mobile behavior).

### D7 — Empty / loading / error / pre-search

| State | New controls |
|-------|----------------|
| Pre-search onboarding | No Sort/Per page chrome (results chrome absent). Published/Schedule visible in EURES filter row as compose fields. |
| Initial loading skeleton | Optional: keep last chrome if replacing prior results; else hide Sort/Per page until results header mounts — prefer **show chrome once `hasSearched()`** even during refresh so values remain editable. |
| Error | Keep filter row values; results chrome may hide with workspace (match today). Filled CTA = Try again. |
| Empty results (0) | Keep filter row; **show** results header + chrome (summary “0…”) so user can change Sort/Per page / go edit Published+Schedule + Search — avoid trapping user with only “View saved jobs”. If FE keeps today’s empty state without chrome, at minimum ensure filter-row facets remain editable + Search — **preferred UX:** empty state **below** the same one-summary chrome so Per page/Sort still reachable on EURES. |
| Load-more footer | Unchanged; page size change resets to page 1 (no conflict). |

### D8 — CTA rules (preserve #15)

| Section | Filled primary | Secondary |
|---------|----------------|-----------|
| Filters | **Search** / Update results | — |
| Results chrome | none | Refresh (outline); Sort/Per page are controls not CTAs |
| Empty / error | one recover action (existing) | — |
| Detail | Save / View saved (existing #15) | Open listing |

---

## 6. UX deliverables (copy & states cheat-sheet)

### Labels (exact)

| UI label | Role |
|----------|------|
| Published | EURES publication period |
| Schedule | EURES work schedule |
| Sort | EURES result order |
| Per page | Page size both boards |
| Any | Neutral “no filter” for Published & Schedule |
| Most recent | Sort option |
| Best match | Sort option |
| Last week / Last month / Last 3 months | Published options |
| Full-time / Part-time | Schedule options |
| Multiple keywords use best match. | Sort helper |

### States

| Control | Idle | Active non-default | Disabled / forced | Invalid URL value |
|---------|------|--------------------|-------------------|-------------------|
| Published | Any | Selected bucket | — | Reset to Any + inline validation near filters (clear copy; do not invent board behavior) |
| Schedule | Any | Full-time or Part-time | — | Same |
| Sort | Most recent | Best match | Multi-kw: Best match selected; Most recent disabled + helper | Reset to default allowlist value + message |
| Per page | 10 | 5 or 20 | — | Clamp/reset to 10 |

### Visual weight

- Filter-row Published/Schedule: same weight as Country (form fields).
- Results Sort/Per page: quieter than Search; align with Refresh (meta controls).
- No new pill clusters, no purple/glow, no card-in-card around results knobs.

---

## 7. Contracts (FE bindings)

Preserve and extend (do not remove):

- Facades / URL sync for `sort`, `pageSize`, `published`, `schedule` (product D4).
- Source tablist, list keydown, mobile detail focus, live region pattern.
- Routes; one Search primary in filters.
- `#15` honest Jobnet board context (no disabled Country).
- No Remote/tags chrome reintroduction.
- Contract/occupation controls **must not** appear this slice (even if arch doc once listed offering codes — human deferred).

Binding expectations for implementers:

- EURES `buildSearchRequest` sends sort + pageSize + period + schedule when set.
- Jobnet request sends pageSize only among new knobs.
- Changing Sort/Per page → search page 1 immediately.
- Changing Published/Schedule → dirty compose until Search (same as Country).

---

## 8. Security / a11y notes

- All new fields: associated `<label>` (visible) + native `<select>`; helper uses `aria-describedby` on Sort when multi-kw.
- Results controls region keeps a single `aria-label="Results controls"`.
- Do not announce Sort/Per page changes with a second competing live region; rely on existing results summary live update after search returns.
- Focus: changing Sort/Per page should not steal focus into detail; keep focus on the control.
- Mobile select→detail focus (#15) unchanged.
- Allow-list server-side; UI only offers closed options — no free-text facet fields.
- Untrusted listing HTML path unchanged.

---

## 9. Validation (acceptance notes for FE / QA)

1. EURES filter row shows Published + Schedule; Jobnet does not.
2. EURES results chrome shows Sort + Per page; Jobnet shows Per page only.
3. Only **one** non-sr-only results summary string when results visible.
4. Filters section still has exactly **one** filled primary = Search / Update results.
5. Multi-kw (≥2): Best match selected; Most recent not choosable; helper visible.
6. Published/Schedule default Any; changing them requires Search to apply.
7. Sort/Per page change re-runs search without requiring Search click.
8. URL round-trip restores non-default shipped params; EURES-only keys absent on Jobnet.
9. No occupation/contract/WiD/radius controls.
10. Tokens stay `--app-*`; no new design system.

---

## 10. Risks

| Risk | Mitigation |
|------|------------|
| Filter row feels crowded with 4 fields on desktop | Keep one row with existing grid; stack on mobile; do not add a second card |
| Users expect Published to apply instantly | Match Keywords/Country compose model; optional future “auto-search” out of scope |
| Empty state without chrome traps facet edits | Prefer empty-under-chrome (D7) |
| Wire label mismatch (BE codes) | FE uses closed UI labels; map via constants from BE allowlist |
| Relevance still “off” (#7) | Filters help discovery; do not claim ranking fix |

---

## 11. Handoffs

| To | Ask |
|----|-----|
| **frontend-engineer** | Implement placement per D2–D7 after BE wires params; reuse existing select/filter styles |
| **qa-engineer** | Exercise §9 checklist (EURES + Jobnet, multi-kw, URL, mobile) |
| **Principal** | Feed into `/to-spec` #6 if still deepening AC; no app code from this agent |

Artifacts:

- This brief: `agent-system/handoffs/active/job-search-filters-slice2-2026-08-03/ux-filters-placement.md`
- Thin YAML: `agent-system/handoffs/active/job-search-filters-slice2-2026-08-03/handoff-ui-ux-designer.yaml`

---

## 12. Status

**READY** — placement locked to human accept; actionable for `frontend-engineer` after API/URL wiring. No open product placement questions; residual BE wire-code confirmation is outside UX ownership.
