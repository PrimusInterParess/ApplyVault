# Execution Workflow — Sample Catalog API

Existing operating procedures: **NONE** (Architect defaults).

1. **Initialize** — Architect loads shared context, registry, contracts, ownership.
2. **Classify request** — Map to ownership; select agents; resolve task id.
3. **Same-task restart gate** — If `handoffs/active/<TASK-ID>/` or
   `scratch/<TASK-ID>/` already has data, ask: **Resume** (keep + reconcile)
   vs **Fresh start** (Close-abandon wipe, then empty restart). Never silent.
4. **Delegate** — Fill `task-delegation.yaml` with mandatory
   `scratch_dir: agent-system/scratch/<TASK-ID>/` and
   `handoff_dir: agent-system/handoffs/active/<TASK-ID>/`;
   inject agent prompt + delegation via host Task when available.
5. **Execute** — Primary agent works in bounds; probes/builds/temp only under
   `scratch_dir`; no provider invention.
6. **Handoff** — Return thin `agent-handoff.yaml` package under `handoff_dir`
   (YAML/MD metadata only; `artifacts:` are external paths/URLs).
7. **Reconcile** — Before trusting `READY`, verify claimed paths/symbols against
   the working tree; mismatch → `STALE`, no integrate.
8. **Validate** — Architect (or reviewer) fills `validation-report.yaml` when required.
9. **Integrate** — Apply integration policy; resolve conflicts if needed.
10. **Approve** — Record required approvals explicitly.
11. **Close** — Cleanup gate (see `protocols/task-close.yaml`):
    - write `handoffs/archive/<task-id>/summary.yaml`
    - delete `scratch/<task-id>/` entirely
    - clear `handoffs/active/<task-id>/`
    - fail if handoffs contain binaries or files > 64 KB
12. **Status** — `REQUEST COMPLETE` | `REQUEST COMPLETE WITH DOCUMENTED LIMITATIONS` |
    `REQUEST BLOCKED` (active may remain for a later Resume unless abandoned).
