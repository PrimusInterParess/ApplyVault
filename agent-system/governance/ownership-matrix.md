# Ownership Matrix — ApplyVault

| Area | Primary | Secondary | Notes |
|---|---|---|---|
| Orchestration / `/operate` | principal-software-architect | — | BRIDGE to project skills |
| Product framing / Issues quality | product-manager | principal-software-architect | `gh` + triage labels |
| `frontend/applyvault-jobs-ui/` | frontend-engineer | ui-ux-designer, qa-engineer | Angular 19 |
| `extension/` | browser-extension-engineer | backend-engineer, qa-engineer | MV3 |
| `api/ApplyVault.Api/` | backend-engineer | ai-llm-engineer, platform-engineer, qa-engineer | net10.0 |
| `shared/cv-section-catalog/` | backend-engineer | frontend-engineer, ai-llm-engineer | ADR-0001 |
| Gemini / GoogleAi* services | ai-llm-engineer | backend-engineer | No secret values in output |
| Dashboard UX | ui-ux-designer | frontend-engineer | Respect job-results rule |
| Tests & quality evidence | qa-engineer | relevant engineer | Extension CI gap known |
| CI / config / Redis / storage / health | platform-engineer | backend-engineer | `.github/workflows/api-ci.yml` |
| `.agents/skills`, `docs/agents/` | principal-software-architect | product-manager | Do not overwrite skills |
| `CONTEXT.md`, `docs/adr/` | principal-software-architect | domain-aware engineers | Update only via domain-modeling skill norms |

## Conflict rule

If two agents claim the same path, principal-software-architect resolves using this matrix and `governance/conflict-resolution.md`. GitHub Issues remain the work-tracking source of truth.
