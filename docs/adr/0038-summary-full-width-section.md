# ADR-0038: Summary spans the full Template row

## Status

Accepted (2026-10-20 — human request that Summary match the other Sections)

Supersedes the Summary-in-header placement in [ADR-0028](0028-single-cv-template-page-policy.md) decision 1. Name, contact lines, and the profile photo stay in the header. The single Template and the one-page / two-page policy are unchanged.

## Context

ADR-0028 put Summary in the header copy, beside the profile photo. That column is narrower than the main column by the photo and the gap, so the Summary heading and prose stop short of the row that Skills, Experience, and the other Sections use.

## Decision

1. **Summary is a main-column Section.** It uses the same full content width and section chrome as Skills, Experience, Projects, Education, and Certifications & Languages.
2. **The header holds the name, contact lines, and the profile photo.** Contact stays out of the main column.
3. **Catalog order is unchanged.** Summary still follows Contact and precedes Skills on the Template and the edit canvas.

## Consequences

- The edit canvas and the export stay in parity (`partitionSectionsForExport` and `CvExportHtmlMapper`).
- A long Summary uses the full row, so it may take fewer lines and change which compact level fits the page.
- Rejected: stretching only the Summary heading underline while leaving the prose beside the photo.
