# Agent System Builder (Router)

IDE-agnostic entry workflow for The Architect. Route the request; do not invent providers.

Also see: [../slash-commands.md](../slash-commands.md)

## Quick start

1. Classify intent (table below) or honor an explicit slash command.
2. Apply provider-neutral rules.
3. Hand off to the matching workflow.
4. Never skip discovery for a new/unknown project before fleet generation.

## Intent routing

| Intent signals | Mode | Action |
|---|---|---|
| `/update-context` | Phase 2 | [update-context-mapping.md](update-context-mapping.md) |
| `/upgrade-architect` | Phase 2 | [upgrade-architect.md](upgrade-architect.md) — refresh fleet after library update |
| `/architect`, start Architect, Prompt Factory | Router | This file |
| `/discover`, greenfield discovery | `DISCOVER_PROJECT` | [project-discovery.md](project-discovery.md) (ask A/B/C if needed) |
| `/brownfield`, analyze existing repo | Brownfield | [brownfield-research.md](brownfield-research.md) |
| `/hybrid`, existing + major new features | Hybrid | [brownfield-research.md](brownfield-research.md) then discovery follow-up |
| `/generate-prompt-pack`, GENERATE_COMPLETE_PROMPT_PACK, BUILD_FLEET | Phase 2 | [generate-prompt-pack.md](generate-prompt-pack.md) � requires approved spec |
| `/create-agent`, CREATE_AGENT | Phase 2 | [create-agent.md](create-agent.md) |
| `/extend-fleet`, EXTEND_FLEET | Phase 2 | [extend-fleet.md](extend-fleet.md) |
| `/audit`, AUDIT_PROMPTS | Phase 2 | [audit-prompts.md](audit-prompts.md) |
| `/operate`, delegate, handoff | Phase 2 | [operate-agent-system.md](operate-agent-system.md) |
| UPDATE_ORCHESTRATOR | Phase 2 | Prefer [extend-fleet.md](extend-fleet.md) or [create-agent.md](create-agent.md) Architect update blocks |

## First question when mode is unknown

Use the host's native choice tool when available so the project modes are
clickable. In Cursor, use `AskQuestion`. Use the following text only as a
fallback:

```text
Which project mode best matches the work?

A. GREENFIELD � a future project without an existing implementation.
   Best when: you are designing a new product or system from requirements.
   Result: requirements interview first; there is no repository to research.

B. BROWNFIELD � an existing project that should be researched first.
   Best when: the goal is to understand, document, audit, or improve what exists.
   Result: read-only repository analysis before recommendations.

C. HYBRID � an existing project receiving significant new functionality.
   Best when: current contracts must be preserved while planning major new work.
   Result: repository research first, then discovery for the new capability.

D. Other � describe your situation.

Recommended: the agent marks A, B, or C from the known context and gives one
short reason. If there is not enough context, the recommendation is omitted.

Reply with A, B, C, D, or describe your situation.
```

- **A** ? [project-discovery.md](project-discovery.md)
- **B** or **C** ? [brownfield-research.md](brownfield-research.md)

## Provider-neutral foundation

- Capability before provider.
- Source-of-truth order: verified evidence ? user-confirmed ? org standard ? constraints ? preference ? approved recommendation ? labeled assumption.
- Decision states: `VERIFIED_EXISTING`, `USER_REQUIRED`, `ORGANIZATION_REQUIRED`, `USER_PREFERRED`, `ARCHITECT_PROPOSED`, `UNDECIDED`, `NOT_APPLICABLE`, `MIGRATION_PLANNED`.
- Prefer minimal architecture.
- Prefer project-local agent/skills/documentation procedures when present —
  see [existing-operating-procedures.md](existing-operating-procedures.md).
- Never fabricate files, command results, tests, or deployments.
- Never request or echo secrets.

## Phase gates

1. Discovery ends with `APPROVAL REQUIRED`.
2. After `APPROVED` / `APPROVED WITH CHANGES`, automatically run
   [generate-prompt-pack.md](generate-prompt-pack.md) in `SAVE` mode.
3. Do not ask whether to create the selected agents after approval.
4. Do not generate agents before approval or implement the application during
   prompt-pack generation.

## Schemas

- [../../schemas/project-context.schema.json](../../schemas/project-context.schema.json)
- [../../schemas/capability-matrix.schema.json](../../schemas/capability-matrix.schema.json)
- [../../schemas/discovery-ledger.schema.json](../../schemas/discovery-ledger.schema.json)
- [../../schemas/requirements-spec.schema.json](../../schemas/requirements-spec.schema.json)

## Prohibited

- Generating agent prompts during discovery
- Editing repositories during discovery without authorization
- Selecting providers without evidence or approval
- Claiming unexecuted builds/tests/deploys
