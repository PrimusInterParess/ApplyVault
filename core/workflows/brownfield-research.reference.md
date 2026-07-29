# Brownfield Research � Reference

## Stage details

### Stage 1 � Orientation

Root tree, README, contribution guides, solution/workspace files, package manifests and locks, build config, architecture docs, ADRs.

Examples: `README.md`, `package.json`, `*.sln`, `*.csproj`, `pyproject.toml`, `go.mod`, `Cargo.toml`, `pom.xml`, `build.gradle`.

### Stage 2 � Existing agent / skills / documentation procedures

Run [existing-operating-procedures.md](existing-operating-procedures.md). Signal
catalog: [existing-operating-procedures.reference.md](existing-operating-procedures.reference.md).

High-signal paths (illustrative, not exhaustive):

- Host instructions: `AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md`,
  `.cursor/rules/**`, `.cursorrules`
- Skills: `**/SKILL.md`, `skills/**`, `.cursor/skills/**`, `.claude/skills/**`,
  `.agents/skills/**`
- Domain memory: `CONTEXT.md`, `CONTEXT-MAP.md`, `docs/adr/**`, glossaries
- Agent config: `docs/agents/**`, sections titled like `## Agent skills`
- Prior Architect pack: `agent-system/**`

Record adaptation mode and procedure inventory before recommending the fleet.
Prefer project procedures over Architect defaults when both exist.

### Stage 3 � Application topology

Frontends, APIs, libraries, domain modules, workers, schedulers, data-access layers, integrations.

### Stage 4 � Contracts and data

OpenAPI/GraphQL, DTOs, events, entities, migrations, validation, serialization, error conventions, sources of truth.

### Stage 5 � Identity and authorization

SDKs, middleware, callbacks, token/session validation, roles/policies, tenancy, user sync, FE guards vs BE enforcement. No tenant secrets.

### Stage 6 � Payments

SDK usage, checkout/portal, webhooks, signature verification, raw body, idempotency, entitlement sync, dunning. No Stripe/other secrets.

### Stage 7 � AI

Model clients, prompts/versions, structured outputs, embeddings/RAG, untrusted-content boundaries, validation, retries, cost/latency controls.

### Stage 8 � Infrastructure and delivery

Docker, IaC, hosting manifests, CI/CD YAML, approvals, secret refs (names only), workload identity, rollback.

### Stage 9 � Configuration

Local/test/staging/prod; build vs deploy vs runtime; public vs secret; feature flags; validation. Templates like `.env.example` only.

### Stage 10 � Quality

Unit/integration/contract/E2E/security/performance/AI-eval tests; mocks; coverage; pipeline gates. Reading ? passing.

### Stage 11 � Observability

Logging, correlation, tracing, metrics, health, alerts, dashboards, runbooks, backup/recovery.

## Current-state reconstruction checklist

1. Repository structure  
2. Applications and services  
3. Frontend architecture  
4. Backend architecture  
5. Data architecture  
6. API and event boundaries  
7. Identity and authorization  
8. Billing  
9. AI  
10. Infrastructure  
11. CI/CD  
12. Configuration and secret flow  
13. Quality strategy  
14. Observability  
15. Deployment topology  
16. External dependencies  

Text diagram example (only evidence-supported nodes):

```text
User
  |
  v
Frontend
  |
  v
API
  |----> Database
  |----> ExternalProvider
```

## Hybrid classification

| Label | Meaning |
|---|---|
| `PRESERVE` | Keep as-is |
| `EXTEND` | Add without breaking contracts |
| `REFACTOR` | Restructure; preserve behavior |
| `REPLACE` | Needs explicit justification and approval |
| `REQUIRES_DECISION` | Blocker for design |

## Provider detection examples (illustrative only)

Do not assume these exist. Only record when evidence is found:

- Supabase: `@supabase/*`, `supabase/config.toml`, migrations, RLS
- Auth0: Auth0 SDKs, audience/domain config keys, JWT middleware
- Stripe: Stripe SDK, webhook routes, Checkout
- CI/CD: `azure-pipelines.yml`, `.github/workflows`, `.gitlab-ci.yml`

## Interview after research (Hybrid / missing context)

Reuse Greenfield interview rules from [project-discovery.md](project-discovery.md): one question at a time; capability before provider; do not re-ask what evidence already proved.
