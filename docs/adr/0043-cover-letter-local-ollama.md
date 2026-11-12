# ADR-0043: Cover letters use the local Ollama model

## Status

Superseded by [ADR-0045](0045-cover-letter-google-gemini.md) (2026-11-12). Decisions 1 and 2 below no longer apply. The Ollama client remains available when `CoverLetter:Provider` is `Ollama`.

Accepted (2026-11-10). Supersedes [ADR-0042](0042-saved-job-cover-letter.md) decision 3. Decisions 1 and 2 of ADR-0042 remain in force.

## Context

The local machine already runs Ollama with `qwen3:32b`. Cover letters were wired only to Gemini, so the saved-job action failed unless a Google API key was set.

## Decision

1. Cover letters call Ollama's chat API (`CoverLetter` options: provider `Ollama`, model `qwen3:32b`, base URL `http://127.0.0.1:11434`).
2. The local Docker API uses `http://host.docker.internal:11434` so the container reaches Ollama on the host. `CoverLetter:Enabled` is true in that compose file.
3. `CoverLetter:Provider=Google` keeps the Gemini client for an environment that sets `GoogleAi`. The prompt and grounding rules are unchanged.

## Consequences

- A local cover letter needs Ollama running with `qwen3:32b`. A 32B model can take longer than a minute; the local timeout is 180 seconds.
- Production stays off until `CoverLetter:Enabled` is set. It does not call localhost by default.
