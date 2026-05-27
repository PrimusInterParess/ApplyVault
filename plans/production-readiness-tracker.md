---
name: Production Readiness Tracker
overview: Master index for production hardening. Implement steps in order; each step has a dedicated plan under plans/prod-NN-*.md. Do not skip steps 1–2 before multi-user hosting.
todos:
  - id: prod-01
    content: Scrape ingest authentication
    status: completed
  - id: prod-02
    content: Multi-tenant data isolation
    status: completed
  - id: prod-03
    content: API integration tests (tenancy)
    status: completed
  - id: prod-04
    content: API environment configuration
    status: completed
  - id: prod-05
    content: Database and migrations
    status: completed
  - id: prod-06
    content: CI pipeline
    status: completed
  - id: prod-07
    content: Deployment and hosting
    status: completed
  - id: prod-08
    content: Frontend environment builds
    status: completed
  - id: prod-09
    content: Extension production config
    status: completed
  - id: prod-10
    content: OAuth redirects and secrets
    status: completed
  - id: prod-11
    content: CORS and transport security
    status: completed
  - id: prod-12
    content: Health checks and readiness
    status: completed
  - id: prod-13
    content: Logging and monitoring
    status: completed
  - id: prod-14
    content: Rate limiting
    status: completed
  - id: prod-15
    content: Frontend critical-path tests
    status: completed
  - id: prod-16
    content: EURES cache (multi-instance)
    status: pending
  - id: prod-17
    content: Gmail sync (multi-instance)
    status: pending
isProject: true
---

# Production Readiness Tracker

## Consecutive implementation rules

1. **Do steps in numeric order** unless a plan explicitly marks a step as deferrable.
2. **Step 1 before step 2:** stop new orphan rows before migrating or tightening queries.
3. **Steps 16–17** are required only when running **more than one API replica**; until then, document single-replica in the deploy runbook.
4. **Parallel allowed:** step 6 (CI) after 3–5; step 15 (FE tests) after 8.

## Status

| Step | Plan | Phase | Status | Blocked by | Notes |
|------|------|-------|--------|------------|-------|
| 1 | [prod-01-scrape-ingest-auth.md](prod-01-scrape-ingest-auth.md) | A Security | done | — | |
| 2 | [prod-02-tenancy-isolation.md](prod-02-tenancy-isolation.md) | A Security | done | 1 | |
| 3 | [prod-03-api-integration-tests.md](prod-03-api-integration-tests.md) | A Security | done | 2 | |
| 4 | [production-readiness/prod-04-api-environment-configuration.md](production-readiness/prod-04-api-environment-configuration.md) | B Config | done | 2 | |
| 5 | [production-readiness/prod-05-database-and-migrations.md](production-readiness/prod-05-database-and-migrations.md) · [DATABASE.md](production-readiness/DATABASE.md) | B Config | done | 4 | Option B default in Production |
| 6 | [production-readiness/prod-06-ci-pipeline.md](production-readiness/prod-06-ci-pipeline.md) | B Config | done | — | `.github/workflows/api-ci.yml` |
| 7 | [production-readiness/prod-07-deployment-and-hosting.md](production-readiness/prod-07-deployment-and-hosting.md) | C Staging | done | — | Docker + Caddy in `deploy/` · [RUNBOOK](../../deploy/RUNBOOK.md) |
| 8 | [production-readiness/prod-08-frontend-environment-builds.md](production-readiness/prod-08-frontend-environment-builds.md) · [FRONTEND.md](production-readiness/FRONTEND.md) | C Staging | done | 7 | `environment.*.ts`, `angular.json` fileReplacements |
| 9 | [production-readiness/prod-09-extension-production-config.md](production-readiness/prod-09-extension-production-config.md) · [EXTENSION.md](production-readiness/EXTENSION.md) | C Staging | done | 8 | `environment.*.ts`, manifest variants, `npm run build:production` |
| 10 | [production-readiness/prod-10-oauth-redirects-and-secrets.md](production-readiness/prod-10-oauth-redirects-and-secrets.md) · [OAUTH.md](production-readiness/OAUTH.md) | D Hardening | done | 9 | Startup HTTPS validation; [OAUTH.md](production-readiness/OAUTH.md) + `deploy/.env.example` |
| 11 | [production-readiness/prod-11-cors-and-transport-security.md](production-readiness/prod-11-cors-and-transport-security.md) | D Hardening | done | 10 | HTTPS origins validation; HSTS at Caddy edge; [RUNBOOK](../../deploy/RUNBOOK.md) |
| 12 | [production-readiness/prod-12-health-checks-and-readiness.md](production-readiness/prod-12-health-checks-and-readiness.md) | D Hardening | done | 7 | `/health` readiness + DB; `/health/live` liveness; Docker healthcheck |
| 13 | [production-readiness/prod-13-logging-and-monitoring.md](production-readiness/prod-13-logging-and-monitoring.md) | D Hardening | done | 7 | JSON console; auth ops table; 4xx/5xx request logging; [RUNBOOK](../../deploy/RUNBOOK.md) |
| 14 | [production-readiness/prod-14-rate-limiting.md](production-readiness/prod-14-rate-limiting.md) | D Hardening | done | 11 | Partition limits; 429 + Retry-After; [RUNBOOK](../../deploy/RUNBOOK.md) |
| 15 | [production-readiness/prod-15-frontend-critical-path-tests.md](production-readiness/prod-15-frontend-critical-path-tests.md) | E Quality | done | 8 | Karma specs; `npm run test:ci`; CI `frontend-ci` job |
| 16 | [production-readiness/prod-16-eures-cache-multi-instance.md](production-readiness/prod-16-eures-cache-multi-instance.md) | E Scale | pending | 7 | Defer if 1 replica |
| 17 | [production-readiness/prod-17-gmail-sync-multi-instance.md](production-readiness/prod-17-gmail-sync-multi-instance.md) | E Scale | pending | 7 | Defer if 1 replica |

## Related plans

- [hosted_auth_plan.md](hosted_auth_plan.md) — Supabase JWT (already implemented)
- [production-readiness/README.md](production-readiness/README.md) — Steps 4–17 plan index

## Shortcuts (solo beta, single server)

| Can defer | Cannot skip |
|-----------|-------------|
| 16, 17 | 1, 2 |
| 15 (briefly) | 4, 7, 8, 10, 11 |
| 13 (light logging) | |
