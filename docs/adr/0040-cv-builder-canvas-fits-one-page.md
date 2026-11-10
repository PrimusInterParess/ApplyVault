# ADR-0040: The CV builder canvas fits on one page

## Status

Accepted (2026-11-10 — human request that the editor layout match a one-page export)

Supersedes the fixed editing size in [ADR-0036](0036-cv-builder-canvas-section-placement.md) decision 3. Check export remains the fidelity view of the download ([ADR-0003](0003-cv-builder-edit-canvas-vs-export-preview.md)).

## Context

The export compact search now keeps tightening until a CV fits on one page. The editor kept its full type size and stacked a second sheet, so the page being edited did not match the download.

## Decision

1. **The edit canvas uses the export type scales, then one tighter step.** It applies the first scale whose content fits on one A4 sheet. The scales are 1, 0.96, 0.92, 0.86, 0.8, and 0.74.
2. **A CV that still overflows at 0.74 stacks further sheets.** Entries stay whole, and a taller Entry still continues between bullets.
3. **The photo stays at or above 96px.** Side insets stay equal and shrink with the scale.

## Consequences

- A CV that exports on one page is one sheet in the editor, with smaller type than the full editing size.
- A CV that cannot fit at the tightest scale still shows every sheet it needs. The editor does not warn.
- Rejected: leaving the editor at full size while only the PDF compacts, and scaling the sheet with a zoom that also shrinks the editing controls.
