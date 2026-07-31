# Extend Fleet

Add one or more agents to an existing prompt pack without breaking ownership.

## Prerequisites

| Check | If missing |
|---|---|
| Approved `agent-system/project-specification.md` | Stop; finish approval |
| Existing pack `manifest.yaml` for **this** project | Stop on foreign pack conflict |
| Capability gap or requested specialization identified | Ask once |

## Modes

- `SAVE` (default when authorized): write agent + governance updates under
  `agent-system/`.
- `PREVIEW`: propose changes without writing files.

## Steps

1. Inventory current agents and ownership matrix.
2. Identify capability gap or requested specialization.
3. Detect overlap with existing agents.
4. Re-check adopted project operating procedures
   ([existing-operating-procedures.md](existing-operating-procedures.md)); new
   agents must follow them.
5. Decide: new agent, specialization of existing agent, or reject as unnecessary.
6. Generate/update:
   - new or revised agent prompt(s) (all 18 sections), inheriting
     [generate-prompt-pack.md](generate-prompt-pack.md) **§6 / §11**
     style–patterns–skills–knowledge binding rules
   - Architect prompt updates
   - registry, ownership matrix, contract registry
   - workflow / handoff updates
7. In `SAVE` mode: write files, preserve
   `project-specification.md` / plans / mappings, verify headings, run
   `scripts/validate-agent-system.ps1` when PowerShell is available.
8. Recommend semantic version bump:
   - patch: clarification only
   - minor: backward-compatible addition
   - major: breaking ownership/contract change
9. Provide migration instructions (including `/upgrade-architect` if library
   rules also changed).

Stop and escalate if the change requires an undecided provider selection or a
breaking public contract without approval.

## Output

1. Decision (add / specialize / reject)
2. Mode and files created/updated/skipped
3. Verification result
4. Version bump recommendation
5. Next step: `/operate` or `/audit`
