# ApplyVault dashboard — frontend builds

Implements [prod-08-frontend-environment-builds.md](prod-08-frontend-environment-builds.md).

## Environment files

| File | Used when |
|------|-----------|
| `frontend/applyvault-jobs-ui/src/environments/environment.ts` | `ng serve`, development build |
| `frontend/applyvault-jobs-ui/src/environments/environment.staging.ts` | `ng build --configuration staging` |
| `frontend/applyvault-jobs-ui/src/environments/environment.production.ts` | `ng build --configuration production` |

Before a hosted build, edit the staging or production file (or copy values from your deploy host):

- `apiBaseUrl` — `https://<API_DOMAIN>/api` (same host as [deploy/.env.example](../../deploy/.env.example) `API_DOMAIN`)
- `supabase.url` — must match API `Supabase__Url` (project root, not `/auth/v1`)
- `supabase.anonKey` — Supabase **anon** / publishable key (public in the bundle; never use service role)

The dashboard origin (e.g. `https://app.example.com`) must appear in API `Cors__AllowedOrigins__*` (step 11).

## Build commands

From `frontend/applyvault-jobs-ui`:

```bash
npm ci
npm run build:production   # dist/applyvault-jobs-ui/browser/
npm run build:staging      # optional pre-prod
```

Development (localhost API on 5173):

```bash
npm start
```

## Deploy static bundle

Serve the contents of `dist/applyvault-jobs-ui/browser/` over HTTPS (nginx, Azure Static Web Apps, S3+CloudFront, Caddy `file_server`, etc.). SPA fallback: route unknown paths to `index.html`.

## Verification

1. Network tab: API requests go to `https://<API_DOMAIN>/api`, not localhost.
2. Sign-in works; `GET /api/auth/session` returns 200 with a valid Supabase JWT.
3. Staging build hits the staging API without source edits beyond `environment.staging.ts`.
