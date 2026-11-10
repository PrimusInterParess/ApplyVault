# ADR-0036: CV builder canvas Section placement

## Status

Accepted (2026-10-19 — human-approved grilling session)

Refines [ADR-0034](0034-cv-section-placement.md) so stay-whole Section placement applies on the CV builder as well as the Template. The Template spacing search is unchanged ([ADR-0034](0034-cv-section-placement.md), [ADR-0035](0035-cv-section-visibility-persistence-and-compact-step.md)).

Refines [ADR-0003](0003-cv-builder-edit-canvas-vs-export-preview.md). Check export remains the fidelity view of the download. ADR-0003 is not revoked.

Decision 3's fixed editing size is superseded by [ADR-0040](0040-cv-builder-canvas-fits-one-page.md): the canvas tightens type until the CV fits on one sheet. A CV that still overflows at the tightest scale still stacks further sheets.

Decision 1's whole-Section move is refined by [ADR-0037](0037-cv-section-continues-between-entries.md): a Section continues between Entries, and the leftover space on a page is spread across the content that stayed.

## Context

ADR-0034 decides how the Template places a Section and an Entry, and accepts that a page may end short. ADR-0035 binds the two-page spacing search that produces that short page. ADR-0003 keeps the CV builder as the editing surface and Check export as the view of the download.

The CV builder and the Template were not yet bound to the same stay-whole rule. Editing size and download spacing are different jobs: tightening the canvas to match the download would hide the editing size, and capping the canvas at two pages would hide content the Structured CV still holds.

## Decision

1. **A Section or Entry that fits on one page stays whole** on both the CV builder and the Template. If it does not fit in the room left, it moves to the next page and that page ends short.
2. **A taller Section continues between Entries.** A taller Entry continues between bullets, and each bullet stays whole.
3. **Only the Template tightens spacing on two pages.** The CV builder keeps its editing size. Check export remains the fidelity view of the download.
4. **The CV builder shows every page the content needs.** It does not warn. The two-page warning stays on Check export and download.
5. **A CV that still cannot fit on two pages still downloads** with the existing warning ([ADR-0028](0028-single-cv-template-page-policy.md)). Do not squeeze to the smallest type. Do not refuse the download.
6. **The Template spacing search is unchanged.** The short page on the PDF is accepted Section placement (ADR-0034, ADR-0035). This decision does not retune that search.

## Consequences

- Rejected: fill page 1 by continuing a fitting Section; tighten until the short page is small; make the CV builder use the download's spacing; cap the CV builder at two pages.
- Section visibility stays a separate choice ([ADR-0033](0033-cv-section-visibility.md)). Placement does not omit a Section.
- A bullet taller than one page cannot stay whole; it may split because it has to (ADR-0034).
- No new store, route, or catalog field. `CONTEXT.md` already defines **Section placement** for the CV builder and the Template.
