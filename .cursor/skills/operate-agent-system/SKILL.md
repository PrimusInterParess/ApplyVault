---
name: operate-agent-system
description: >-
  Operates a generated agent prompt pack through Architect delegation, handoffs,
  validation, and final integration review. Use when the user wants to run the
  multi-agent workflow against a real request.
disable-model-invocation: true
---

# Cursor adapter: operate-agent-system

**Read and follow:** [core/workflows/operate-agent-system.md](../../../core/workflows/operate-agent-system.md)

## Cursor-specific: native Task delegation

When executing options **C** or **D** (or any specialist implementation under
`/operate`):

1. Parent session acts only as `principal-software-architect`.
2. For each selected fleet agent, call the Cursor **`Task`** tool.
3. Inject into each Task prompt:
   - that agent’s file under `agent-system/agents/`
   - the filled `task-delegation.yaml` envelope
   - shared context / contracts / authorization bounds
4. Use `generalPurpose` for implementation or design work; `explore` only for
   read-only research.
5. Do not implement another agent’s owned work in the parent chat while `Task`
   is available.
6. Integrate returned handoffs in the parent, then continue the operate loop.
