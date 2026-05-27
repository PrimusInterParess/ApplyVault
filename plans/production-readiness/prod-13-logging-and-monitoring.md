---
name: Step 13 — Logging and Monitoring
overview: Add structured production logging, auth failure observability, and basic monitoring hooks for the deployed API.
todos:
  - id: log-levels
    content: Set Production log levels (Information default, Warning for Microsoft.AspNetCore)
    status: pending
  - id: structured-logging
    content: Use structured logging templates consistently; avoid logging secrets or JWT bodies
    status: pending
  - id: auth-log-review
    content: Document ApplyVault.Auth.JwtBearer and AppUserService log categories for ops
    status: pending
  - id: request-logging
    content: Optional HTTP request logging middleware for 4xx/5xx with correlation id
    status: pending
  - id: monitoring-sink
    content: Connect logs to platform (App Insights, CloudWatch, seq, etc.) or document manual tail
    status: pending
  - id: alert-baseline
    content: Define alerts for health check failures and error rate spikes
    status: pending
isProject: false
---

# Step 13 — Logging and Monitoring

**Tracker:** [production-readiness-tracker.md](../production-readiness-tracker.md) · **Prerequisites:** [prod-07-deployment-and-hosting.md](prod-07-deployment-and-hosting.md) · **Next:** [prod-14-rate-limiting.md](prod-14-rate-limiting.md)

## Problem

Logging is console-only with default levels. Auth debugging added categories:

- `ApplyVault.Auth.JwtBearer` in [`ConfigureSupabaseJwtBearerOptions`](../../api/ApplyVault.Api/Infrastructure/ConfigureSupabaseJwtBearerOptions.cs)
- `AppUserService` claim warnings

Production needs aggregated logs and basic alerts without exposing tokens.

## Partial work already done

- JWT failure reasons logged at Warning
- JWKS load logged at Information

## Risk

| Risk | Impact |
|------|--------|
| No central logs | Slow incident response |
| Logging JWTs | Credential leak in log store |
| No alerts on /health failures | Silent outage |

## Goal

1. Production logs searchable by level, category, and trace id.
2. Runbook: how to diagnose 401 spikes using auth log categories.
3. At least one alert on unhealthy `/health`.

## Implementation tasks

### 1. appsettings.Production.json logging

```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning",
    "ApplyVault.Auth.JwtBearer": "Information"
  }
}
```

### 2. Never log

- Full `Authorization` header
- OAuth client secrets
- Gmail message bodies at Info level

### 3. Correlation

- Rely on ASP.NET `TraceIdentifier` in exception handler ([`EuresJobClientExceptionHandler`](../../api/ApplyVault.Api/Infrastructure/EuresJobClientExceptionHandler.cs) pattern).
- Optional: `app.UseMiddleware<RequestLogging>()` for 5xx only.

### 4. Platform sink

Configure host-specific logging provider (Application Insights, etc.) in deploy runbook.

### 5. Auth ops guide

Document in README or runbook:

| Symptom | Log category | Likely cause |
|---------|--------------|--------------|
| 401 invalid_token issuer | ApplyVault.Auth.JwtBearer | Supabase URL mismatch |
| 401 signature key | ApplyVault.Auth.JwtBearer | JWKS fetch failure |
| 401 after 200 JWT | AppUserService | Missing sub claim |

## Verification

1. Trigger intentional 401 → appears in log sink with reason.
2. Production logs exclude secrets.
3. Health failure triggers configured alert (manual test).

## Production-grade notes

- Can start light (console + platform capture) and expand later.
- Step 6 CI does not require monitoring sink.
