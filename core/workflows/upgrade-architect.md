# Upgrade Architect Library + Refresh Fleet

After installing a newer The Architect version, refresh the **generated**
prompt pack so agent docs and governance match the current `core/` workflows
(including existing-operating-procedures adaptation).

Slash: `/upgrade-architect`

## Mission

1. Confirm (or apply) the library update in this project.
2. Keep `agent-system/project-specification.md` as the approved source of truth.
3. Re-run prompt-pack generation in `SAVE` mode so agents, shared context,
   execution workflow, and governance pick up new library rules.
4. Do **not** implement application features.

## When to use

- User updated The Architect via submodule, `git pull`, or
  `scripts/update-into-project.*`
- User wants generated `agent-system/agents/*.md` and governance rewritten for
  the new library (e.g. procedure-adaptation Operating Principles)
- Equivalent phrases: refresh fleet after library update, upgrade architect,
  sync agent prompts with new core

## Prerequisites

| Check | If missing |
|---|---|
| Approved `agent-system/project-specification.md` | Stop; run discovery / approval first |
| Spec status `APPROVED` or `APPROVED WITH CHANGES` | Stop; finish approval |
| Current `core/workflows/generate-prompt-pack.md` present | Stop; update the library first |

## Steps

### 1. Library version check + auto install policy

**Goal:** detect install style automatically, write
`agent-system/architect-install.yaml` when possible, never invent hardcoded
folder names, never create an app-root stamp for non-copy installs.

#### 1a. Detect install style (path-neutral)

Inspect the app workspace (do not assume folder names):

| Detection | `install_style` | `library_root` |
|---|---|---|
| `core/` is a junction or symlink | `junction` | Parent of the resolved `core/` target (prefer path relative to app root) |
| `.gitmodules` entry whose path contains Architect `AGENTS.md` + `core/workflows/` | `submodule` | That submodule path |
| `core/workflows/` is a real (non-link) directory at app root with copied library files | `copy_install` | `.` (or omit) |
| Workspace root *is* an Architect checkout and has no separate app fleet | `standalone_clone` | `.` |
| `architect-install.yaml` already exists | Keep existing values | Prefer existing `library_root` |

If detection cannot find a root and no policy exists, ask the user once for
`library_root`. Do not invent a path.

#### 1b. Auto-write / refresh policy (do this; do not ask)

When `agent-system/` exists (fleet already generated):

1. If `agent-system/architect-install.yaml` is **missing** and detection
   produced `library_root` + `install_style`, **create it** with:
   - detected `install_style`
   - detected `library_root`
   - `write_app_root_stamp: false` for `junction` / `submodule` /
     `standalone_clone`
   - `write_app_root_stamp: true` only for `copy_install`
2. If the file **exists**, do not overwrite a user-set `library_root`. You may
   set `write_app_root_stamp: false` when style is non-copy and the field is
   missing or incorrectly `true`.
3. Report whether the policy was created, updated, or left as-is.

#### 1c. Version string (first hit wins)

1. Policy `library_root` → `VERSION`
2. Resolved `core/` link → sibling `VERSION`
3. App-root `VERSION` when present
4. Existing `.architect/library-version` only (read; never create here)

Report path + version. If library files are not yet updated, tell the user to
update that checkout, then re-invoke `/upgrade-architect`.

#### 1d. Stamp rules + cleanup

**Do not create `.architect/`** unless `install_style` is `copy_install` **and**
`write_app_root_stamp` is not `false`.

For non-copy styles (`junction` / `submodule` / `standalone_clone`):

- If `.architect/` exists at the app root (stray stamp from an older upgrade),
  **delete it** and note that in the upgrade summary.
- Do not edit `.gitignore` / excludes unless the user asks.

Do not run install/update scripts unless the user explicitly authorizes shell
file copies in this session.

### 2. Preserve project artifacts

**Never delete or regenerate** unless the user explicitly asks:

- `agent-system/project-specification.md`
- `agent-system/implementation-plan.md` (if present)
- `agent-system/repository-task-mapping.md` (if present)
- Application source code outside `agent-system/` library paths

### 3. Refresh procedure inventory (Brownfield / Hybrid)

If the specification mode is `BROWNFIELD` or `HYBRID`, or existing procedures
were previously recorded:

1. Re-run the detection rules in
   [existing-operating-procedures.md](existing-operating-procedures.md)
   (read-only scan).
2. Update the specification section
   **Existing Agent / Skills / Documentation Procedures** when the inventory
   or adaptation mode changed.
3. Keep status `APPROVED WITH CHANGES` when the procedure inventory or
   adaptation mode changes in a material way; otherwise leave approval status
   as-is and note procedure refresh in the upgrade summary.
4. If adoption decisions would change in a breaking way, stop and ask for
   approval before regenerating agents.
5. Always note library version before → after and list agent/governance files
   replaced.

For `GREENFIELD` with `NOT_FOUND` / `NONE`, skip unless the user points at a
new skills pack.

