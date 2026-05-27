# ApplyVault API — deployment runbook

Implements [prod-07-deployment-and-hosting.md](../plans/production-readiness/prod-07-deployment-and-hosting.md).

## Hosting choice

**Default:** Docker Compose on a Linux VPS with **Caddy** for HTTPS (Let's Encrypt).

| Component | Role |
|-----------|------|
| `api` container | ASP.NET Core API on port 8080 (internal) |
| `caddy` container | Public HTTP/HTTPS, terminates TLS, proxies to `api` |
| Azure SQL / RDS / managed SQL | Database (not in Compose — connect over the network) |

**Alternatives:** Azure App Service (container or `dotnet publish` zip deploy), Railway, Fly.io. Use the same env vars from [ENV.md](../plans/production-readiness/ENV.md).

**Single replica:** Run one API instance until steps 16–17 (EURES cache, Gmail sync) are implemented.

## Prerequisites

1. Linux host with Docker and Docker Compose v2.
2. DNS `A`/`AAAA` record pointing `API_DOMAIN` at the host.
3. Production SQL Server and connection string.
4. Supabase project URL (same project as the Angular dashboard).
5. CI passing on `main` ([`.github/workflows/api-ci.yml`](../.github/workflows/api-ci.yml)).

## First-time setup

```bash
# On the server (clone repo or copy deploy/ + api/ApplyVault.Api/)
cd deploy
cp .env.example .env
# Edit .env: API_DOMAIN, ConnectionStrings__ApplyVault, Supabase__Url, Cors__AllowedOrigins__0
```

Open firewall ports **80** and **443** only. Do not expose SQL Server publicly.

## Publish (without Docker)

From the repo root:

```bash
dotnet publish api/ApplyVault.Api/ApplyVault.Api.csproj -c Release -o ./publish
```

Run `./publish/ApplyVault.Api.dll` on the host with production env vars set. Use nginx or Caddy on the host for HTTPS.

## Deploy sequence (Option B migrations — Production default)

1. **CI** — merge only after `api-ci` passes.
2. **Migrate** — from a machine with .NET SDK and network access to production SQL:

   ```bash
   bash deploy/scripts/migrate.sh
   ```

   Uses `ConnectionStrings__ApplyVault` from `deploy/.env`. See [DATABASE.md](../plans/production-readiness/DATABASE.md).

3. **Deploy** — on the server:

   ```bash
   cd deploy
   docker compose pull    # if using a registry image
   docker compose build   # or build on server from repo
   docker compose up -d
   ```

4. **Smoke test**:

   ```bash
   bash deploy/scripts/smoke-test.sh "https://${API_DOMAIN}"
   # Optional auth check (Supabase access token from dashboard devtools):
   bash deploy/scripts/smoke-test.sh "https://${API_DOMAIN}" "<jwt>"
   ```

5. **Dashboard** — point the frontend at `https://${API_DOMAIN}` (step 8) and sign in.

## Rollback

1. Keep the previous image tag or git commit SHA before deploy.
2. Roll back database only if a migration was applied and must be reversed (prefer forward-fix migrations; test rollback on staging first).
3. Redeploy previous bits:

   ```bash
   cd deploy
   git checkout <previous-commit>   # or pull previous image tag
   docker compose build
   docker compose up -d
   bash ../deploy/scripts/smoke-test.sh "https://${API_DOMAIN}"
   ```

## Environment variables

Minimum production set:

| Env var | Purpose |
|---------|---------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__ApplyVault` | SQL Server |
| `Supabase__Url` | Supabase project URL |
| `Cors__AllowedOrigins__0` | Frontend origin (HTTPS) |
| `Database__MigrateAtStartup` | `false` (run `migrate.sh` instead) |

Full reference: [ENV.md](../plans/production-readiness/ENV.md). Template: [`.env.example`](.env.example).

## Logs

```bash
cd deploy
docker compose logs -f api
docker compose logs -f caddy
```

## HTTPS

Caddy obtains and renews certificates automatically when `API_DOMAIN` resolves to this host and ports 80/443 are reachable.

## Verification checklist

- [ ] `curl -fsS "https://${API_DOMAIN}/health"` returns HTTP 200 with database healthy.
- [ ] Dashboard sign-in against prod API loads jobs.
- [ ] Rollback procedure exercised once on staging.

## Related steps

- **Step 8:** Frontend environment builds — set `apiUrl` to `https://${API_DOMAIN}`.
- **Step 10–11:** OAuth redirect URIs and CORS hardening for production domains.
- **Step 12:** Extended health/readiness probes (builds on `GET /health`).
