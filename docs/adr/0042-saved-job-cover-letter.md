# ADR-0042: Cover letter from a saved job

## Status

Accepted (2026-11-10). Decision 1’s “nothing is stored” is superseded by [ADR-0044](0044-cover-letter-saved-per-job.md). Decision 3 is superseded by [ADR-0043](0043-cover-letter-local-ollama.md).

## Context

Saved jobs already deep-link into Interview Prep. Seekers still leave ApplyVault to draft a cover letter, and a free-form model call would invent employers, titles, and metrics the Structured CV does not support. Interview Prep already snapshots the Structured CV and the saved job for grounded generation (ADR-0026).

## Decision

1. **Cover letter** is an ephemeral letter for one saved job. `POST /api/scrape-results/{id}/cover-letter` with an optional seeker note returns `{ scrapeResultId, letter }`. Nothing is stored.
2. Generation requires a Structured CV and a saved-job description. The prompt (`CoverLetterPrompt`, version 1) may use only those two sources, must not invent facts, and follows the note only as emphasis.
3. The model call uses the existing Gemini HTTP client pattern (ADR-0008) and `GoogleAi` options. The letter is shown on the saved-job detail panel, where the seeker can copy it.

## Consequences

- A seeker can draft a grounded letter without a new document type or persistence.
- Google AI must be enabled; local Dev without a key gets a clear error instead of a fake letter.
- Regenerating replaces only the on-screen letter. A later decision can persist a letter if seekers need it across sessions.
