# UX brief — Public job search redesign

**Task:** `job-search-ux-redesign-2026-08-03`  
**Agent:** `ui-ux-designer`  
**Surface:** `frontend/applyvault-jobs-ui/src/app/features/job-search/`  
**Mode:** Discuss/design only — incremental; no new design system  
**Tokens:** Prefer `--app-*` from `frontend/applyvault-jobs-ui/src/styles.scss`  
**Linked issues:** #6 (filters / dead fields), #12 (source chrome), #15 (results chrome / mobile focus)

---

## 1. Executive summary

Public job search feels “off” because **too many jobs share one filter card**, **results chrome repeats the same summary three ways**, **Jobnet still wears EURES filter clothing**, and **cards/detail advertise `remote` / `tags` the providers never fill**. Fix hierarchy and honesty first; keep routes, facade bindings, and existing a11y patterns.

`job-results` workspace pattern (header → attention → toolbar → list/detail) **does not map 1:1** to search: search has a discovery/filter phase before the workspace. Reuse its **calm list/detail**, **one filled primary CTA per state**, and **token language** — do not force an attention panel onto search.

---

## 2. Scope confirmation

| In | Out |
|----|-----|
| Job-search page IA, section jobs, CTAs | New design system / brand rewrite |
| Listing card + detail honesty | API filter payload authorship (note UX needs for #6) |
| Source toggle + Jobnet filter chrome | EURES feature folder, extension, API code |
| Results chrome + mobile detail focus | Saved-jobs Phases 2–4 |

---

## 3. Verified facts (audit)

### Current section stack (`job-search-page.component.html`)

1. **Hero** — source toggle, H1 (`{provider} job search`), intro  
2. **Filters card** — selected keywords, Keywords / Country / Department row, Search CTA, popular IT suggestion chip groups  
3. **Banner / live region / save error** (when applicable)  
4. **States** — skeleton | error | empty | pre-search onboarding | results  
5. **Results** — `Latest jobs` header + summary, then **controls row with the same summary again**, then list/detail workspace  

### Structural problems

| Issue | Evidence | User signal |
|-------|----------|-------------|
| Multiple jobs in filters | Keywords + country + “Department” + Search + large chip taxonomy in one bordered card | “sections not well organized” |
| Dishonest “Department” | Client-only `activeSuggestionGroup`; does not change API query | Misleads (#12) |
| Jobnet EURES clothing | Disabled Country select “Denmark (Work in Denmark)” | Confusing (#12) |
| Suggestion source no-op | `filterKeywordSuggestionGroupsForSource` returns groups unchanged | Same chips both boards (#12) |
| Triple results summary | Live region + `eures-page__results-header` + `jobs-page__filter-summary` all use `resultsSummary()` | Noise (#15) |
| Dead listing fields | `remote` / `tags` on `ExternalJob*`; not on EURES/Jobnet DTOs; still rendered in card/detail | “listings are off” (#6) |
| Split / inverted CTAs | Detail: filled **Open listing** in header; **Save to ApplyVault** as secondary below chips | Product goal (save) underweighted |
| Naming drift | Page classes mix `eures-page__*` and `jobs-page__*` | Mental model of “saved jobs skin on search” |

### What already works (preserve)

- Source toggle (`role="tablist"`) + facade `source` / URL state  
- List keyboard handling (`handleListKeydown`), list `tabindex="0"`, mobile list↔detail swap (`jobs-page__content--mobile-detail`)  
- Skeleton / error / empty / onboarding states with clear copy  
- Save / retry / load-more secondary patterns  
- `--app-*` token usage on page, cards, detail  

### Rule note

Referenced path `.cursor/rules/job-results-ui-ux.mdc` was **not present** in-repo at audit time. Guidance below follows `ui-ux-designer` purpose + live `job-results` page structure, and calls out search-specific differences.

---

## 4. Assumptions

- A1: Product intent of public search remains **find → read → save to ApplyVault**, not apply-in-place.  
- A2: Until API exposes high-value filters (#6), UI should **not invent** sort/schedule/contract controls that do nothing.  
- A3: Human approves this brief before FE implements (delegation approval gate).  
- A4: Chip “pill” radius may stay for selected keywords / suggestions short-term; avoid **new** pill-cluster chrome on results cards beyond location + source + date.

---

## 5. Decisions (UX recommendations — defaults)

### D1 — Page hierarchy (desktop)

```
[ Hero: source toggle · title · one-line intro ]     ← discover context
[ Filters: query controls · primary Search ]         ← one job: compose query
[ Optional: Suggestions (collapsed or secondary) ]   ← assist keywords only
[ Results chrome: ONE summary · refresh ]            ← orient
[ List | Detail workspace ]                          ← browse + act
```

- Keep max-width / page padding; **tighten hero** (less vertical gap; intro ≤ 1 sentence).  
- Filters card stays; **suggestions leave the same visual “primary form” weight** — either:  
  - **Preferred:** collapsible “Popular searches” below the Search row (default collapsed after a successful search), or  
  - Always visible but separated by a quieter divider and smaller type (no second “admin panel” feel).  
- After `hasSearched()`, consider sticky or compact filter strip (keywords + Search) so list/detail stay in the first viewport — **layout polish only**, no new system.

### D2 — Page hierarchy (mobile, ≤1080 existing breakpoint)

```
Hero (compact)
Filters (stacked; suggestions collapsed by default)
Results chrome (single line)
List  XOR  Detail (+ back)
```

- Preserve existing show/hide of list vs detail.  
- **#15:** On select, move focus into detail (prefer back button or detail heading with `tabindex="-1"` focus) so keyboard/VoiceOver continuity matches visual swap.  
- Do not show both list and detail stacked.

### D3 — One job per section

| Section | Single job | Primary CTA |
|---------|------------|-------------|
| Hero | Know which board you’re on | none (toggle is control, not CTA) |
| Filters | Compose & run search | **Search** / Update results (filled) |
| Suggestions | Add keywords only | none (chips are toggles) |
| Results chrome | Orient + refresh | Refresh = ghost/outline |
| List | Choose a listing | none (selection) |
| Detail | Understand & save | see D5 |
| Empty / error | Recover | one filled action |

### D4 — Listing honesty (#6)

**Remove or hide until populated:**

- Card: `@if (job().remote)` Remote pill  
- Detail: Remote chip; `@for (tag of selectedJob.tags …)`  

**Keep:** title, employer, location (or “Location not specified”), source label, posted date, Saved / In ApplyVault, contractType / workHours **only when non-null from detail DTO**.

**Card meta target (honest, calm):**  
`Location · Source · Posted date` + optional Saved badge. Prefer muted text meta over dense pill rows where easy; if pills stay, max ~3 factual chips.

Do **not** show empty Remote/Tags placeholders.

### D5 — Detail primary CTA rules

| State | Filled primary | Secondary |
|-------|----------------|-----------|
| Unsaved, URL available | **Save to ApplyVault** | Open listing (outline/ghost) |
| Saving | Save disabled / “Saving…” | Open listing |
| Saved / already exists | **View saved job** | Open listing |
| No URL | Save (if canSave) | — |
| Description error | Retry description (secondary unless whole detail failed) | — |

Move Save into the **header action cluster** with Open listing so actions are one job, not split by a chip row.

### D6 — Source toggle + Jobnet chrome (#12)

- Keep segmented toggle in hero; ensure active source is the loudest signal before H1.  
- **Jobnet:** **Hide** Country `<select>` (do not show a disabled EURES control). Replace with static context line under intro or beside filters:  
  **“Board: Work in Denmark · Denmark”** (copy may vary; must not look like an editable country filter).  
- **EURES:** keep Country select as a real filter.  
- Rename **Department** → **Suggestion area** (or fold into suggestions header as “Show chips for…”) and label hint: *Filters the chip list only — does not change the search query.*  
- When product supplies source-specific groups, show different chips / titles (“Popular IT searches” vs Jobnet-appropriate label). Until then, same list OK **if** labeled as client-side helpers, not board facets.

### D7 — Results chrome (#15)

- **One** visible summary string near results (e.g. under “Latest jobs” or in the controls row — not both).  
- Keep `aria-live` summary for AT; visually hide duplicate if needed (`visually-hidden` / sr-only), or drive live region from the same single visible node.  
- Controls row: last-searched + Refresh only.  
- Title “Latest jobs” → prefer **“Results”** or **“{Provider} results”** (less misleading if sorted by relevance later).

---

## 6. Issue mapping

| Issue | UX ask | Brief refs |
|-------|--------|------------|
| **#6** | Remove dead remote/tags UI now; expose sort/page-size only when API+URL wired; Jobnet filter chrome honesty | D4, D6; defer inventing sort UI |
| **#12** | Source-aware suggestions labeling; hide Jobnet country select; Department = chip-area only | D6 |
| **#15** | Dedupe results summary; focus detail on mobile select | D2, D7 |

---

## 7. Contracts (FE must preserve)

- Routes and query params (`source`, keywords, country/location, `selected`)  
- Facades: `JobSearchFacade` / source switching, search, select, save, load-more, detail retry  
- A11y: source `tablist`/`tab`, list keydown, live regions, `aria-current` on selected card, mobile back  
- No requirement to remove skeleton / error / empty states  

---

## 8. Security / a11y notes

- Untrusted description HTML stays sanitized via existing description panel path.  
- Focus management (#15) must not trap focus; restore list focus on “Back to list” when practical.  
- Destructive actions: none on this page; Save retries stay explicit.  

---

## 9. Acceptance notes for FE / QA

1. Filters section answers one question: “What am I searching?” Suggestions do not compete as a second form.  
2. Jobnet UI never shows a disabled Country dropdown.  
3. No Remote/Tags UI unless data is present from provider mapping.  
4. With results visible, only **one** non-sr-only results summary appears.  
5. Mobile: select listing → detail visible → focus inside detail; Back returns to list.  
6. Detail unsaved: one filled primary = Save; Open listing is not the filled primary.  
7. Pre-search: one filled primary = Search.  
8. Visual language stays on `--app-*` (warm stone/terracotta product look — no purple/glow drift).  

---

## 10. Risks

- Collapsing suggestions may reduce keyword discovery until users expand once — mitigate with default **open before first search**, **collapsed after**.  
- Promoting Save over Open may surprise users who only browse — acceptable if product goal is capture.  
- Full #6 filter exposure needs BE + PM filter set; shipping honest chrome without fake controls avoids a second “dead UI” cycle.  

---

## 11. Open questions (human / PM)

1. Confirm **Save** as detail primary vs keep **Open listing** primary for browse-heavy users.  
2. Jobnet context: hide country only, or also surface a future WorkInDenmark / radius control when API-ready?  
3. Include **sort / page-size** chrome in the same FE pass as this hierarchy work, or wait until #6 API acceptance is ready?  
4. Source-specific suggestion lists: ship label/copy-only now, or block #12 on new Jobnet chip copy?  

---

## 12. Status

**READY** for principal reconciliation and human approval before `frontend-engineer` implementation. Defaults above are implementable without new tokens or design-system work.
