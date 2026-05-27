---
name: Step 11 — CORS and Transport Security
overview: Lock down cross-origin access and enforce HTTPS for production API and OAuth flows.
todos:
  - id: cors-production
    content: Set Cors:AllowedOrigins to production frontend URL(s) only
    status: pending
  - id: cors-no-wildcard-prod
    content: Ensure Production never falls back to AllowAnyOrigin
    status: pending
  - id: https-redirection
    content: Add UseHttpsRedirection or rely on platform HTTPS termination with documented approach
    status: pending
  - id: hsts
    content: Enable HSTS in production (UseHsts)
    status: pending
  - id: supabase-https-metadata
    content: Confirm RequireHttpsMetadata=true in Production for JWT (already env-aware)
    status: pending
  - id: verify-preflight
    content: Verify browser preflight from prod frontend to prod API succeeds
    status: pending
isProject: false
---

# Step 11 — CORS and Transport Security

**Tracker:** [production-readiness-tracker.md](../production-readiness-tracker.md) · **Prerequisites:** [prod-10-oauth-redirects-and-secrets.md](prod-10-oauth-redirects-and-secrets.md) · **Next:** [prod-12-health-checks-and-readiness.md](prod-12-health-checks-and-readiness.md)

## Problem

[`AddApplyVaultCors`](../../api/ApplyVault.Api/Infrastructure/ServiceCollectionExtensions.cs) currently:

- Uses explicit origins when `Cors:AllowedOrigins` is set
- Falls back to `AllowAnyOrigin()` in **Development** when origins list is empty
- Applies **no CORS policy** in non-Development when origins list is empty

Production must explicitly allow only trusted frontend origins and use HTTPS.

## Partial work already done

- [`CorsOptions`](../../api/ApplyVault.Api/Options/CorsOptions.cs) and config section exist
- README troubleshooting distinguishes CORS vs 401

## Risk

| Risk | Impact |
|------|--------|
| AllowAnyOrigin in prod | Any site can call API with user's browser token |
| Missing CORS headers | Dashboard broken in browser |
| HTTP API | Token and OAuth interception |

## Goal

1. Production API only accepts CORS from known frontend origin(s).
2. All user-facing traffic uses HTTPS.
3. JWT metadata fetched over HTTPS (`RequireHttpsMetadata` in Production).

## Implementation tasks

### 1. Production CORS config

```json
"Cors": {
  "AllowedOrigins": [ "https://app.your-domain.com" ]
}
```

Via env: `Cors__AllowedOrigins__0=https://app.your-domain.com`

### 2. Harden AddApplyVaultCors

In non-Development with empty origins, log warning at startup (or fail validation from step 4).

### 3. HTTPS

In [`WebApplicationExtensions`](../../api/ApplyVault.Api/Infrastructure/WebApplicationExtensions.cs):

```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}
```

Skip if platform terminates TLS and redirects at edge — document chosen approach.

### 4. Verify

- Dashboard on prod origin can call API.
- Random origin in browser console gets CORS error (not 401 with ACAO *).

## Verification

1. Production config has explicit `AllowedOrigins`.
2. `curl -I https://api.../health` shows HSTS header if enabled.
3. OAuth redirects use HTTPS (step 10).

## Production-grade notes

- Do not use `AllowAnyOrigin()` with credentialed requests; current API uses Bearer header (non-credential CORS), but explicit origins are still required for security.
