# ADR-0002: CV builder as sole surface; Template vs content sources

## Status

Accepted (implemented)

## Context

Blank starters, PDF import, and export Templates were easy to confuse, and `/my-cv` vs `/cv-builder` split the same Structured CV across two UIs.

## Decision

- The **CV builder** (`/cv-builder`) is the sole CV surface: Templates, Blank CV / PDF import entry, WYSIWYG edit, Structure ops, AI assist, profile photo, project-summary import, and export.
- A **Template** is export layout only. Choosing one with an existing Structured CV changes presentation, not content.
- **Content sources:** Blank CV (starter Sections) only when no Structured CV exists; **PDF import** creates or replaces the Structured CV (replace requires confirmation). One CV document per user.
- Legacy `/my-cv` redirects to `/cv-builder`; the My CV page was removed after structure, photo, and project import landed on the builder.

## Consequences

- Primary nav exposes CV Builder only; do not reintroduce a parallel My CV edit surface.
- Do not offer Start blank as a wipe once a Structured CV exists—only Template layout changes or confirmed PDF replace.
- Project summaries are generated on `/cv-projects` and imported into the builder Projects section via `sourceSummaryId`.
