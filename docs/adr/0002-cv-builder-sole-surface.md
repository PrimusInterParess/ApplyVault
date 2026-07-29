# ADR-0002: CV builder as sole surface; Template vs content sources

## Status

Accepted

## Context

We needed one clear home for creating and editing a Structured CV. Blank starters, PDF import, and export Templates were easy to confuse, and `/my-cv` vs `/cv-builder` split the same domain across two UIs.

## Decision

- The **CV builder** is the sole end-state CV surface (Templates, Blank/PDF entry, edit, export, AI, structure). `/my-cv` redirects or is removed once structure ops live on the builder.
- A **Template** is export layout only. Choosing one with an existing Structured CV changes presentation, not content.
- **Content sources:** Blank CV (starter Sections) only when no Structured CV exists; **PDF import** creates or replaces the Structured CV (replace requires confirmation). One CV document per user.
- **Ship in phases:** Phase 1 — PDF upload + Template-fills-saved-data + returning users open edit — while `/my-cv` keeps structure ops. Phase 2 — structure on builder, then retire `/my-cv`.

## Consequences

- Nav and deep links should treat the builder as primary; do not add a second “start blank” wipe path once a Structured CV exists.
- Retiring `/my-cv` before structure-on-builder would leave users unable to reshape Sections/Entries.
