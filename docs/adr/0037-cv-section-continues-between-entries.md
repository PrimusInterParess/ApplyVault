# ADR-0037: A Section continues between Entries

## Status

Accepted (2026-10-19 — human correction after the canvas ran to three pages)

Refines [ADR-0034](0034-cv-section-placement.md) and [ADR-0036](0036-cv-builder-canvas-section-placement.md) decision 1. A fitting Entry still stays whole. A Section no longer moves as one block when it has more than one Entry.

## Context

Moving a whole Section onto the next page left a large empty tail and pushed a CV that fit on two pages onto a third. Opening that leftover space up between Entries made the page look sparse. The seeker wants the Section to continue, the Entries kept whole, and the space between content kept even. The side inset should be the same on the left and the right.

## Decision

1. **A Section continues onto the next page between Entries.** The heading stays with the next Entry when that lead fits on one page. An Entry is not split. A taller Entry still continues between bullets, and each bullet stays whole.
2. **The space between content stays at the normal rhythm.** The unused remainder stays at the bottom of the page. It is not opened up between Entries.
3. **The CV builder side inset is equal** on the left and the right.
4. **The Template follows the same split.** Print CSS lets a Section continue and keeps each Entry whole. One page remains the aim and two remains the maximum the layout tries to meet.

## Consequences

- A page can end part-way through a Section, after a whole Entry.
- A single Entry that does not fit in the room left still moves with its heading, because the Entry stays whole.
- Rejected: moving every fitting Section whole, and opening the leftover space up between Entries.
