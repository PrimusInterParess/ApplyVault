# Interview Prep — Mode & language hint presentation

**Task:** `interview-prep-mode-hints-ux-2026-08-02`  
**Owner:** ui-ux-designer → frontend-engineer  
**Surface:** `/interview-prep` setup fieldsets Practice mode + Language mix

## Problem

Always-visible explanations are correct (hover `title` alone was insufficient). Current selected-option treatment looks like an alert/callout:

- accent left border + tinted fill (`.interview-prep__option-hint`)
- uppercase micro-label repeating the chip label (`.interview-prep__option-hint-label`)

That fights the calm density of the same Setup panel (especially Saved job’s plain help + hint).

## Chosen pattern: match Saved job notes

Reuse the **job-help → controls → job-hint** rhythm already on this page.

| Layer | Role | Presentation |
| --- | --- | --- |
| Field help | Section job (what this control is for) | Muted paragraph under legend — keep current copy |
| Chips | Selection | Unchanged interaction / active state |
| Selection note | What the chosen option means | Plain body/muted line under chips — **no** callout box |

### Markup intent (sketch only)

```html
<fieldset …>
  <legend class="interview-prep__legend">Practice mode</legend>
  <p class="interview-prep__option-help">…field help…</p>
  <div class="interview-prep__chips" role="list">…chips…</div>
  @if (selectedMode(); as mode) {
    <p id="mode-hint" class="interview-prep__option-hint" aria-live="polite">
      {{ mode.description }}
    </p>
  }
</fieldset>
```

Same structure for Language mix (`id="language-hint"`).

Do **not** repeat the option label as a separate uppercase block. The active chip already shows the label; the note carries the description only.

Optional (not preferred): if FE wants a tiny lead-in for sighted scanning, use sentence-case inline emphasis matching job-hint (`Practicing for **Company**`), e.g. `<strong>{{ mode.label }}.</strong> {{ mode.description }}` — still no uppercase tracking, no border, no fill.

## Visual / tokens

**Remove / stop using for these notes:**

- `border-left: 3px solid var(--app-accent)`
- tinted `background: color-mix(… accent …)`
- `text-transform: uppercase` + letter-spacing micro-label on the selection note
- any new card, shadow, pill cluster, or glow

**Target styles for `.interview-prep__option-hint`:** align with `.interview-prep__job-hint`

- margin ~`0.75rem 0 0`
- font-size ~`0.9rem`
- line-height ~`1.5`
- color `var(--app-text)` or `var(--app-text-muted)` (prefer muted if description reads as support; body text is OK)
- `max-width: 52ch` may stay for line length
- no padding box unless needed for hit area (prefer none)

**Field help (`.interview-prep__option-help`):** keep muted; drop awkward negative top margin if it fights legend spacing — match `.interview-prep__job-help` (`margin: 0 0 0.75rem`).

## Copy

Keep model strings in `interview-prep.model.ts` as source of truth (ids unchanged per ADR-0012).

**Field help (already good — keep):**

- Practice mode: “Pick the interview style to rehearse. The coach adapts questions to that style — not to a fixed job title.”
- Language mix: “Language the coach uses for questions and feedback. Applies to every practice mode.”

**Selection note:** render `description` only (modes + language mixes). No new API fields. No inventing longer marketing blurbs in this task unless copy is clearly broken.

**Chip `title`:** may remain as progressive enhancement for unselected options; must not be the only explanation for the selected option.

## A11y

- Keep `aria-describedby` on the **active** chip pointing at `mode-hint` / `language-hint`.
- Keep `aria-live="polite"` on the selection note so changes announce without `role="alert"`.
- Preserve `aria-pressed` on chips.
- Do not require removing `title` attributes.
- Facade bindings, mode/language ids, and session disable behavior stay unchanged.

## Out of scope

- Angular implementation in the designer task (done by FE)
- Job-results page hierarchy rule (not this surface)
- New design tokens / design system
- API / ADR-0012 catalog changes
