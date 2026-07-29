# Extend Fleet

Add one or more agents to an existing prompt pack without breaking ownership.

## Steps

1. Inventory current agents and ownership matrix.
2. Identify capability gap or requested specialization.
3. Detect overlap with existing agents.
4. Re-check adopted project operating procedures
   ([existing-operating-procedures.md](existing-operating-procedures.md)); new
   agents must follow them.
5. Decide: new agent, specialization of existing agent, or reject as unnecessary.
6. Generate/update:
   - new or revised agent prompt(s)
   - Architect prompt updates
   - registry, ownership matrix, contract registry
   - workflow / handoff updates
7. Recommend semantic version bump:
   - patch: clarification only
   - minor: backward-compatible addition
   - major: breaking ownership/contract change
8. Provide migration instructions.

Stop and escalate if the change requires an undecided provider selection or a breaking public contract without approval.
