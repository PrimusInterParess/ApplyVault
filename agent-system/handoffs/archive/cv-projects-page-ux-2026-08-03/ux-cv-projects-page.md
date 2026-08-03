# UX note — CV Projects page (`/cv-projects`)

**Agent:** ui-ux-designer  
**Task:** `cv-projects-page-ux-2026-08-03`  
**Status:** READY for frontend-engineer  
**Visual check:** code audit only (live `localhost:4200` not verified this run)

---

## 1. Executive summary

The Projects surface already has the right pieces (GitHub gate, browse/saved modes, list/detail, generate/remove CTAs). It reads as misaligned and cluttered because the page stacks too many bordered cards, the list-card header does not follow the calm Jobs card grid, and browse vs saved controls are not framed as one workspace toolbar. Tighten hierarchy to match Saved jobs: quiet page title → optional connect banner **or** mode toolbar → list/detail workspace.

## 2. Scope confirmation

| In scope | Out of scope |
| --- | --- |
| `cv-projects-page` HTML/SCSS/TS presentation | CV builder, API/DTOs, extension |
| Tokens (`--app-*`), a11y/facade/route preservation | New design system / brand rewrite |
| Empty, loading, error, mobile detail states | Changing generate/eligibility logic |

## 3. Verified facts (from code)

- Route `/cv-projects`; facade `CvProjectsFacade` + `GitHubConnectionsFacade`; keyboard list nav present.
- Modes: `browse` | `saved`; mobile list↔detail via `showMobileDetail()` / `--mobile-detail`.
- List cards use monogram + eyebrow + optional Saved badge; selected state adds left accent bar **and** absolute ✓ top-right — conflicts with Saved badge.
- Saved badge uses undefined token `--app-success` (only `--app-success-soft` / `--app-positive` exist in `styles.scss`).
- Hero is centered, max-width `42rem`, bordered card — unlike Jobs’ left-aligned `h1`.
- Stats are two elevated cards (`--app-shadow-md`) above controls, independent of active mode.
- Jobs reference pattern: monogram | title+subtitle | status badge in one header row; quieter surface + `--app-shadow-sm`.

## 4. Assumptions

- User pain (“listings off / sections poorly organized”) maps to list-card header layout + stacked chrome, not missing features.
- Terminology: **Projects** surface; **saved project summaries** / CV-ready copy — align with CONTEXT.md (`Project summary import`, Structured CV). Prefer “Projects” / “saved summaries” over “Portfolio builder” as primary brand chrome.
- No API or facade contract changes required for this UX pass.

## 5. Decisions (implement these)

1. **Page hierarchy:** left-aligned title block → connect banner **OR** (when connected) compact mode toolbar + mode-aware controls → alerts → list/detail workspace.
2. **Drop competing chrome:** remove centered hero card treatment; demote or fold stats into the toolbar/list header (not two heavy metric cards).
3. **Align list cards to Jobs card anatomy:** single header row `monogram | heading | status`; drop selected ✓ ornament; quiet selected border/background only.
4. **One primary CTA per active detail state:** Generate / Regenerate (browse) or Browse repositories (saved empty detail); Settings when disconnected; Remove stays danger secondary.
5. **Token fix:** replace `--app-success` with `--app-positive` (color) + `--app-success-soft` / positive mix for badge surface.

---

## 6. Target page hierarchy

```
<main.cv-projects-page>
  1. Page header (not a card)
     - h1: Projects
     - one muted intro line (optional; keep short)
  2a. IF GitHub disconnected (after connections load):
     Connect banner → primary "Open settings"
  2b. ELSE:
     Workspace chrome (one surface or tight stack):
       - Mode switch: Browse repositories | Saved summaries (count)
       - Mode-specific controls row
         browse: search | forks filter | Refresh repos (secondary)
         saved: short helper copy | Refresh summaries (secondary)
       - Optional inline counts (text, not cards): "N repos · M saved"
     Alerts (generate / repos / summaries errors) — full width under chrome
     Workspace:
       aside list | detail panel
```

**Do not** keep: centered bordered hero, separate stats card grid above the toolbar, or eyebrow “Portfolio builder” as a third title layer.

---

## 7. Listing alignment & card density

### Target list-card structure (mirror Jobs)

```
[ Monogram ]  Title (repo name / cvTitle)     [ Saved? ]
              Subtitle (owner or fullName · date)
Language · Private · Fork   (meta row)
Excerpt (2-line clamp)
```

### Concrete fixes

| Issue | Fix |
| --- | --- |
| Monogram beside eyebrow; title below → ragged columns | Put monogram + heading block + badge on one flex row (`align-items: flex-start`; `min-width: 0` on heading) |
| Selected ✓ overlaps Saved badge | Remove `::after` checkmark; rely on border + soft accent fill + `aria-current` |
| Left 6px accent bar + heavy shadow + gradient | Prefer Jobs: `--app-shadow-sm`, flat `--app-surface`, selected = accent-tinted border/bg; optional thin left bar **or** checkmark — not both |
| Uneven card heights from long excerpts | Keep 2-line clamp; consistent padding `0.85–1rem`; gap between cards `0.65rem` |
| Saved badge token broken | `color: var(--app-positive)`; soft bg from `--app-success-soft` or positive mix |
| Dense chips in list | Meta as muted text or soft pills (`--app-bg-soft`), not competing with detail status chips |

Skeleton cards should match the same header row proportions so loading ↔ loaded does not jump.

---

## 8. Browse vs saved organization

