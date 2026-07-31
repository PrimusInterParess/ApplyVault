# Update Ownership

Scan the repository (or a branch diff) against the fleet ownership map, propose
coverage updates, and **write** `governance/ownership-matrix.md` (and light
registry/`owns` notes when needed) only after the user confirms.

Slash: `/update-ownership`

Also invoked automatically as the final phase of
[upgrade-architect.md](upgrade-architect.md) (same workflow; do not duplicate
logic).

## Mission

1. Detect path/concern gaps: repo structure (or diff) vs current ownership.
2. Propose matrix rows / path bindings to **existing** agents when possible.
3. When nothing fits, recommend `/extend-fleet` or `/create-agent` — do not
   invent a new agent in this workflow.
4. Apply confirmed updates to the pack governance files.
5. Do **not** regenerate the whole prompt pack and do **not** implement app code.

## When to use

- On demand: ownership feels stale after new modules/services land
- After CI / human notice of unowned paths
- Chained from `/upgrade-architect` after library pack refresh
- Equivalent phrases: sync ownership, update ownership matrix, ownership coverage

## Prerequisites

| Check | If missing |
|---|---|
| `agent-system/` with approved `project-specification.md` | Stop; finish discovery/approval |
| `governance/ownership-matrix.md` (or regeneratable from registry) | Stop or recreate empty matrix shell from registry agents, then continue |
| `governance/agent-registry.yaml` + `manifest.yaml` for **this** project | Stop on foreign pack / missing fleet |

## Modes

| Mode | Behavior |
|---|---|
| `REPO` (default on-demand) | Compare high-signal repo roots/projects to ownership patterns |
| `DIFF` | Compare changed paths vs a base ref (user-required base, same spirit as `/architect-review`) |
| `PREVIEW` | Propose only; write nothing |

When chained from `/upgrade-architect`, default to `REPO` unless the user asks
for `DIFF` with a base.

## Inputs

| Input | Required? | Notes |
|---|---|---|
| Scan mode `REPO` / `DIFF` / `PREVIEW` | No | Default `REPO` (or `PREVIEW` if user forbids writes) |
| Base ref | Only for `DIFF` | Ask once; do not guess |
| Ignore globs | No | Default ignore: `**/bin/**`, `**/obj/**`, `**/node_modules/**`, `**/dist/**`, lockfiles, `.git/**` |

## Ownership sources (read)

Build coverage patterns from, in order:

1. `governance/ownership-matrix.md` (concern + primary owner; path/area text)
2. `governance/agent-registry.yaml` `owns:` lists when present
3. Agent prompt path hints only when clearly listed (do not invent)

## Execution steps

### 1. Resolve pack context

Load specification status, manifest project identity, registry agents, current
matrix. Abort on unrelated pack conflict.

### 2. Collect candidates

**REPO mode (read-only scan):**

- List high-signal roots: solution projects, `apps/`, `services/`, `packages/`,
  `src/` top-level, verified stack folders from the specification / shared
  context when present.
- Prefer depth that matches how the matrix already speaks (projects/folders,
  not every file).

**DIFF mode:**

- `git diff --name-only <base>...<head>` (default head = `HEAD`)
- Apply ignore globs
- Roll up to directory/project prefixes for proposal clarity

### 3. Match coverage

For each candidate path/area:

- **Covered** — matches an existing matrix row or registry `owns` pattern
- **Uncovered** — no owner
- **Ambiguous** — multiple plausible owners

### 4. Propose (always before write)

Present a table:

| Area / path pattern | Suggested primary owner | Confidence | Action |
|---|---|---|---|
| … | existing agent id or `NEEDS_NEW_AGENT` | high/medium/low | add row / reassign / skip / extend-fleet |

Rules for suggestions:

- Prefer an **existing** agent whose current ownership is the closest parent or
  same layer (web → frontend, api → backend, etc.).
- Label guesses `ARCHITECT_PROPOSED`; never present as verified fact.
- If confidence is low or no agent fits → `NEEDS_NEW_AGENT` + point to
  `/extend-fleet` (do not create the agent here).
- Do not remove existing rows unless the user explicitly confirms removals.
- One primary owner per concern; no duplicate primaries.

Ask the user to confirm or edit the proposal (AskQuestion when available).
If the user declines all changes → stop with `OWNERSHIP_SYNC_NO_CHANGES`.

### 5. Apply (SAVE)

Only for confirmed items:

1. Update `governance/ownership-matrix.md`
2. Update registry `owns:` (and manifest notes if the pack records ownership
   there) when the project already uses those fields
3. Optionally add a short note to `governance/decision-register.yaml` /
   change-management that ownership was synced (date + summary)
4. Do **not** rewrite unrelated agent prompt bodies unless a path list inside a
   prompt is the project's sole ownership source and the user confirmed that
   edit
5. Recommend pack semver **patch** (clarification) or **minor** if ownership
   surface grew materially

### 6. Report

1. Mode (`REPO` / `DIFF` / `PREVIEW`)
2. Candidates scanned / covered / uncovered / ambiguous
3. Rows added / updated / skipped
4. `NEEDS_NEW_AGENT` items (if any) → next `/extend-fleet`
5. Files written
6. Result status

## Result status

- `OWNERSHIP_SYNC_READY` — confirmed updates applied (or preview completed cleanly)
- `OWNERSHIP_SYNC_NO_CHANGES` — nothing to do or user declined
- `OWNERSHIP_SYNC_BLOCKED` — missing fleet/spec, or user must choose owners /
  run `/extend-fleet` before a coherent matrix exists

## Safety

- No application implementation
- No secrets
- No silent owner assignment without confirmation
- No whole-pack regenerate (that is `/upgrade-architect` / generate-prompt-pack)
- No new agent files in this workflow
- Preserve `project-specification.md`

## Related

- Chained from: [upgrade-architect.md](upgrade-architect.md)
- New capability gap: [extend-fleet.md](extend-fleet.md) / [create-agent.md](create-agent.md)
- Path index only: [update-context-mapping.md](update-context-mapping.md)
- Diff review (quality, not ownership): [architect-review.md](architect-review.md)
