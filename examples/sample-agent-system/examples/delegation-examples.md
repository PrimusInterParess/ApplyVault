# Delegation Examples — Sample Catalog API

## Example A — List endpoint sketch

- Primary: `backend-engineer`
- Delegator: `principal-software-architect`
- Objective: Document list-items route and response fields
- Excluded: auth, payments, deploy
- `scratch_dir`: `agent-system/scratch/TASK-SAMPLE-001/`
- `handoff_dir`: `agent-system/handoffs/active/TASK-SAMPLE-001/`

See `protocols/task-delegation.yaml` for the filled envelope.

## Example B — Catalog boundary redesign

- Primary: `architecture-engineer`
- Delegator: `principal-software-architect`
- Objective: Propose module and contract shape before expanding catalog fields
- Follow-on: `backend-engineer` after design handoff is integrated
- Excluded: implementation coding in the design-only delegation

## Example C — Blocked persistence binding

- Primary: `backend-engineer`
- Stop when asked to bind a cloud datastore without user approval
- Escalation: Architect → user (provider decision)
