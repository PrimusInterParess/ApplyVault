# ADR-0027: Interview Prep brief external study search

## Status

Accepted (2026-08-07 — grill-with-docs + `/operate` D)

Additive to [ADR-0025](0025-interview-prep-brief.md). Does **not** change Interview Prep brief body schema, API/DTOs, persistence, or session lifecycle. UI affordance only — no new glossary term in `CONTEXT.md`.

## Context

Seekers studying an Interview Prep brief often want a quick external web search for a topic or Coverage item. Encoding search URLs in the API, or wiring job-context into the query, would couple study navigation to backend contracts and risk leaking job text into third-party search without a clear product need.

Grill locked: clickable surfaces, query formulas, frontend-only Google URL, new tab.

## Decision

1. **Clickable.** Topic **names** and **Coverage item** text only. Not clickable: sample questions, CV talking points, notes, Brief topic gap badges.

2. **Presentation.** The topic name / Coverage item text itself is the link (no separate icon/button affordance required by this ADR).

3. **Query formulas.**
   - Topic: `{topic.name} interview preparation`
   - Coverage item: `{topic.name} {coverage.text} interview preparation`

4. **Open.** New browser tab with `noopener` (and usual `noreferrer` hygiene as implementers prefer).

5. **Implementation.** Frontend-only construction of `https://www.google.com/search?q=...` (URL-encode the query). No brief API, DTO, AI schema, or persistence change.

## Consequences

- Angular brief study UI owns link construction and target; API/backend remain unaware.
- Rejected (worth remembering): backend-provided `searchUrl`; job-title/company/context in the query; making sample questions or CV talking points clickable; deep-linking questions/talking points to Coverage items for search.
- Out of scope here: non-Google engines, in-app search, caching results, or changing ADR-0025 body shape.