### 4. Regenerate the prompt pack

Execute [generate-prompt-pack.md](generate-prompt-pack.md) in `SAVE` mode with
these upgrade rules:

1. Authorized to replace existing pack files for **this same project**
   (same project id/name in manifest / specification).
2. Rewrite agents, governance, and protocols from the approved specification
   using **current** core rules.
3. Preserve `project-specification.md` (and plan/mapping files above).
4. Bind `existing_operating_procedures` into shared context when adopted.
5. **Auto-ensure project reviewer:** follow generate-prompt-pack **Default
   reviewer (required in SAVE packs)**. If `code-review-engineer` (or an
   equivalent REVIEWER already owning PR/diff review) is absent and the spec
   does not exclude code review, **create and wire it** during this SAVE.
   Do not tell the user to run `/extend-fleet` / `/create-agent` for this
   default agent.
6. **Auto-ensure architecture engineer:** follow generate-prompt-pack
   **Default architecture engineer (required in SAVE packs)**. If
   `architecture-engineer` (or an equivalent SPECIALIST already owning
   architecture design/redesign) is absent and the spec does not exclude
   architecture design, **create and wire it** during this SAVE.
   Do not tell the user to run `/extend-fleet` / `/create-agent` for this
   default agent.
7. Ensure operate working trees and Close protocol exist (create if missing):
   `handoffs/active/` (+ `.gitkeep`), `handoffs/archive/`, `scratch/` (+
   `.gitkeep`), `protocols/task-close.yaml`, pack `.gitignore` entries for
   `scratch/`, `**/build-out/`, and `handoffs/active/`. Refresh
   `execution-workflow.md` and Principal DoD so Close cleanup is part of
   COMPLETE. Do not delete user archive summaries.
8. **Legacy operate piles:** scan for loose handoffs, `debug/` trees, and
   build dumps under operate paths (see operate-agent-system **Legacy operate
   piles**). Present M1 Keep-as-active / M2 Archive+clean / M3 Delete; never
   silent-delete thin YAML. Always remove `bin/` / `obj/` / `build-out/` /
   coverage dumps from handoff areas. Record outcome in the upgrade summary.
9. Verify with the generate-prompt-pack save verification steps.
10. In the upgrade summary, state reviewer and architecture-engineer outcomes:
   each as `created` | `refreshed` | `skipped (excluded)` |
   `skipped (equivalent exists)`; also note handoffs/scratch/Close and legacy
   pile migration outcomes.

### 5. Ownership sync (required chain)

After a successful pack regenerate (step 4), run
[update-ownership.md](update-ownership.md) in `REPO` mode (or `DIFF` if the
user already provided a base ref for that purpose):

1. Use the **same** workflow as `/update-ownership` — do not fork behavior.
2. Propose ownership gaps vs the live repo; **confirm with the user** before
   writing the matrix.
3. If the user skips confirmation, record
   `ownership_sync: skipped (user declined)` and continue; do not block the
   library upgrade result solely for a declined ownership pass.
4. If update-ownership is `OWNERSHIP_SYNC_BLOCKED` only because new agents are
   required, note `NEEDS_NEW_AGENT` and point to `/extend-fleet`; library
   upgrade may still be `ARCHITECT_UPGRADE_READY`.

### 6. Report

Return a short summary only:

1. Library version before → after (if known)
2. Install policy: created / updated / unchanged (`install_style`, `library_root`)
3. Stamp cleanup: deleted `.architect/` or n/a
4. Spec path + approval status
5. Procedure adaptation mode (`FOLLOW` / `COMPOSE` / `BRIDGE` / `NONE`)
6. Files created / updated / skipped under `agent-system/`
7. `code-review-engineer` outcome (see step 4)
8. `architecture-engineer` outcome (see step 4)
9. Ownership sync outcome (see step 5): applied / no changes / skipped / blocked
10. Verification result
11. Next step: `/operate`, `/architect-review <base>`, `/update-ownership`, or
    `/extend-fleet` when `NEEDS_NEW_AGENT`

## Result status

- `ARCHITECT_UPGRADE_READY` — library current and fleet regenerated
- `ARCHITECT_UPGRADE_LIBRARY_PENDING` — user must update library files first
- `ARCHITECT_UPGRADE_BLOCKED` — missing/invalid approved spec, project
  conflict, or verification failure

## Safety

- No application implementation
- No secret values
- No silent overwrite of an unrelated project's prompt pack
- Prefer regenerating from the approved spec over hand-editing dozens of
  agent files
- Never create an app-root `.architect/` stamp for submodule, standalone,
  or junction installs; stamps are copy-install (Option B) only
- Auto-create `agent-system/architect-install.yaml` when install style can be
  detected; never invent `library_root` paths
- Honor existing `architect-install.yaml` `library_root` (do not clobber)
- Remove stray app-root `.architect/` on non-copy installs
