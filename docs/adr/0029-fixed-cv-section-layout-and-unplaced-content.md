# ADR-0029: Fixed CV section layout and Unplaced content

## Status

Accepted (2026-10-18 — human-approved plan "CV path restructure", Milestone 1)

Refines [ADR-0001](0001-cv-section-schema-catalog.md) (catalog gains `fixedLayout`) and [ADR-0002](0002-cv-builder-sole-surface.md) (Blank CV seeds the full fixed set, server-side). Supersedes ADR-0005 binding **P1** ("unmatched lines must persist as `Custom` 'Additional information'"): leftovers now go to Unplaced content, never into a Section.

[ADR-0033](0033-cv-section-visibility.md) refines decisions 1 and 4: Sections stay fixed, and a seeker may omit a Section from the Template without removing it.

## Context

The Structured CV was free-form: any number of Sections in any order, free headings, several Sections per type, and a Structure panel to add/move/remove/rename them. PDF import had to invent Sections for whatever headings a PDF used and forced leftovers into a catch-all "Additional information" Section so nothing was lost. The same rules (starter sections, partition, uniqueness) lived twice, in Angular and in C#.

With a single export Template ([ADR-0028](0028-single-cv-template-page-policy.md)) the product wants one predictable shape for a software developer CV, and an honest place for imported text that does not fit it.

## Decision

1. **Seven fixed, ordered Sections.** Declared once in `shared/cv-section-catalog/cv-section-catalog.json` as `fixedLayout`:
   Contact → Summary → Skills → Experience → Projects → Education → Custom "Certifications & Languages".
   Headings are fixed; users cannot add, remove, reorder or rename Sections. Entries inside a Section stay fully editable.
2. **One normalizer, server-owned.** `FixedSectionLayout.Normalize` maps incoming Sections to slots by Section type (Custom and unknown → last slot), merges same-slot Sections by concatenating Entries (first id kept), creates missing slots, forces heading and sort order. It runs on structured save, on read (lazy heal of legacy data, persisted once), on Blank CV creation and after every AI merge. The Angular edit session applies the same rules only as a client-side safety net.
3. **Unplaced content.** Text the importer cannot place is stored on the CV document (`UnplacedContentJson`, exposed as `unplacedContent: string[]`) and shown in a side drawer beside the canvas for copy/paste and dismissal (`PUT /api/cv-documents/current/unplaced-content`). It is never rendered in the export.
4. **Empty Sections are hidden in the export** (entry content only; a heading alone does not count). The edit canvas always shows all seven.

## Consequences

- Existing CVs are reshaped on first load: custom headings become the fixed ones, duplicate-type Sections merge, Custom Sections such as Awards or Volunteer fold into "Certifications & Languages" (Entries kept, original heading lost). One-way.
- The Structure panel, client-side starter sections and section add/remove/reorder edits are removed from the SPA.
- Import no longer needs to invent Sections; the import redesign is recorded in [ADR-0030](0030-cv-import-ai-parser-contact-only-fallback.md).
- Introducing another layout later means a deliberate catalog change, not reviving free-form Sections.
