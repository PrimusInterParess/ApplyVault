# ADR-0034: Section placement

## Status

Accepted (2026-10-19 — human-approved grilling session)

Refines [ADR-0028](0028-single-cv-template-page-policy.md) decision 2. One page remains the aim and two pages remains the maximum. The two-page case no longer stops at the loosest spacing that fits.

The compact-step seam is [ADR-0035](0035-cv-section-visibility-persistence-and-compact-step.md).

The CV builder uses the same stay-whole placement. This ADR's Template spacing search is unchanged. See [ADR-0036](0036-cv-builder-canvas-section-placement.md). A Section continues between Entries, and leftover space is spread on the page ([ADR-0037](0037-cv-section-continues-between-entries.md)).

## Context

The Template tries five spacing steps and takes the first that fits on one page. If none does, it keeps the loosest step that still fits on two. At that step a Section may split anywhere under its heading, so a heading and one Entry sit at the bottom of page 1 and the rest continues on page 2, with large gaps between Sections. Seekers want the Sections whole and the two-page CV less loose, without crushing the type to the smallest step.

## Decision

1. **One page stays the aim.** The first spacing step that fits on one page is still the one used. Two pages is still the maximum. A CV that cannot fit two pages still exports, with the existing warning.
2. **On two pages, tighten only enough.** Prefer a milder tighter step that keeps each fitting Section on one page and shrinks the gap. Do not take the smallest type merely to fill the page.
3. **A Section stays on one page whenever that Section fits.** If it does not fit in the room left on page 1, it moves whole to page 2. A Section taller than one page continues on the next page only between Entries.
4. **An Entry stays whole whenever that Entry fits on one page.** An Entry taller than one page continues between bullets, and each bullet stays whole.

## Consequences

- Page 1 may end short when a whole Section moves to page 2. That gap is accepted.
- A bullet taller than one page cannot stay whole; it may split because it has to.
- Omitting a Section to chase one page is a separate choice ([ADR-0033](0033-cv-section-visibility.md)). Placement does not change Section visibility.
- `CONTEXT.md` defines **Section placement**.