- Mode chips are the **primary** navigation for the page; visually group them with the controls row in one toolbar band (`border: 1px solid var(--app-border)`, `--app-surface`, `--app-radius-lg`, `--app-shadow-sm`).
- Active chip: accent border + text (`--app-accent` / `--app-accent-dark`); inactive: muted, no heavy fill.
- Show browse-only controls only in browse; saved-only copy/refresh only in saved (already gated — keep, just nest inside toolbar).
- Counts: prefer `Saved summaries (N)` on the chip (already) + optional muted inline “Showing N repositories” in list header — **not** a second stats section.
- Cross-mode link “Open in saved view” stays text action inside detail preview (secondary).

---

## 9. Spacing & tokens

Use existing `--app-*` only:

| Role | Token |
| --- | --- |
| Page padding | `2rem 1.5rem 2.5–3rem`; `max-width: var(--app-content-max-width)` |
| Section stack gap | `0.75–1rem` between header → toolbar → workspace |
| Workspace grid | `minmax(280px, 340px) 1fr`; gap `0.75–1rem` |
| Surfaces | `--app-surface`, `--app-border`, `--app-radius` / `--app-radius-lg` |
| Shadows | cards `--app-shadow-sm`; selected/hover `--app-shadow-md` max — avoid `--app-shadow-hover` on every list row |
| Text | `--app-text`, muted `--app-text-muted` / soft `--app-text-soft` |
| Accent / danger | `--app-accent`, `--app-accent-dark`, `--app-danger` |
| Success/saved | `--app-positive` (+ soft mixes); do **not** invent `--app-success` |

Reduce nested “card inside card” in detail: detail panel = one surface; inner README/summary blocks use `--app-bg-soft` + border, not a third shadow level.

---

## 10. States

| State | Behavior |
| --- | --- |
| **Loading connections / GitHub unknown** | Prefer quiet page (no flash of empty workspace); keep existing gate once `!loading && !connected` |
| **Disconnected** | Banner only; no empty list chrome |
| **Browse loading** | Skeleton list + optional quiet detail skeleton (Jobs pattern) |
| **Browse empty** | List empty copy; detail “Select a repository” only when not loading and no selection |
| **Saved empty** | List empty + detail primary CTA “Browse repositories” (one filled primary) |
| **Error** | Alert under toolbar; Retry via existing Refresh / Try again — do not duplicate primary generate |
| **Load more error** | Keep inline in list footer with secondary Try again |
| **Mobile ≤1080** | Preserve list OR detail; back control; full-width detail actions |
| **Reduced motion** | Keep existing `prefers-reduced-motion` (no card transform) |

Preserve: list `tabindex`, arrow key nav, `aria-pressed` on mode chips, `aria-current` on selected cards, `role="alert"` on errors, README `safeHtml`.

---

## 11. Primary vs secondary CTAs

| Context | Primary (one filled) | Secondary / text / danger |
| --- | --- | --- |
| Disconnected | Open settings | — |
| Browse detail, eligible | Generate summary / Regenerate summary | View on GitHub |
| Browse detail, ineligible | none filled (disabled Generate OK) | View on GitHub; notice explains why |
| Browse → saved preview | — | Open in saved view (text) |
| Saved detail | — | View on GitHub; Remove summary (danger) |
| Saved empty detail | Browse repositories | — |
| Toolbar | — | Refresh repos / Refresh summaries; filter chip |

Do not style Refresh as primary. Do not add a second filled button beside Generate.

---

## 12. Acceptance checklist (frontend-engineer)

- [ ] Page header is left-aligned title + short intro; no centered bordered hero / “Portfolio builder” eyebrow card
- [ ] When disconnected: only connect banner + Settings primary CTA
- [ ] When connected: mode switch + mode controls share one calm toolbar band; no separate dual stats cards
- [ ] Browse vs saved controls remain mode-gated; saved chip shows count when > 0
- [ ] List cards: monogram | title/subtitle | badge on one row; consistent left edges across rows
- [ ] Selected state: no ✓ badge; no collision with Saved; quiet accent treatment
- [ ] Card density: softer shadow, consistent padding/gap; 2-line excerpt clamp retained
- [ ] `--app-success` removed; saved styling uses verified tokens
- [ ] Detail: one primary CTA rule per state; Remove remains danger secondary
- [ ] Empty / loading / error / load-more / mobile back+detail still work; keyboard nav + a11y attrs preserved
- [ ] Facade bindings, routes, generate/delete/load handlers unchanged in behavior
- [ ] No new design tokens or API changes

---

## 13. Contracts (FE bindings — immutable)

Preserve `CvProjectsFacade` / `GitHubConnectionsFacade` usage, `/cv-projects` route, workspace mode signals, select/generate/delete/load-more handlers, README sanitization, and existing ARIA/keyboard behavior. Presentation-only changes.

## 14. Security / a11y

No secrets in UI. Keep README via `safeHtml`. Maintain focus-visible outlines, `aria-pressed`, `aria-current`, alert roles, and list region focus for arrow keys.

## 15. Validation

FE implements against this checklist; Principal/QA visual-pass on `/cv-projects` after implementation. This note does **not** claim visual QA or tests passed.

## 16. Risks

- Auto-select first repo/summary may still feel busy on desktop — acceptable; do not change selection effects in this pass unless needed for empty-state clarity.
- Folding stats may feel like “loss of overview” — mitigated by chip count + optional inline text counts.

## 17. Handoffs

→ Principal reconciles → **frontend-engineer** implements SCSS/HTML structure per this note.
---

## Copy guidance (CONTEXT.md)

- Surface name: **Projects**
- Artifacts: **saved summaries** / personal-project CV copy destined for **Project summary import** into the Structured CV
- Avoid inventing “Resume portfolio” or “My CV” as page titles
