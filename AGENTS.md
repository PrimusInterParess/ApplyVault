# ApplyVault — Agent Guide

BRIDGE layout: this file binds **project delivery procedures**. The Architect
library lives under `core/` (slash commands in `core/slash-commands.md`).
Do not treat this file as a replacement for `core/` workflows.

## Application coding standards

Before changing application code, read [docs/coding-standards.md](docs/coding-standards.md),
`CONTEXT.md`, and the ADRs relevant to the change. These standards apply to
`api/`, `frontend/`, `extension/`, and application assets under `shared/`.

- Identify the owning feature and trace its existing implementation before editing.
- Follow the responsibilities and dependency rules in the standards; existing
  code is context, not an automatic exemption from them.
- Keep changes focused and improve the code you touch without unrelated rewrites.
- Before finishing, review the diff against the standards and task requirements.
  Report checks run, results, and any remaining limitations or justified exceptions.

## Agent skills

### Issue tracker

Issues and PRDs live in GitHub Issues on `PrimusInterParess/ApplyVault`. See `docs/agents/issue-tracker.md`.

### Triage labels

Default Matt Pocock triage vocabulary (`needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`). See `docs/agents/triage-labels.md`.

### Domain docs

Single-context layout: `CONTEXT.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.

### Delivery chain

Prefer `.agents/skills`: clarify/grill → `/to-spec` → `/to-tickets` → `/implement` (+ `/tdd`) → `/code-review` → triage/QA as needed.

## The Architect (orchestration)

When the user runs Architect slash commands (`/architect`, `/discover`,
`/operate`, `/upgrade-architect`, `/architect-review`, etc.), follow
`core/slash-commands.md` and the matching `core/workflows/*` file.

- Discovery ends at `APPROVAL REQUIRED`.
- Do not invent secrets, files, tests, or deployments.
- Generated fleet: `agent-system/` (preserve `project-specification.md`).
- Install policy: `agent-system/architect-install.yaml` (copy-install).
