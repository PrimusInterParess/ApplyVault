---
name: Step 8 — Frontend Environment Builds
overview: Add Angular environment configurations for staging and production API/Supabase URLs and document the production build process.
todos:
  - id: environment-files
    content: Add environment.staging.ts and environment.production.ts with apiBaseUrl and Supabase config
    status: completed
  - id: angular-config
    content: Wire file replacements in angular.json for production and staging configurations
    status: completed
  - id: remove-hardcoded-config
    content: Ensure api.config.ts and supabase.config.ts read from environment files at build time
    status: completed
  - id: production-build
    content: Document ng build --configuration production and artifact output path
    status: completed
  - id: deploy-frontend
    content: Deploy static bundle to host (Azure Static Web Apps, S3+CDN, nginx, etc.)
    status: completed
isProject: false
---

# Step 8 — Frontend Environment Builds

**Tracker:** [production-readiness-tracker.md](../production-readiness-tracker.md) · **Prerequisites:** [prod-07-deployment-and-hosting.md](prod-07-deployment-and-hosting.md) · **Next:** [prod-09-extension-production-config.md](prod-09-extension-production-config.md)

## Problem

The Angular app hardcodes local URLs in:

- [`api.config.ts`](../../frontend/applyvault-jobs-ui/src/app/core/config/api.config.ts) → `http://localhost:5173/api`
- [`supabase.config.ts`](../../frontend/applyvault-jobs-ui/src/app/core/config/supabase.config.ts) → project URL and anon key

Production dashboard must call the **deployed API** and the **same Supabase project** as configured on the API.

## Risk

| Risk | Impact |
|------|--------|
| Prod build still points to localhost | Dashboard broken for all users |
| Supabase anon key in wrong env | Auth failures |
| CORS mismatch | Browser blocks API calls |

## Goal

1. `ng build --configuration production` bakes in production API URL and Supabase settings.
2. Staging configuration for pre-prod validation.
3. Deploy static files to HTTPS host matching `Cors:AllowedOrigins` on API (step 11).

## Affected code

| File | Change |
|------|--------|
| `frontend/applyvault-jobs-ui/src/environments/` | New environment TS files |
| [`app.config.ts`](../../frontend/applyvault-jobs-ui/src/app/app.config.ts) | Inject from environment |
| [`angular.json`](../../frontend/applyvault-jobs-ui/angular.json) | fileReplacements |

## Implementation tasks

### 1. Environment files

```typescript
// environment.production.ts
export const environment = {
  production: true,
  apiBaseUrl: 'https://api.your-domain.com/api',
  supabase: { url: '...', anonKey: '...' }
};
```

### 2. Wire providers

Replace `defaultApiConfig` / `defaultSupabaseConfig` with values from `environment`.

### 3. Build commands

```bash
cd frontend/applyvault-jobs-ui
npm run build -- --configuration production
```

### 4. Align with API

- Frontend `supabase.url` must match API `Supabase:Url` (root URL, not `/auth/v1`).
- Frontend origin must appear in API `Cors:AllowedOrigins`.

### 5. CI (optional)

Add frontend build job to workflow from step 6.

## Verification

1. Production build calls deployed API (verify in Network tab).
2. Sign-in and `GET /api/auth/session` succeed against prod.
3. Staging build uses staging API URL without code changes.

## Production-grade notes

- Supabase **anon** key is public by design; still avoid mixing staging/prod projects.
- Never embed service role or API secrets in the Angular bundle.
