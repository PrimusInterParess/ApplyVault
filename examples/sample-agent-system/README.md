# Sample Catalog API — Agent System

Minimal, complete prompt pack for The Architect demos and CI validation.

- Project: Sample Catalog API (fictional greenfield)
- Fleet: Principal Architect + Architecture Engineer + Backend Engineer
- Provider scope: generic / provider-neutral
- No secrets

## Operate working trees

| Path | Purpose |
|---|---|
| `handoffs/active/<task-id>/` | Thin YAML/MD handoffs only (gitignored) |
| `handoffs/archive/<task-id>/summary.yaml` | Short Close record |
| `scratch/<task-id>/` | Probes, builds, temp — always disposable |

Durable fleet material stays in `agents/`, `governance/`, `protocols/`.
Do not put operate scratch under `docs/architecture/handoffs/`.

If an app already has loose handoffs / debug dumps from older runs, run
`/upgrade-architect` or `/operate` and choose migrate (M1), archive+clean
(M2), or delete (M3) — see The Architect operate workflow **Legacy operate
piles**.

See pack `.gitignore` and `protocols/task-close.yaml`.

Validate from repo root:

```powershell
powershell -NoProfile -File scripts/validate-agent-system.ps1 -AgentSystemPath examples/sample-agent-system
```

Operate with `/operate` after copying or referencing this pack as `agent-system/`.
