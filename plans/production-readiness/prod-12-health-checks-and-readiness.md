---
name: Step 12 — Health Checks and Readiness
overview: Complete health and readiness endpoints for production orchestrators and load balancers.
todos:
  - id: liveness-vs-readiness
    content: Document /health (readiness with DB) vs /api/health (simple liveness)
    status: completed
  - id: health-json
    content: Optional HealthChecks UI response format for platform probes
    status: completed
  - id: platform-probes
    content: Configure host probes (Azure, k8s) to hit GET /health
    status: completed
  - id: unhealthy-db
    content: Verify unhealthy DB marks instance not ready (stop traffic)
    status: completed
  - id: startup-probe
    content: Account for migration time at startup in probe initialDelay
    status: completed
isProject: false
---

# Step 12 — Health Checks and Readiness

**Tracker:** [production-readiness-tracker.md](../production-readiness-tracker.md) · **Prerequisites:** [prod-07-deployment-and-hosting.md](prod-07-deployment-and-hosting.md) · **Next:** [prod-13-logging-and-monitoring.md](prod-13-logging-and-monitoring.md)

## Problem

Two health endpoints exist:

| Endpoint | Behavior |
|----------|----------|
| `GET /health` | ASP.NET health checks + EF Core DB ([`ServiceCollectionExtensions`](../../api/ApplyVault.Api/Infrastructure/ServiceCollectionExtensions.cs)) |
| `GET /api/health` | Simple `{ status: "ok" }` from [`HealthController`](../../api/ApplyVault.Api/Controllers/HealthController.cs) |

Production hosts need a documented probe strategy and failure behavior.

## Partial work already done

- `AddHealthChecks().AddDbContextCheck<ApplyVaultDbContext>()`
- `MapHealthChecks("/health")` in [`WebApplicationExtensions`](../../api/ApplyVault.Api/Infrastructure/WebApplicationExtensions.cs)

## Risk

| Risk | Impact |
|------|--------|
| Probe wrong endpoint | Unhealthy instances keep receiving traffic |
| Migration at startup | Probe kills pod during long migrate |
| No DB check | API serves 500s while marked healthy |

## Goal

1. Load balancer uses `GET /health` for readiness (includes database).
2. Optional separate liveness endpoint if platform requires it.
3. Runbook documents probe paths and expected status codes.

## Implementation tasks

### 1. Probe mapping

| Probe | URL | Pass |
|-------|-----|------|
| Readiness | `/health` | 200 + Healthy |
| Liveness | `/api/health` or `/health/live` | 200 |

Consider adding tagged checks if splitting live vs ready.

### 2. Response format

Optionally map health to JSON for Azure/k8s:

```csharp
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
```

Requires package `AspNetCore.HealthChecks.UI.Client` if desired.

### 3. Startup timing

If migrations run at startup (step 5), set:

- `initialDelaySeconds` on readiness probe > worst-case migration time
- Or run migrations outside app startup before probes start

### 4. Document in deploy runbook (step 7)

## Verification

1. Stop SQL Server → `/health` returns Unhealthy / 503.
2. Running API → `/health` returns 200.
3. Platform removes unhealthy instance from rotation.

## Production-grade notes

- Do not expose detailed health JSON publicly without auth if it reveals infrastructure details; restrict at edge if needed.
