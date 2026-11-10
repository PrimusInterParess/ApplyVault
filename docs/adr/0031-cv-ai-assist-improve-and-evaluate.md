# ADR-0031: CV AI assist is two actions — Improve section and Evaluate CV

## Status

Accepted (2026-10-18 — human-approved plan "CV path restructure", Milestone 3)

Supersedes [ADR-0004](0004-cv-summary-regenerate-propose-approve.md) (dedicated Regenerate Summary flow) and [ADR-0011](0011-cv-update-ai-propose-approve.md) decision 1 and 5 (separate `ai-update-propose`, suggestions apply path, legacy `ai-update`). The propose-then-approve principle from both ADRs (ephemeral proposal, approve = local merge + structured PUT, no server Apply endpoint) is kept. [ADR-0007](0007-cv-quality-evaluation-ephemeral.md) (ephemeral evaluation) remains in force. [ADR-0008](0008-google-gemini-http-clients-no-sdk.md) (HTTP, no SDK) remains in force.

## Context

Assist offered four AI actions (Update with instructions, Suggestions list, Evaluate quality, Regenerate Summary), each with its own Google AI client, response schema, service, normalizer and SPA state — five near-identical HTTP clients and two normalizers in total. With a fixed layout the Summary is just one of seven slots and "suggestions" are instructions the user then has to run through Update anyway.

## Decision

1. **Improve this section**: `POST /api/cv-documents/current/structured/improve-section` with `{ sectionId, instructions? }` returns an ephemeral proposal (`changeBullets`, `proposedSection`). The Summary slot uses the same action. Approve merges the proposed Section into the local draft, runs `FixedSectionLayout.Normalize`, and persists via the existing structured PUT. Discard clears memory only.
2. **Evaluate CV**: `POST /api/cv-documents/current/structured/evaluate` keeps the ADR-0007 read-only report (score, dimensions, findings). A finding can prefill Improve instructions for its Section.
3. **One AI transport.** A single generic Google AI structured-output client (HTTP + schema + retry) parameterised by request/response schema replaces the per-feature clients. Schemas remain small records per use case (import, improve section, evaluate).
4. **Removed:** `ai-update`, `ai-update-propose`, `ai-suggestions`, `ai-summary-propose` endpoints and their services, normalizers, clients and SPA state; the Assist panel becomes two tabs.

## Consequences

- Users lose the Suggestions list and the dedicated Summary control; both are covered by Improve this section (Evaluate findings act as the suggestion source).
- AI code shrinks (roughly 1.6k → 0.6k lines of clients) and every future AI feature is a schema plus a service, not a new client.
- Design notes `agent-system/design-cv-summary-regenerate-approve.md` and `design-cv-update-propose-approve.md` are historical.
