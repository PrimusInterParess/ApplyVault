# ADR-0044: One saved cover letter per job

## Status

Accepted (2026-11-10)

Supersedes the “nothing is stored” clause in [ADR-0042](0042-saved-job-cover-letter.md) decision 1. Grounding rules and the local Ollama provider stay as in ADR-0042 and [ADR-0043](0043-cover-letter-local-ollama.md).

## Context

A cover letter disappeared when the seeker left the job. The letter belongs to that saved job, and writing it again should replace the previous one.

## Decision

1. Each saved job stores at most one cover letter and the time it was written (`CoverLetter`, `CoverLetterUpdatedAt` on the scrape result).
2. `POST /api/scrape-results/{id}/cover-letter` writes the letter and replaces any letter already stored for that job.
3. Saved-job reads include the letter, and the job detail shows it again when the seeker returns.

## Consequences

- Leaving the job no longer discards the letter.
- There is no letter history. Write again overwrites the stored text.
- Deleting the saved job deletes its letter with it.
