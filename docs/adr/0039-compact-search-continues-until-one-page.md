# ADR-0039: Compact search continues until one page

## Status

Accepted (2026-11-10 — human request to fit an existing CV on one page)

Supersedes the early stop in [ADR-0035](0035-cv-section-visibility-persistence-and-compact-step.md) decision 4. The two-page fallback in that decision stays. [ADR-0028](0028-single-cv-template-page-policy.md) still recommends one page and caps the layout at two.

## Context

The compact ramp has five levels. The search returned as soon as it had a two-page fit and one tighter step, so a later level that would land on one page was never rendered. A CV whose second page held only the tail of one Entry plus the closing Sections stayed on two pages.

## Decision

1. **The search keeps walking levels 0–4 until one fits on one page.** The first such level is the one that is used.
2. **When no level fits on one page, the two-page choice is unchanged.** Use one step tighter than the loosest level that still fits on two pages, or that loosest level when the next step exceeds two pages or is the last level.
3. **Entries stay whole, and the empty tail stays at the bottom.** Reaching one page does not pull later Entries upward or add a skills block.

## Consequences

- A CV that can fit on one page at a tighter level does so, with the existing notice that the layout was compacted.
- A CV that cannot fit on one page at level 4 still exports on two pages (or more, with the existing warning) at the same level as before.
- Export renders every level until the one-page fit, so a long CV pays for up to five PDF renders instead of stopping after the two-page choice.
