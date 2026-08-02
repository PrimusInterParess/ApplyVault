# Ownership Matrix — ApplyVault

| Area | Primary | Secondary | Notes |
|---|---|---|---|
| Orchestration / `/operate` / Close | principal-software-architect | — | BRIDGE to project skills |
| Product framing / Issues quality | product-manager | principal-software-architect | `gh` + triage labels |
| Architecture design / redesign | architecture-engineer | principal-software-architect | Design handoffs; not orchestration |
| `frontend/applyvault-jobs-ui/` | frontend-engineer | ui-ux-designer, qa-engineer | Angular 19 |
| `extension/` | browser-extension-engineer | backend-engineer, qa-engineer | MV3 |
| `api/ApplyVault.Api/` | backend-engineer | ai-llm-engineer, platform-engineer, qa-engineer | net10.0 |
| `shared/cv-section-catalog/` | backend-engineer | frontend-engineer, ai-llm-engineer | ADR-0001 |
| Gemini / GoogleAi* services | ai-llm-engineer | backend-engineer | No secret values in output |
| Interview Prep API (`api/interview-prep/*`) | backend-engineer | ai-llm-engineer | ADR-0012; controller/service/DTO primary backend; Gemini client stays under Gemini / GoogleAi* |
| Interview Prep UI (`features/interview-prep/`) | frontend-engineer | ui-ux-designer | Route `/interview-prep`; deep-link `jobId` |
| Dashboard UX | ui-ux-designer | frontend-engineer | Respect job-results rule |
| Tests & quality evidence | qa-engineer | relevant engineer | Extension CI gap known |
| PR/diff review / `/architect-review` | code-review-engineer | principal-software-architect | Split from QA; no host publish |
| CI / config / Redis / storage / health | platform-engineer | backend-engineer | `.github/workflows/api-ci.yml` |
| `.agents/skills`, `docs/agents/` | principal-software-architect | product-manager | Do not overwrite skills |
| `CONTEXT.md`, `docs/adr/` | principal-software-architect | domain-aware engineers | Update only via domain-modeling skill norms |
| Root `AGENTS.md` (BRIDGE) | principal-software-architect | — | Skills guide + Architect pointer |

## Conflict rule

If two agents claim the same path, principal-software-architect resolves using this matrix and `governance/conflict-resolution.md`. GitHub Issues remain the work-tracking source of truth.

## Review split

- **qa-engineer** — test strategy, matrices, evidence that tests exist/run when tasked.
- **code-review-engineer** — intent vs diff, maintainability, architecture/security findings for `/architect-review` and operate validation.
- **architecture-engineer** — authors design/redesign; does not own PR review findings.
