# Generate Complete Prompt Pack

Use only after an approved `PROJECT REQUIREMENTS AND CURRENT-STATE SPECIFICATION`.

If approval is missing, stop and ask the user to complete discovery first.

Read `agent-system/project-specification.md` first when it exists. Its status
must be `APPROVED` or `APPROVED WITH CHANGES`. Treat it as the authoritative
source for scope, assumptions, provider decisions, recommended agents, and
excluded agents.

If the file is unavailable, use an approved specification supplied in the
conversation. If neither exists, stop instead of reconstructing it from memory.

Full deep checklist: [../../references/source-prompts/03-generate-complete-prompt-pack.md](../../references/source-prompts/03-generate-complete-prompt-pack.md)

## Mission

Generate a project-specific multi-agent prompt pack. Do **not** implement the application.

Specialize agents only for verified or approved providers. Prefer generic capability agents when undecided.

## Existing operating procedures

Before writing agent prompts, read the specification section
**Existing Agent / Skills / Documentation Procedures** (or treat as
`NOT_FOUND` / mode `NONE` when absent).

Follow [existing-operating-procedures.md](existing-operating-procedures.md):

1. Persist adopted procedures in `governance/shared-context.yaml` under
   `existing_operating_procedures`.
2. Map `protocols/execution-workflow.md` onto the project's workflow chain
   when one exists (`FOLLOW` / `COMPOSE` / `BRIDGE`).
3. Require every agent **Operating Principles** section to prefer adopted
   project procedures over Architect defaults.
4. Require the Principal Architect to re-resolve procedures at `/operate`
   start.
5. Never overwrite project skills, host instruction files, `CONTEXT.md`, ADRs,
   or `docs/agents/*` during prompt-pack SAVE.

If adaptation mode is `FOLLOW` or `BRIDGE`, do not invent a second competing
clarify → spec → ticket → implement loop in Architect protocols.

## Generation modes

- `SAVE` (default): create every prompt and governance artifact as a real,
  separate file under `agent-system/`.
- `SAVE_WITH_PLAN`: same as `SAVE`, plus `implementation-plan.md`.
- `SAVE_WITH_PLAN_AND_MAPPING`: same as `SAVE_WITH_PLAN`, plus
  `repository-task-mapping.md`.
- `PREVIEW`: write no files; show a concise proposed tree and representative
  excerpts in chat.

If the user says only `/generate-prompt-pack`, use `SAVE`. Never interpret
“separate files” as “print multiple file-shaped blocks in chat” when workspace
file-writing tools are available.

## Required deliverables

1. Prompt-pack manifest
2. Shared project context
3. Principal Architect / Orchestrator prompt
4. One prompt per approved agent (18-section structure)
5. Agent registry + ownership matrix + contract registry
6. Task-delegation and handoff protocols
7. Execution workflow + approval/quality/integration policies
8. Risk and decision register templates
9. Examples + README + actual file tree in `SAVE` modes

Required layout:

```text
agent-system/
  project-specification.md  # preserve; do not regenerate or delete
  README.md
  manifest.yaml
  governance/
    shared-context.yaml
    agent-registry.yaml
    ownership-matrix.md
    contract-registry.yaml
    approval-gates.md
    quality-gates.md
    integration-policy.md
    conflict-resolution.md
    change-management.md
    risk-register.yaml
    decision-register.yaml
  protocols/
    task-delegation.yaml
    agent-handoff.yaml
    execution-workflow.md
    validation-report.yaml
  agents/
    00-principal-architect.md
    <one-file-per-approved-agent>.md
  examples/
    project-invocation.md
    delegation-examples.md
    handoff-example.yaml
```

## File-writing rules

In `SAVE` modes:

1. Create missing directories.
2. Preserve `agent-system/project-specification.md`.
3. Write each artifact directly to its required path; never combine several
   prompts into one response or one Markdown file.
4. Do not surround a saved agent prompt with an outer code fence.
5. Ensure nested examples inside prompts use balanced fences and valid Markdown.
6. Do not overwrite an unrelated existing prompt pack silently. If
   `manifest.yaml` identifies another project, stop and report the conflict.
7. Update an existing pack for the same project only when the requested mode
   authorizes it; report files added or replaced. `/upgrade-architect`
   authorizes a same-project refresh after a library update.
8. If file-writing tools are unavailable, stop with
   `PROMPT_PACK_BLOCKED` and explain that `PREVIEW` is available. Do not pretend
   chat output is a saved prompt pack.

## Agent prompt required sections

1. Role  
2. Mission  
3. Position in the Agent Fleet  
4. Primary Responsibilities  
5. Explicit Non-Responsibilities  
6. Operating Principles  
7. Input Context  
8. Required Contracts  
9. Dependencies and Handoffs  
10. Execution Workflow  
11. Technical Standards  
12. Security, Privacy, and Compliance Guardrails  
13. Error and Uncertainty Handling  
14. Required Output Format  
15. Quality Gates  
16. Definition of Done  
17. Escalation Conditions  
18. Prohibited Behaviors  

## Consistency checks before finishing

- No duplicate ownership
- No excluded agents generated
- Provider specialization matches approved decisions
- Proposed vs approved contracts are distinct
- No secrets
- No fabricated test/deploy claims
- Adopted existing operating procedures are reflected in shared context,
  execution workflow, and agent Operating Principles (or explicitly `NONE`)
- Project skill / host-instruction / domain-memory files were not overwritten

## Save verification

Before reporting a `SAVE` mode as ready:

1. Read the generated file tree from the workspace.
2. Confirm every manifest path exists.
3. Confirm every registered agent has a non-empty prompt file.
4. Confirm every agent prompt contains all 18 required section headings.
5. Confirm governance and protocol files are separate and non-empty.
6. Confirm `project-specification.md` still exists and remains approved.
7. When PowerShell is available, run:
   `powershell -NoProfile -File scripts/validate-agent-system.ps1`
8. Report any missing or malformed artifact as `PROMPT_PACK_BLOCKED`; do not
   downgrade a missing file to a “documented limitation.”

## Response after saving

Do not dump all generated file contents into chat. Return only:

1. Generation mode.
2. Files created, updated, skipped, or blocked.
3. Agent fleet summary.
4. Unresolved approved/proposed decisions.
5. Verification results.
6. Exact next command: `/operate`.
7. The `/operate` options from
   [operate-agent-system.md](operate-agent-system.md), with a contextual
   recommendation when justified.

## Result status

Choose one:

- `PROMPT_PACK_READY`
- `PROMPT_PACK_READY_WITH_DOCUMENTED_LIMITATIONS`
- `PROMPT_PACK_BLOCKED`
