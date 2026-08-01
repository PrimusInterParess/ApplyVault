# ADR-0011: Assist Update with AI is propose-then-approve

## Status

Accepted (2026-08-01 — `cv-update-propose-approve-2026-08-01`)

## Context

Assist **Update with instructions** and **Apply selected** suggestions called
`POST .../structured/ai-update`, which runs Gemini and **persists immediately**.
Users could not review Current vs Proposed section text or Discard before the
server wrote changes.

ADR-0004 introduced propose-then-approve for **Regenerate Summary** only and
left multi-section Update as an immediate path. Product now requires the same
review gate for Update CV with AI (and the suggestions apply path that shared
`updateWithAi`).

ADR-0001 (catalog), ADR-0002 (CV builder sole surface), and ADR-0004 (Summary
propose/approve) remain in force.

## Decision

1. Assist **Update with instructions** and **Apply selected** use an
   **ephemeral** `POST /api/cv-documents/current/structured/ai-update-propose`
   that returns `focusSectionIds`, `changeBullets`, and `proposedSections` and
   **never** calls `SaveStructuredAsync`.
2. Assist shows **what changed** bullets plus **Current vs Proposed** readable
   panes for affected sections; **Discard** clears in-memory proposal only.
3. **Approve** merges proposed sections into the local Structured CV draft
   (existing focus/omit-preserve merge) and persists via the existing structured
   PUT/save path — **not** via `ai-update`.
4. No durable proposal store and no new server Apply endpoint (avoids racing
   unsaved local edits), same rationale as ADR-0004.
5. Legacy `POST .../ai-update` may remain for compatibility but is **not** used
   by Assist for Update or Apply selected.

## Consequences

- ADR-0004’s consequence that “Update-with-instructions remains the immediate
  multi-section path” is **superseded for Assist**.
- Regenerate Summary stays on `ai-summary-propose` (unchanged).
- Design note: `agent-system/design-cv-update-propose-approve.md`.
