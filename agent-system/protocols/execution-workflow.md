# Execution Workflow — ApplyVault (BRIDGE)

## Precedence

Architect `/operate` orchestrates. Project skills own how work is specified and shipped.

## Canonical delivery chain (FOLLOW project)

1. Clarify as needed (`grilling` / `grill-with-docs` / `domain-modeling`)
2. `/to-spec` → GitHub Issue (`ready-for-agent`) per `docs/agents/issue-tracker.md`
3. `/to-tickets` → tracer-bullet tickets
4. `/implement` with `/tdd` at agreed seams
5. `/code-review` (project skill) and/or pack `code-review-engineer` via `/architect-review`
6. Triage / QA skills as needed for test evidence

## Architect operate loop

1. Re-resolve existing operating procedures from `governance/shared-context.yaml`
2. Read approved `project-specification.md` + ownership/contracts
3. Choose `/operate` option (A plan / B map / C implement milestone / D specific request / E resume)
4. For design/redesign/unset boundaries → delegate `architecture-engineer` before feature coding
5. Select agents; write task-delegation envelopes with mandatory `scratch_dir` + `handoff_dir`
6. Launch specialists (Cursor Task) with purpose + envelope — do not role-play the fleet
7. Collect thin handoffs under `handoffs/active/<task-id>/`; validate
8. READY reconciliation → integration review → approval gates
9. **Close** per `protocols/task-close.yaml` (archive summary, wipe scratch, clear active)
10. Update risk/decision registers when material

## Handoffs and scratch

| Kind | Path |
|---|---|
| Active handoffs (thin YAML/MD) | `agent-system/handoffs/active/<task-id>/` |
| Scratch / probes / builds | `agent-system/scratch/<task-id>/` |
| Closed record | `agent-system/handoffs/archive/<task-id>/summary.yaml` |

Never put `bin/`, `obj/`, `node_modules/`, coverage, or `build-out/` under `handoffs/`.

## READY reconciliation

Before INTEGRATE, confirm handoff status, artifacts paths exist, contracts respected, and no secret leakage. STALE handoffs stay in active until replaced or Close — do not integrate them.

## Close cleanup (part of COMPLETE)

Principal Definition of Done includes Close. Fail Close on binary/large dumps under handoffs. Always delete build dumps from operate paths.

## Do not

- Invent a second ticket tracker
- Overwrite `.agents/skills`, `CONTEXT.md`, ADRs, or `docs/agents/*`
- Implement application code during discovery or prompt-pack generation
- Silent-delete legacy thin YAML without M1/M2/M3 user choice
