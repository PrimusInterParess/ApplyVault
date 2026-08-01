# ADR-0004: Summary regeneration is propose-then-approve

## Status

Accepted (2026-08-01 — operate `cv-summary-regenerate-approve-2026-08-01`)

## Context

Assist **Update with instructions** routes through `POST .../structured/ai-update`, which runs Gemini and **persists immediately**. That path cannot support a review/discard gate for rewriting the Structured CV **Summary** section: by the time the UI shows new prose, the server (and often local merge) already applied changes.

Product requirement: regenerate Summary using the rest of the Structured CV plus Contact / user identity as needed; show what changed; replace only after explicit approval.

ADR-0001 (catalog) and ADR-0002 (CV builder sole surface) remain in force. This decision does not change section types or revive legacy CV surfaces.

## Decision

1. **Regenerate Summary** is a dedicated Assist control, separate from immediate multi-section Update.
2. Generation uses an **ephemeral** `POST /api/cv-documents/current/structured/ai-summary-propose` that returns `currentSummaryText`, `proposedSummaryText`, and `changeBullets` and **never** calls `SaveStructuredAsync`.
3. Assist shows **current vs proposed** Summary text plus short **what changed** bullets; **Discard** clears in-memory proposal only.
4. **Approve** patches **Summary section content only** in the local Structured CV draft and persists via the existing structured PUT/save path — not via `ai-update`.
5. AI context for propose includes the full Structured CV, Contact, AppUser identity when available, and optional free-text instructions. Contact identity wins over AppUser when they conflict. The model must not invent employers, metrics, or degrees.

## Consequences

- Assist gains a Regenerate summary review flow.
- ~~Update-with-instructions remains the immediate multi-section path.~~ **Superseded for Assist by ADR-0011** (Update / Apply selected are propose-then-approve).
- No durable proposal store; no new Apply endpoint (avoids racing unsaved local edits).
- No ADR-0001 catalog change; Summary remains `body` ↔ flat `summary`.
- Design notes: `agent-system/design-cv-summary-regenerate-approve.md`.
