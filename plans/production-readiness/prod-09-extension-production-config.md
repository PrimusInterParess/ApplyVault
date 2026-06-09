---
name: Step 9 — Extension Production Config
overview: Configure the Chrome extension for production API and Supabase URLs and document the release build and distribution process.
todos:
  - id: extension-env
    content: Add production config for API base URL and Supabase (build-time or manifest config)
    status: completed
  - id: aspnet-api-client
    content: Update aspNetApiClient.ts to use production API URL in release builds
    status: completed
  - id: supabase-auth
    content: Align extension Supabase project with API and dashboard (same as step 8)
    status: completed
  - id: build-release
    content: Document npm run build for store-ready dist/ output
    status: completed
  - id: manifest-review
    content: Review manifest permissions and host_permissions for production domains
    status: completed
  - id: store-checklist
    content: Chrome Web Store listing checklist (privacy, icons, screenshots) if publishing publicly
    status: completed
isProject: false
---

# Step 9 — Extension Production Config

**Tracker:** [production-readiness-tracker.md](../production-readiness-tracker.md) · **Prerequisites:** [prod-08-frontend-environment-builds.md](prod-08-frontend-environment-builds.md) · **Next:** [prod-10-oauth-redirects-and-secrets.md](prod-10-oauth-redirects-and-secrets.md)

## Problem

The Chrome extension targets local development:

- [`aspNetApiClient.ts`](../../extension/src/infrastructure/api/aspNetApiClient.ts) posts scrapes to localhost API
- Supabase auth in [`supabaseAuth.ts`](../../extension/src/infrastructure/auth/supabaseAuth.ts) must match production project

Unpacked `dist/` loading is fine for dev; production users need a build aimed at the hosted API.

## Risk

| Risk | Impact |
|------|--------|
| Extension saves to localhost | Data never reaches prod |
| Wrong Supabase project | Users cannot sign in |
| Over-broad host_permissions | Store rejection or security review failure |

## Goal

1. Release build points at production HTTPS API.
2. Same Supabase project as dashboard and API JWT validation.
3. Documented build + load/unpacked vs store publish flow.

## Affected code

| Path | Purpose |
|------|---------|
| [`extension/src/infrastructure/api/aspNetApiClient.ts`](../../extension/src/infrastructure/api/aspNetApiClient.ts) | Scrape ingest HTTP |
| [`extension/src/infrastructure/auth/supabaseAuth.ts`](../../extension/src/infrastructure/auth/supabaseAuth.ts) | Extension sign-in |
| [`extension/manifest.json`](../../extension/manifest.json) / build output | Permissions |

## Implementation tasks

### 1. Config pattern

Use build-time env (webpack/vite define) or a small `config.production.ts` swapped during `npm run build`:

```typescript
export const apiBaseUrl = 'https://api.your-domain.com/api';
```

### 2. Auth alignment

- Extension, dashboard, and API must share Supabase project URL.
- Email OTP template must include `{{ .Token }}` (see README).

### 3. Manifest permissions

- Restrict `host_permissions` to job boards you support + production API origin.
- Remove localhost permissions from store build or use separate dev manifest.

### 4. Verify scrape ingest

1. Sign in via extension against prod Supabase.
2. Scrape a job page.
3. Confirm `POST /api/scrape-results` returns 201 and job appears in prod dashboard for same user.

## Verification

- Release `dist/` loaded in Chrome saves to production API.
- Unsigned-out save shows clear error (step 1 behavior).
- Manifest version bumped for each release.

## Production-grade notes

- Chrome Web Store review requires privacy policy if handling user data.
- Consider separate **dev** vs **prod** extension IDs for internal testing.
