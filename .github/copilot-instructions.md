# GitHub Copilot instructions — The Architect

When working in a repository that includes The Architect library:

- Prefer `AGENTS.md` and files under `core/workflows/`.
- For multi-agent discovery or prompt-pack work, start with `core/workflows/agent-system-builder.md`.
- Honor slash-style requests using `core/slash-commands.md`.
- Keep provider choices evidence-based and user-approved.
- During discovery, ask one question at a time and stop at `APPROVAL REQUIRED`.
- Do not generate application implementation when the user asked only for discovery or prompt-pack generation.
- Never invent credentials, test results, or deployments.
