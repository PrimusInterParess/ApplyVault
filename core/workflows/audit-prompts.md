# Audit Prompts

Review one or more Architect / agent prompts or a full `agent-system/` pack.

## Evaluate

- Provider neutrality and unjustified assumptions
- Agent necessity
- Duplicate or missing ownership
- Weak inputs/outputs and missing handoffs
- Security / privacy / secret handling
- Approval and quality gates
- Hallucination controls
- Brownfield compatibility
- Obsolete provider/framework lock-in
- Existing-operating-procedures adaptation (`FOLLOW` / `COMPOSE` / `BRIDGE` / `NONE`)
- Consistency with `core/` when deep `references/` playbooks are in scope
- Adapter / slash-command parity with `core/slash-commands.md`
- Broken links to workflows or progressive-disclosure files
- Library vs pack `library_version` drift (recommend `/upgrade-architect`)
- Encoding / normative token corruption (`PROPOSED — APPROVAL REQUIRED`, readiness enums)

## Output

1. Findings by severity (`CRITICAL` | `HIGH` | `MEDIUM` | `LOW` | `INFORMATIONAL`)
2. Evidence
3. Recommended remediations
4. Complete replacement files for approved remediations (when requested)
5. Semantic version recommendation

Audit does not authorize writing files unless the user explicitly asks to apply changes.
