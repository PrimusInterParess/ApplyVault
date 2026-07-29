# Conflict Resolution — ApplyVault

## Precedence

1. Current user directive
2. Approved `agent-system/project-specification.md`
3. Project host instructions + skills + domain docs (`AGENTS.md`, `.agents/skills`, `CONTEXT.md`, ADRs, `docs/agents/*`)
4. This prompt pack (`governance/*`, agent prompts)
5. Architect library defaults in `core/`

## Ownership conflicts

- Use `governance/ownership-matrix.md`.
- If still ambiguous, `principal-software-architect` decides and records in `decision-register.yaml`.

## Procedure conflicts (BRIDGE)

- Delivery loop (spec/tickets/implement/review) → project skills win.
- Multi-agent orchestration and delegation envelopes → Architect `/operate` wins.
- Never silently replace GitHub Issues with a parallel tracker.

## Technical conflicts

- Prefer incremental change at existing seams.
- REPLACE / provider swap requires explicit human approval gate.
