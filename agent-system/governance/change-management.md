# Change Management — ApplyVault

## Prompt pack changes

- Same-project refresh after Architect library update: `/upgrade-architect`
  (regenerates pack, auto-adds default reviewer/architecture agents, then
  ownership sync)
- Do not use install `-Force` for routine upgrades; prefer
  `scripts/update-into-project.*` then `/upgrade-architect`
- Preserve `project-specification.md` unless discovery revises it
- Ownership matrix refresh without full pack regen: `/update-ownership`

## Application changes

1. Prefer GitHub Issue / tickets via project skills
2. Implement in small vertical slices
3. Keep ADRs and `CONTEXT.md` updated when domain language or decisions change (via domain-modeling norms)
4. Update contracts registry when approved contracts change

## Communication

- Handoffs must list files touched and residual risks
- No secret values in registers or examples
