# ADR-0035: Section visibility persistence and two-page compact step

## Status

Accepted (2026-10-19 — architecture handoff for `/operate` task `cv-section-visibility-placement`, integrated by the Principal Architect)

Decision 4's stop after one tighter step is superseded by [ADR-0039](0039-compact-search-continues-until-one-page.md): the search keeps going until a level fits on one page. The two-page fallback in decision 4 is unchanged when no level fits on one page.

Binds the implementation seams of [ADR-0033](0033-cv-section-visibility.md) and [ADR-0034](0034-cv-section-placement.md). Does not change those product rules.

## Context

ADR-0033 and ADR-0034 say what Section visibility and Section placement mean. They do not say where the flag is stored, or which existing compact step a two-page CV uses. The Interview Prep brief fingerprint is a hash of the structured snapshot, so a careless store would mark every brief outdated when a seeker only omits a Section. Today's structured write DTO is non-nullable; a missing bool would deserialize as false and omit every Section from an old client.

## Decision

1. **`includedOnTemplate` lives on the section row** (`UserCvSections`, `bit NOT NULL` default 1) and on the existing `GET`/`PUT /api/cv-documents/current/structured` section DTO. No new route, table, or catalog field. The write property is nullable: null or omitted means included. Contact is forced included. A slot created because it was missing is included. Blank CV and replacement PDF import start included.
2. **The Interview Prep snapshot projection omits the flag.** Entries of an omitted Section stay in the snapshot. Evaluate CV's payload field list stays as it is. Assist merge copies the previous flag and does not ask the model for it.
3. **Export drops a non-Contact Section when the flag is false**, before the compact search. Empty Sections are still dropped after that.
4. **Two-page spacing is one step tighter than the loosest level that still fits on two pages**, using the existing compact levels 0–4. If the next level exceeds two pages, keep the loosest. A level that fits on one page is still returned immediately. Print CSS keeps a fitting Section and Entry whole and lets a taller Entry break between bullets.

## Consequences

- Existing rows stay included via the column default.
- A visibility-only save does not change the brief fingerprint. A save that also changes Entries does.
- Page 1 may end short. The compact search does not keep tightening to fill that gap.
- The include / omit switch sits on the left section list. An omitted Section leaves the edit canvas until it is included again.
