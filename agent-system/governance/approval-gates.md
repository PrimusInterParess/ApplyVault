# Approval Gates — ApplyVault

## Always require human approval

- Changing identity provider away from Supabase
- Introducing payments/billing
- Architecture rewrite or major REPLACE decisions
- Overwriting `.agents/skills`, `CONTEXT.md`, ADRs, or `docs/agents/*`
- Shipping secrets, tokens, or production credentials into the repo or chat
- Force-push / destructive git operations (per user/repo rules)

## Require Architect + human when material

- Cross-cutting contract changes (catalog fields, scrape DTO breaking changes)
- Enabling multi-instance Redis/Gmail assumptions in production without evidence
- Expanding fleet with new agent roles

## Authorized by specification approval (already granted)

- Creating/updating this `agent-system/` prompt pack
- `/operate` planning and repository mapping (read-mostly)

## Not authorized by specification approval

- Application implementation until `/operate` options C/D (or explicit user request) authorize it
- Claiming CI green / deploy success without running or observing evidence
