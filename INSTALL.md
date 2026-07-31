# Install The Architect into another project

Use this when you want Architect workflows inside an existing application repo.

## Option A — Clone as a standalone toolkit

```bash
git clone <YOUR_REPO_URL> thearchitect
cd thearchitect
```

Open the folder in your AI IDE and follow `AGENTS.md`.

**Do not** create an app-root `.architect/` stamp for Option A. Version comes
from the clone’s `VERSION` file. If an app keeps a separate checkout and links
`core/` (junction/symlink) or otherwise points at that checkout, add a tracked
policy with that project’s `library_root` — see **Install policy** below.

## Option B — Copy portable files into a target project

From this repository:

```powershell
powershell -File scripts/install-into-project.ps1 -TargetPath "C:\path\to\your\app"
```

Or:

```bash
bash scripts/install-into-project.sh /path/to/your/app
```

This copies:

- `AGENTS.md`
- `core/`
- `schemas/`
- `references/source-prompts/` (required for phase-2 prompt-pack generation)
- `examples/` (sample outputs)
- optional Cursor adapters into the target’s `.cursor/` (skills, commands, rules)
- `scripts/`
- optional `CLAUDE.md` and `.github/copilot-instructions.md`

It does **not** overwrite existing target files unless you pass `-Force`.

Copy-install writes `.architect/library-version` at the app root by default
(Option B only). Skip the stamp with `-NoStamp` (PowerShell),
`ARCHITECT_NO_APP_STAMP=1` (bash/env), or
`write_app_root_stamp: false` in `agent-system/architect-install.yaml`.

## Option C — Git submodule

```bash
cd /path/to/your/app
git submodule add <YOUR_REPO_URL> vendor/thearchitect
```

Then point your IDE instructions at:

- `vendor/thearchitect/AGENTS.md`
- `vendor/thearchitect/core/workflows/agent-system-builder.md`

**Do not** stamp the app root for submodules. Use `<submodule-path>/VERSION`
(or set `library_root` in the install policy).

## Install policy (auto-managed)

`/upgrade-architect` **creates or refreshes**
`agent-system/architect-install.yaml` when it can detect the layout
(junction/symlink `core/`, submodule path, copy-install, etc.). You normally
do not create this file by hand.

It will also delete a stray app-root `.architect/` on non-copy installs.

If detection cannot find the checkout, it asks once for `library_root`.

Manual template (only if you need to set it yourself):

```yaml
# agent-system/architect-install.yaml
install_style: junction   # or standalone_clone | submodule | copy_install
library_root: <path-to-architect-checkout>
write_app_root_stamp: false
```

Schema: `schemas/architect-install.schema.json`. Example:
`examples/architect-install.junction.yaml`.

Version discovery does **not** hardcode folder names. It uses (in order)
policy `library_root`, a resolved `core/` link target, in-tree `VERSION`,
then an existing stamp only.

## After install

1. Open the project in your AI IDE.
2. Run `/architect` or `/discover` and complete discovery.
3. Reply `APPROVED` (or `APPROVED WITH CHANGES`) when the specification is ready.
4. After approval, the pack is generated automatically in `SAVE` mode under
   `agent-system/`. Use `/generate-prompt-pack` only to regenerate or recover.
5. Run `/operate` for planning or implementation.
6. On-demand code review (no fleet required): `/architect-review <base>`.
7. After a later library update: update files, then `/upgrade-architect`
   (auto-adds `code-review-engineer` and `architecture-engineer` when missing,
   then runs ownership sync).
8. On demand without a library bump: `/update-ownership` to refresh the
   ownership matrix from the repo/diff.

## Updating to a new library version

Bump `VERSION` in this repo when you publish. Consumers update as follows.

### Easiest ongoing: Option C (submodule) — recommended for teams

```bash
cd /path/to/your/app
git submodule update --remote vendor/thearchitect
# or pin a release tag:
# cd vendor/thearchitect && git fetch && git checkout v0.5.0
```

Point IDE instructions at `vendor/thearchitect/AGENTS.md` so you never copy
library files into the app root.

### Option A (standalone clone)

```bash
cd thearchitect
git pull
# or: git fetch && git checkout v0.4.0
```

### Option B (copied into an app) — use the update script

From a checkout of the **new** Architect version:

```powershell
powershell -File scripts/update-into-project.ps1 -TargetPath "C:\path\to\your\app"
```

```bash
bash scripts/update-into-project.sh /path/to/your/app
```

Preview first:

```powershell
powershell -File scripts/update-into-project.ps1 -TargetPath "C:\path\to\your\app" -DryRun
```

What the update script does:

- Refreshes `core/`, `schemas/`, `references/source-prompts/`, `scripts/`,
  adapters, `AGENTS.md`, `VERSION`
- Writes `.architect/library-version` (copy-install stamp; skip with
  `-NoStamp` / `ARCHITECT_NO_APP_STAMP=1` / `write_app_root_stamp: false`)
- **Does not touch** `agent-system/` (your generated fleet stays intact)

If you customized `AGENTS.md` / `CLAUDE.md` / Cursor rules in the app, review
the git diff after update (or pass `-SkipAgentsMd` / `-SkipClaude` /
`-SkipCursorAdapters`).

Do **not** use `install-into-project` with `-Force` for routine upgrades — that
is a blunt reinstall. Prefer `update-into-project`.

### Refresh generated agent docs (required for new core rules)

Updating library files does **not** rewrite `agent-system/agents/*.md`. After
the library update, in the target project run:

```text
/upgrade-architect
```

That regenerates the prompt pack from the approved specification using the new
`core/` workflows (procedure adaptation, operating principles, etc.) while
preserving `project-specification.md`.

## Uninstall

Delete the copied folders/files listed above, plus `.architect/` if present.
If you used a submodule, remove the submodule entry normally.
