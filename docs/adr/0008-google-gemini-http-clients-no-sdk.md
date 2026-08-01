# ADR-0008: Google Gemini via raw HTTP clients only

## Status

Accepted (verified project fact; fleet + contract registry)

## Context

ApplyVault uses Gemini for optional CV import fill, structured update, suggestions, Summary propose, quality evaluation, export assist, scrape enrichment, and project summaries. An official Google SDK would pull a large dependency surface and diverge from the existing options/schema pattern.

## Decision

1. All Gemini calls use **raw HTTP** to `generativelanguage.googleapis.com` via dedicated `HttpClient` / `GoogleAi*` clients — **no Google AI / Gemini SDK package**.
2. Feature behavior is gated by configuration (`GoogleAi:Enabled` and per-feature options); callers must tolerate AI-off / failure without inventing content.
3. Structured CV AI outputs must validate against ADR-0001 catalog / response schemas where applicable.

## Consequences

- Prompt, schema, and retry control stay in-repo; SDK upgrades are not a dependency path.
- New AI features add another HTTP client/options section rather than introducing a second vendor or SDK.
- Spec / agents treat “add Google SDK” as out of scope unless a new ADR supersedes this one.
