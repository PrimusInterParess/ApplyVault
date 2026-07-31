# Shared vocabulary (decision states and ledgers)

Use this glossary so builder, discovery, brownfield, EOP, and schemas stay aligned.

## Provider / capability decision states (builder + agents)

Use these on capability matrices and agent specialization decisions:

| State | Meaning |
|---|---|
| `VERIFIED_EXISTING` | Confirmed from repository or runtime evidence |
| `USER_REQUIRED` | User mandated |
| `ORGANIZATION_REQUIRED` | Org standard mandated |
| `USER_PREFERRED` | User preference (not yet required) |
| `ARCHITECT_PROPOSED` | Architect recommendation awaiting approval |
| `UNDECIDED` | Capability needed; provider not chosen |
| `NOT_APPLICABLE` | Capability out of scope |
| `MIGRATION_PLANNED` | Change from current verified provider planned |

## Discovery ledger fact classes

Use these in discovery / brownfield ledgers (`schemas/discovery-ledger.schema.json`):

| Class | Meaning |
|---|---|
| `VERIFIED_PROJECT_FACT` | Evidence-backed project fact |
| `USER_CONFIRMED_REQUIREMENT` | Explicit user confirmation |
| `ORGANIZATION_STANDARD` | Confirmed org standard |
| `PROPOSED_DECISION` | Proposal awaiting approval |
| `SAFE_ASSUMPTION` | Clearly labeled assumption |
| `UNKNOWN` | Not yet determined |

## Mapping (do not mix casually)

| Builder / EOP state | Closest ledger class when recording evidence |
|---|---|
| `VERIFIED_EXISTING` | `VERIFIED_PROJECT_FACT` |
| `USER_REQUIRED` | `USER_CONFIRMED_REQUIREMENT` |
| `ORGANIZATION_REQUIRED` | `ORGANIZATION_STANDARD` |
| `ARCHITECT_PROPOSED` / `USER_PREFERRED` | `PROPOSED_DECISION` |
| `UNDECIDED` | `UNKNOWN` |
| Labeled assumption | `SAFE_ASSUMPTION` |

## Existing operating procedures modes

| Mode | Meaning |
|---|---|
| `FOLLOW` | Honor project procedures as primary |
| `COMPOSE` | Merge project procedures with Architect gates |
| `BRIDGE` | Map Architect steps onto project chain |
| `NONE` | No project procedures found / not adopted |

## Agent identity vs filename

| Concept | Example |
|---|---|
| Registry / delegation **agent id** | `principal-software-architect` |
| Prompt **filename** | `agents/00-principal-architect.md` |

Always record both in `governance/agent-registry.yaml` (`id` + `prompt_file`).

## Spec / pack readiness tokens

- `APPROVED`
- `APPROVED WITH CHANGES`
- `PROPOSED — APPROVAL REQUIRED`
- `NOT_READY — BLOCKING_INFORMATION_REQUIRED`

## Operate handoff / request status

| Layer | Tokens | Retention |
|---|---|---|
| Agent handoff | `READY`, `BLOCKED`, `STALE`, `NEEDS_REVIEW` | `READY`/`BLOCKED` stay in `handoffs/active/<task-id>/` until Close; `STALE` must not integrate |
| Request close | `REQUEST COMPLETE`, `REQUEST COMPLETE WITH DOCUMENTED LIMITATIONS`, `REQUEST BLOCKED`, `ABANDONED` | On complete/limitations/abandoned: archive summary + wipe scratch + clear active |

Operate paths: `agent-system/handoffs/{active,archive}/`,
`agent-system/scratch/<task-id>/`. Same task id with existing data → ask
**Resume** vs **Fresh start** before continuing. See
[workflows/operate-agent-system.md](workflows/operate-agent-system.md).

## Style, skills, and knowledge

Code style, design patterns, skills, and domain knowledge are **project paths
first**, not silent Architect library defaults. Agent §6 / §11 must cite
verified paths, and **may propose better alternatives** as
`ARCHITECT_PROPOSED` (approval required before adoption). Never paste full
skill or `CONTEXT.md` bodies into every agent prompt. See
[workflows/generate-prompt-pack.md](workflows/generate-prompt-pack.md).
