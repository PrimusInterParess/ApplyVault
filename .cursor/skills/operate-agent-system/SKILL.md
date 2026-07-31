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

## Cursor-specific: choice UI

When presenting A–E (or other operate choices), use the native **`AskQuestion`**
tool so options are clickable. Do not print the text menu first when
`AskQuestion` is available.

Also use **`AskQuestion`** for the **Same-task restart gate** (Resume vs
Fresh start) whenever `handoffs/active/<task-id>/` or `scratch/<task-id>/`
already has data for the issue being operated. Do not auto-resume or
auto-wipe across a new chat/thread.

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
6. Integrate returned handoffs in the parent (after READY reconciliation), then
   continue the operate loop through Approve → **Close** (archive summary,
   delete `scratch/<task-id>/`, clear `handoffs/active/<task-id>/`).
