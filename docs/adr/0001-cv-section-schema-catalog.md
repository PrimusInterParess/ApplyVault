# ADR-0001: CV section schema catalog

## Status

Accepted

## Context

Structured CV import duplicated section types, field rules, PDF heading detection, AI prompts, and UI behavior across many files. Skills and contact data were forced into generic columns (`techStack`, `bullets`).

## Decision

- Maintain a **versioned declarative catalog** at `shared/cv-section-catalog/cv-section-catalog.json` as the single definition of section types and entry fields.
- Persist entry content primarily in **`UserCvEntries.FieldsJson`**, validated against the catalog for the parent section’s type.
- Keep flat API fields (`title`, `subtitle`, …) as a **projection** of `FieldsJson` for stable clients during transition; encoding/decoding lives in one codec.
- Generate import AI guidance from the catalog; PDF heading aliases come from the catalog.

## Consequences

- Adding a section type or field is a catalog change plus codec/UI field kinds, not scattered string edits.
- Legacy rows without `FieldsJson` are hydrated on read from legacy columns until the next save.
- Contact is a first-class section type instead of `Custom` + heading heuristics.
