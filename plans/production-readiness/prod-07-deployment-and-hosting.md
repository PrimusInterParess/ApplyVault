---
name: Step 7 — Deployment and Hosting
overview: Deploy the ASP.NET Core API to a hosted environment with HTTPS, process management, and environment configuration from step 4.
todos:
  - id: choose-host
    content: Select hosting target (Azure App Service, VPS + systemd, Docker, etc.)
    status: completed
  - id: deploy-artifact
    content: Define publish command and deployment artifact (dotnet publish)
    status: completed
  - id: env-on-host
    content: Configure production env vars on host (connection string, Supabase, CORS)
    status: completed
  - id: https-termination
    content: Enable HTTPS at reverse proxy or platform level
    status: completed
  - id: runbook
    content: Write deploy runbook (build, migrate, restart, rollback)
    status: completed
  - id: smoke-test
    content: Post-deploy smoke test GET /health and authenticated /api/auth/session
    status: completed
isProject: false
---

# Step 7 — Deployment and Hosting

**Tracker:** [production-readiness-tracker.md](../production-readiness-tracker.md) · **Prerequisites:** [prod-05-database-and-migrations.md](prod-05-database-and-migrations.md) · **Next:** [prod-08-frontend-environment-builds.md](prod-08-frontend-environment-builds.md)

## Problem

The API runs locally on `http://localhost:5173`. Production requires a reachable HTTPS endpoint, managed process lifetime, and injected configuration.

## Risk

| Risk | Impact |
|------|--------|
| HTTP only in production | Token interception |
| Manual deploy steps | Inconsistent releases |
| No rollback plan | Extended outage |

## Goal

1. Published API accessible at a stable HTTPS URL (e.g. `https://api.applyvault.example`).
2. Production env vars loaded from host, not repo.
3. Documented deploy and rollback procedure.

## Affected code

| Area | Notes |
|------|-------|
| Entry | [`Program.cs`](../../api/ApplyVault.Api/Program.cs) — no host-specific logic |
| Pipeline | [`WebApplicationExtensions.cs`](../../api/ApplyVault.Api/Infrastructure/WebApplicationExtensions.cs) |
| Health | `GET /health` for platform probes (step 12) |
| Deploy | [`deploy/`](../../deploy/) — Docker Compose, Caddy, runbook, scripts |

## Implementation tasks

### 1. Publish

```bash
dotnet publish api/ApplyVault.Api/ApplyVault.Api.csproj -c Release -o ./publish
```

### 2. Host selection

Document chosen platform and:

- How env vars are set
- How HTTPS is terminated
- How logs are accessed

### 3. Required production env vars

Minimum set (from step 4):

- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__ApplyVault`
- `Supabase__Url`
- `Cors__AllowedOrigins__0` (frontend URL)

### 4. Deploy sequence

1. Run CI (step 6)
2. Apply migrations (step 5 strategy)
3. Deploy new bits
4. Smoke test `/health`
5. Smoke test dashboard auth against prod API

### 5. Single replica default

Until steps 16–17, run **one API instance** or accept in-memory EURES cache and Gmail sync limitations.

## Verification

1. `curl https://<api-host>/health` returns healthy.
2. Dashboard pointed at prod API can sign in and load jobs.
3. Rollback procedure tested once on staging.

## Production-grade notes

- Do not expose SQL Server port publicly; API connects over private network or platform connector.
- Keep OpenAPI (`MapOpenApi`) disabled in Production unless intentionally public.
