# ADR-0028: Single CV Template and one-page-recommended / two-page-max export

## Status

Accepted (2026-10-18 — human-approved plan "Single CV template")

Partially supersedes [ADR-0006](0006-cv-export-html-puppeteer-pipeline.md) decisions 1 and 4 (multiple supported Template ids; fallback to Classic). The shared HTML → PuppeteerSharp pipeline (ADR-0006 decisions 2–3) and the edit-canvas vs Check-export split ([ADR-0003](0003-cv-builder-edit-canvas-vs-export-preview.md)) are unchanged.

Decision 1's placement of Summary beside the photo is superseded by [ADR-0038](0038-summary-full-width-section.md): Summary is a full-width main-column Section. Name and contact lines stay in the header with the photo.

Decision 2's two-page spacing is refined by [ADR-0034](0034-cv-section-placement.md): on two pages, spacing tightens only enough to keep fitting Sections whole, and a Section or Entry stays on one page whenever it fits.

## Context

v1 shipped a Template gallery (Classic, later Modern + Minimal) selected by an integer id persisted on the CV document, cached in `sessionStorage`, passed as a query parameter to export endpoints, and echoed in the download file name. Two layouts meant two HTML shells, two compact-CSS rule sets, a two-way section partition (sidebar vs header) mirrored in Angular and C#, and a topbar picker. The export also always targeted a single page: the compact CSS ramp (levels 0–4) shrank fonts, spacing and the photo until the PDF fit one page, and only warned when even level 4 could not.

The product direction is simpler: one clean layout that always supports photo, contact lines, markdown links, summary, skills, and the full section catalog; one page is the recommended length and two pages is a hard ceiling.

## Decision

1. **Exactly one Template.** The export layout is the former Minimal one-column shell (`wwwroot/cv-export-templates/cv-template.html`): name, contact lines and Summary in a header with the profile photo on the right; every other Section in a single main column. The Template catalog, `TemplateId` column, `PUT /api/cv-documents/current/export-preferences`, the `templateId` query parameter, the `X-Cv-Export-Template-Id` header, the topbar picker and the `sessionStorage` cache are removed. Legacy `?templateId=` requests are ignored, not rejected.
2. **Page policy: one page recommended, two pages maximum.** The compact ramp still runs levels 0–4:
   - return the first level that fits **one** page (no notice at level 0; "Layout was compacted to fit on one page." otherwise);
   - if none fits one page, return the **lightest** level that fits **two** pages with the advisory "Your CV is 2 pages; one page is recommended — consider trimming content.";
   - if none fits two pages, return the fewest-pages level, set `ExceedsMaxPages` / `X-Cv-Export-Exceeds-Limit`, and warn "This export is N pages after compacting; the maximum is two pages.".
3. **File name** becomes `{PersonName}-CV.pdf` (fallback `CV.pdf`).
4. The DB column is dropped by migration `20261018120000_RemoveCvDocumentTemplateId` (reversible; `Down` re-adds `int NOT NULL DEFAULT 2`).

## Consequences

- One HTML shell, one compact-CSS table and one section partition (`header` / `main`) to keep in parity between `CvExportHtmlMapper` and the Angular edit canvas; adding a layout later means re-introducing a Template concept deliberately, not reviving the old ids.
- Long CVs are no longer squeezed to compact level 4 just to reach one page when they can sit comfortably on two; they get the hint instead. CVs that cannot fit two pages still export (best effort) with a warning.
- `CvDocumentDto` no longer carries `templateId`; clients must not send export preferences.
- `CONTEXT.md` "Template" now describes a single export layout; the CV builder description no longer lists "Template choice".
