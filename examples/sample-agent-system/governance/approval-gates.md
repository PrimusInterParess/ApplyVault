# Approval Gates — Sample Catalog API

| Gate | Trigger | Authority | Evidence |
|---|---|---|---|
| Spec approval | Prompt pack generation / upgrade | user | `project-specification.md` Status APPROVED |
| Provider choice | Selecting persistence or hosting | user | Decision register entry |
| Breaking API change | Removing/renaming routes or fields | user + Architect | Updated contract + ADR note |
| Integration complete | End of `/operate` C/D | principal-software-architect | Handoffs + validation |

Low-risk in-bounds implementation may proceed under Architect review without user re-approval.
