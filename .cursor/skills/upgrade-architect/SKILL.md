---
name: upgrade-architect
description: >-
  Upgrades The Architect library usage in a project and regenerates the
  agent-system prompt pack so agent docs match current core workflows (then
  runs ownership sync). Use when the user says /upgrade-architect, refresh
  fleet after library update, sync agent prompts with new Architect version,
  or update generated agents after core changes.
disable-model-invocation: true
---

# Cursor adapter: upgrade-architect

Thin wrapper for the **`/upgrade-architect`** slash command (single entry with
this description). There is no separate `.cursor/commands/upgrade-architect.md`
— Cursor only shows a rich description for skills.

**Read and follow:** [core/workflows/upgrade-architect.md](../../../core/workflows/upgrade-architect.md)

This is the **only** upgrade entry. It:

1. Refreshes the agent-system pack after a library update
2. Auto-adds `code-review-engineer` when missing (unless excluded)
3. Auto-adds `architecture-engineer` when missing (unless excluded)
4. Then runs ownership sync (same steps as `/update-ownership`; confirm before write)

Preserve project-specification.md. Do not implement the application.
Do not invent a second upgrade command or alias (e.g. `architect-upgrade`).
