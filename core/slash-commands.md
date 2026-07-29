# Slash / command map (IDE-agnostic)

When the user types a slash command or an equivalent phrase, load the matching workflow and execute it.

| Command | Equivalent phrases | Workflow |
|---|---|---|
| `/discover` | start discovery, greenfield discovery | [workflows/project-discovery.md](workflows/project-discovery.md) after mode A is known; otherwise [workflows/agent-system-builder.md](workflows/agent-system-builder.md) |
| `/brownfield` | analyze existing repo, brownfield research | [workflows/brownfield-research.md](workflows/brownfield-research.md) |
| `/hybrid` | existing project + new features | [workflows/brownfield-research.md](workflows/brownfield-research.md) in Hybrid mode |
| `/generate-prompt-pack` | GENERATE_COMPLETE_PROMPT_PACK, build agent fleet prompts | [workflows/generate-prompt-pack.md](workflows/generate-prompt-pack.md) |
| `/create-agent` | CREATE_AGENT, new specialized agent | [workflows/create-agent.md](workflows/create-agent.md) |
| `/extend-fleet` | EXTEND_FLEET, add agent to existing pack | [workflows/extend-fleet.md](workflows/extend-fleet.md) |
| `/audit` | AUDIT_PROMPTS, review agent prompts | [workflows/audit-prompts.md](workflows/audit-prompts.md) |
| `/operate` | operate agents, delegate, handoff | [workflows/operate-agent-system.md](workflows/operate-agent-system.md) |
| `/update-context` | refresh where context artifacts live | [workflows/update-context-mapping.md](workflows/update-context-mapping.md) |
| `/upgrade-architect` | refresh fleet after library update, sync agent prompts | [workflows/upgrade-architect.md](workflows/upgrade-architect.md) |
| `/architect` | start The Architect, Prompt Factory | [workflows/agent-system-builder.md](workflows/agent-system-builder.md) |

## Rules

1. Prefer the exact workflow file over paraphrasing.
2. If prerequisites are missing (e.g. no approved specification for `/generate-prompt-pack`), stop and say what is required.
3. Commands never invent providers or secrets.
4. Phase 1 discovery still ends at `APPROVAL REQUIRED`.
5. `/generate-prompt-pack` defaults to `SAVE`: write separate files under
   `agent-system/`, verify them, and return a short summary. Use `PREVIEW` only
   when the user explicitly asks not to write files.
6. Brownfield/Hybrid discovery and fleet generation must detect and adapt to
   existing project agent/skills/documentation procedures per
   [workflows/existing-operating-procedures.md](workflows/existing-operating-procedures.md).
7. After a library update, use `/upgrade-architect` to regenerate
   `agent-system/` prompts from the approved specification so agent docs match
   current `core/` rules. Library file copy alone does not rewrite the fleet.
