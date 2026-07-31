---
name: update-ownership
description: >-
  Syncs agent-system ownership matrix to the live repository or a branch diff:
  propose coverage gaps, confirm with the user, update governance ownership.
  Use for /update-ownership, stale ownership, or unowned paths after new modules.
disable-model-invocation: true
---

# Cursor adapter: update-ownership

**Read and follow:** [core/workflows/update-ownership.md](../../../core/workflows/update-ownership.md)

## Cursor-specific

Prefer **AskQuestion** for scan mode (`REPO` / `DIFF` / `PREVIEW`) and for
confirming proposed owner assignments before writing files.
