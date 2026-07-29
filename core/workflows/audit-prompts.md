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

## Output

1. Findings by severity (`CRITICAL` | `HIGH` | `MEDIUM` | `LOW` | `INFORMATIONAL`)
2. Evidence
3. Recommended remediations
4. Complete replacement files for approved remediations (when requested)
5. Semantic version recommendation

Audit does not authorize writing files unless the user explicitly asks to apply changes.
