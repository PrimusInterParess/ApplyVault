---
name: Step 14 — Rate Limiting
overview: Protect public and authenticated API endpoints from abuse using ASP.NET rate limiting middleware.
todos:
  - id: rate-limit-policy
    content: Add partition-based rate limiting (by IP or user id) for expensive endpoints
    status: completed
  - id: scrape-ingest-limit
    content: Stricter limits on POST /api/scrape-results and POST /api/eures/jobs/search
    status: completed
  - id: oauth-callback-limit
    content: Moderate limits on OAuth callback routes to reduce abuse
    status: completed
  - id: rate-limit-response
    content: Return 429 with Retry-After header
    status: completed
  - id: config-tuning
    content: Expose limits via options for prod tuning without redeploy
    status: completed
isProject: false
---

# Step 14 — Rate Limiting

**Tracker:** [production-readiness-tracker.md](../production-readiness-tracker.md) · **Prerequisites:** [prod-11-cors-and-transport-security.md](prod-11-cors-and-transport-security.md) · **Next:** [prod-15-frontend-critical-path-tests.md](prod-15-frontend-critical-path-tests.md)

## Problem

Authenticated endpoints are open to abuse:

- `POST /api/scrape-results` triggers optional Google AI enrichment (cost)
- `POST /api/eures/jobs/search` hits external EURES API and in-memory cache
- No per-user or per-IP throttling exists

## Risk

| Risk | Impact |
|------|--------|
| Scrape spam | AI cost + DB bloat |
| EURES search flood | IP ban or degraded UX |
| Credential stuffing on auth | Supabase/API load |

## Goal

Return **429 Too Many Requests** when limits exceeded; normal use unaffected.

## High-value endpoints to limit

| Endpoint | Suggested policy |
|----------|------------------|
| `POST /api/scrape-results` | Per-user fixed window (e.g. 30/min) |
| `POST /api/eures/jobs/search` | Per-user sliding window (e.g. 20/min) |
| OAuth callbacks | Per-IP fixed window (e.g. 10/min) |
| Global | Per-IP ceiling on all `/api/*` |

## Implementation tasks

### 1. Enable rate limiting (.NET 7+)

In [`Program.cs`](../../api/ApplyVault.Api/Program.cs) or `ServiceCollectionExtensions`:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("scrape-ingest", o =>
    {
        o.Window = TimeSpan.FromMinutes(1);
        o.PermitLimit = 30;
        o.QueueLimit = 0;
    });
});
```

### 2. Apply to controllers

```csharp
[EnableRateLimiting("scrape-ingest")]
public async Task<ActionResult> Create(...)
```

Or partition by authenticated user id from `HttpContext.User`.

### 3. Pipeline order

`app.UseRateLimiter()` after routing, before endpoints (see ASP.NET docs).

### 4. Configuration

Add `RateLimitingOptions` bound from config for production tuning.

## Verification

1. Burst requests exceed limit → 429.
2. Normal dashboard usage stays under limit.
3. Rate limit events logged at Warning.

## Production-grade notes

- Place reverse-proxy rate limiting (Cloudflare, API gateway) as additional layer if needed.
- Whitelist internal health probes from global IP limits.
