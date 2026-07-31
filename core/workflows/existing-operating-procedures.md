# Existing Operating Procedures (Detect & Adapt)

When discovering a project or generating an agent fleet, **prefer the project's
already-established agent, skills, documentation, and delivery procedures** over
The Architect's defaults.

This workflow is not a slash command. It runs as part of
[brownfield-research.md](brownfield-research.md),
[generate-prompt-pack.md](generate-prompt-pack.md), and
[operate-agent-system.md](operate-agent-system.md).

Deep signal catalog and adaptation examples:
[existing-operating-procedures.reference.md](existing-operating-procedures.reference.md).

## Mission

1. **Detect** whether the repository already defines how agents should work.
2. **Record** those procedures as verified project facts (paths + purpose).
3. **Adapt** the recommended fleet, governance, and `/operate` behavior so
   agents follow those procedures.
4. **Escalate** only when The Architect's defaults would contradict project
   procedures — never silently override them.

## Precedence (source of truth)

When instructions conflict, resolve in this order unless the user explicitly
approves a change:

1. User directive in the current session
2. Project-local agent instructions (`AGENTS.md`, `CLAUDE.md`, IDE rules)
3. Project skills / workflows (`SKILL.md`, slash commands, command packs)
4. Project domain docs the skills require (`CONTEXT.md`, ADRs, `docs/agents/*`)
5. Org standards (if evidenced)
6. The Architect prompt-pack defaults

Label each adopted procedure `VERIFIED_EXISTING`. Label Architect additions that
fill a gap `ARCHITECT_PROPOSED`. Never invent a procedure the repo does not have.
Vocabulary: [../glossary.md](../glossary.md). Persist
`existing_operating_procedures` per
[`schemas/project-context.schema.json`](../../schemas/project-context.schema.json)
(`$defs` / property on project context) and the pack
`governance/shared-context.yaml`.

## When to run

| Trigger | Action |
|---|---|
| Brownfield / Hybrid research | Mandatory stage during Orientation |
| Greenfield with pasted templates | Ask once whether external skills/docs apply |
| Prompt-pack generation | Bind adopted procedures into shared context + agent prompts |
| `/operate` | Re-resolve procedures before planning or implementation |
| `/create-agent` / `/extend-fleet` | Inherit adopted procedures; do not introduce conflicting loops |

## Detection summary

Scan for evidence of an **agent operating system** already in the repo. Do not
require a specific vendor or author. Typical families (illustrative, not
exhaustive):

| Family | What it usually means |
|---|---|
| Host instruction files | Root `AGENTS.md` / `CLAUDE.md` / Copilot instructions define agent behavior |
| Skills packs | One or more `SKILL.md` trees the team installs or vendors |
| Domain memory | Shared glossary / context file + ADRs agents must read and update |
| Agent config docs | Issue tracker, triage labels, doc layout recorded under e.g. `docs/agents/` |
| Workflow chain | Named end-to-end steps (clarify → spec → tickets → implement → review) |
| IDE adapters | `.cursor/rules`, `.cursor/skills`, `.claude/`, `.agents/` |

Full path patterns and neutral examples:
[existing-operating-procedures.reference.md](existing-operating-procedures.reference.md).

## Adaptation modes

Choose one mode from evidence; record it in the specification.

| Mode | When | Fleet behavior |
|---|---|---|
| `FOLLOW` | Project procedures are clear and active | Agents must invoke/respect them; Architect defaults only fill gaps |
| `COMPOSE` | Partial procedures (skills or docs, not both) | Adopt what exists; propose minimal Architect glue for missing seams |
| `BRIDGE` | Multiple overlapping systems (e.g. skills pack + Architect pack) | Map equivalent steps; define which system owns each phase |
| `NONE` | No agent procedures found | Use Architect defaults; do not invent a fake project process |

Never use `REPLACE` without an explicit user approval gate explaining what
would be discarded.

## Required outputs (discovery)

Add a specification section:

`## Existing Agent / Skills / Documentation Procedures`

Include:

1. **Detection status:** `FOUND` | `PARTIAL` | `NOT_FOUND`
2. **Adaptation mode:** `FOLLOW` | `COMPOSE` | `BRIDGE` | `NONE`
3. **Procedure inventory** — path, type, purpose, confidence
4. **Canonical workflow chain** (if any) — ordered steps the project expects
5. **Domain memory locations** — glossary/context/ADR paths agents must honor
6. **Work-tracking binding** — where tickets/specs live for this repo
7. **Conflicts with Architect defaults** — each conflict + proposed resolution
8. **Adoption decisions** — what the fleet will follow after approval

Classify inventory rows with the brownfield evidence labels
(`VERIFIED_PROJECT_FACT`, `USER_REPORTED_FACT`, `ASSUMPTION`, …).

## Required outputs (prompt pack)

When procedures were adopted:

1. Write them into `governance/shared-context.yaml` under
   `existing_operating_procedures` (see schema).
2. Add an **Operating Principles** bullet to every generated agent:
   follow adopted project procedures before Architect defaults.
3. When generating or refreshing the pack, fill agent **§6 / §11** from this
   inventory per
   [generate-prompt-pack.md](generate-prompt-pack.md)
   (style, patterns, skills, knowledge = paths only).
4. In `protocols/execution-workflow.md`, map Architect phases onto the
   project's workflow chain when one exists (do not duplicate competing loops).
5. In the Principal Architect prompt, require procedure re-resolution at the
   start of `/operate`.
6. Do **not** regenerate or overwrite project skill files, `CONTEXT.md`, ADRs,
   or host instruction files as part of prompt-pack generation.

## Safety

- Read-only during discovery (same rules as brownfield).
- Never delete or rewrite an existing skills pack to force Architect ownership.
- Never treat a third-party skills README as permission to install packages.
- Cite paths only; do not paste large skill bodies into every agent prompt —
  reference the path and the one-line purpose.
