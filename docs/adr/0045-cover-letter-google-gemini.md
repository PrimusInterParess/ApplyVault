# ADR-0045: Cover letters use Google Gemini

## Status

Accepted (2026-11-12)

Supersedes [ADR-0043](0043-cover-letter-local-ollama.md) decisions 1 and 2. Prompt and grounding rules from [ADR-0042](0042-saved-job-cover-letter.md) stay in force. Storage stays as in [ADR-0044](0044-cover-letter-saved-per-job.md).

## Context

Cover letters were sent to a local Ollama model (`qwen3:32b`). The rest of the product already calls Gemini through `GoogleAi` when that section is enabled, and the local environment has a Gemini key. Keeping cover letters on Ollama meant a second runtime that the other AI features do not use.

## Decision

1. The cover letter provider is `Google`. `GoogleAiCoverLetterClient` writes the letter with the `GoogleAi` model (`gemini-2.5-flash`).
2. Local Docker sets `CoverLetter:Provider` to `Google` and leaves `CoverLetter:Enabled` true. `GoogleAi:Enabled` and `GoogleAi:ApiKey` come from the local env file.
3. `CoverLetter:Provider=Ollama` still selects the local Ollama client (base URL and model on the `CoverLetter` section). The prompt and grounding rules are unchanged.

## Consequences

- A cover letter needs `GoogleAi:Enabled` and an API key. Ollama does not need to be running.
- Production stays off until `CoverLetter:Enabled` and `GoogleAi` are set. It does not call localhost by default.
