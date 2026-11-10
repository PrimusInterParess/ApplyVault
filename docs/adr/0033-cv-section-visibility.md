# ADR-0033: Section visibility

## Status

Accepted (2026-10-19 — human-approved grilling session)

Refines [ADR-0029](0029-fixed-cv-section-layout-and-unplaced-content.md) decision 1 ("users cannot add, remove, reorder or rename Sections") and decision 4 (empty Sections are already omitted from the export; the edit canvas shows all seven). Sections stay fixed. Section visibility is a further choice about the Template only.

The persistence seam is [ADR-0035](0035-cv-section-visibility-persistence-and-compact-step.md).

## Context

ADR-0029 removed free-form Sections. A Section with Entry content always appears on the Template, so the only way to shorten the PDF is to delete Entries or rely on the page policy in [ADR-0028](0028-single-cv-template-page-policy.md). Seekers still want a Section's Entries kept for editing, Evaluate CV, and Interview Prep while leaving that Section off the CV an employer sees.

## Decision

1. **Section visibility is omit or include on the Template.** An omitted Section keeps its Entries on the Structured CV. Including it again puts those Entries back on the Template. This is not removing a Section.
2. **Contact cannot be omitted.** When Contact has Entry content it always appears on the Template. Summary, Skills, Experience, Projects, Education, and Certifications & Languages may each be omitted on their own.
3. **A Section with no Entry content is already omitted** from the Template. The visibility choice matters once the Section has content.
4. **An omitted Section leaves the edit canvas.** The include / omit switch sits on that Section in the left section list. Contact has no switch and stays on the canvas. Including the Section puts it back on the canvas with its Entries.
5. **Evaluate CV and Interview Prep still use an omitted Section's Entries.** A visibility change alone does not mark an Interview Prep brief outdated. Adding Entries, including project summary import, does not include the Section. Only the section-list switch does.
6. **A new Structured CV starts with every Section included.** That covers a Blank CV and a replacement PDF import. Omit choices do not carry onto replacement content.

## Consequences

- Existing Structured CVs start included. Nothing is omitted until the seeker says so.
- Page length is not solved by omitting. Fitting the page is Section placement ([ADR-0034](0034-cv-section-placement.md)).
- The Interview Prep outdated check follows Entry changes. A visibility-only save must not look like the Entries changed.
- `CONTEXT.md` defines **Section visibility**.
