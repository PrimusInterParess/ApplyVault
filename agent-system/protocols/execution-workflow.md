# Execution Workflow — ApplyVault (BRIDGE)

## Precedence

Architect `/operate` orchestrates. Project skills own how work is specified and shipped.

## Canonical delivery chain (FOLLOW project)

1. Clarify as needed (`grilling` / `grill-with-docs` / `domain-modeling`)
2. `/to-spec` → GitHub Issue (`ready-for-agent`) per `docs/agents/issue-tracker.md`
3. `/to-tickets` → tracer-bullet tickets
4. `/implement` with `/tdd` at agreed seams
5. `/code-review`
6. Triage / QA skills as needed

## Architect operate loop

1. Re-resolve existing operating procedures from `governance/shared-context.yaml`
2. Read approved `project-specification.md` + ownership/contracts
3. Choose `/operate` option (A plan / B map / C implement milestone / D specific request / E resume)
4. Select agents; write task-delegation envelopes
5. Launch specialists (Cursor Task) with purpose + envelope
6. Collect handoffs; validate; integration review
7. Update risk/decision registers when material

## Do not

- Invent a second ticket tracker
- Overwrite `.agents/skills`, `CONTEXT.md`, ADRs, or `docs/agents/*`
- Implement application code during discovery or prompt-pack generation
