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

5. **Dashboard** — build and host the Angular app ([FRONTEND.md](../plans/production-readiness/FRONTEND.md)):

   ```bash
   cd frontend/applyvault-jobs-ui
   # Edit src/environments/environment.production.ts: apiBaseUrl, supabase.url, supabase.anonKey
   npm ci && npm run build:production
   # Serve dist/applyvault-jobs-ui/browser/ at https://app.example.com (must match Cors__AllowedOrigins__0)
   ```

   Sign in and confirm API calls target `https://${API_DOMAIN}/api`.

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
| `Cors__AllowedOrigins__0` | Frontend origin (HTTPS, no path — e.g. `https://app.example.com`) |
| `Database__MigrateAtStartup` | `false` (run `migrate.sh` instead) |

Full reference: [ENV.md](../plans/production-readiness/ENV.md). Template: [`.env.example`](.env.example).

## OAuth (calendar and Gmail)

When enabling calendar connect or Gmail sync, register HTTPS callback URLs in each provider console and set API env vars. See [OAUTH.md](../plans/production-readiness/OAUTH.md).

Quick checklist:

1. Register redirect URIs: `https://${API_DOMAIN}/api/calendar-connections/{google|microsoft}/callback` and (if mail enabled) `https://${API_DOMAIN}/api/mail-connections/gmail/callback`.
2. Set `CalendarIntegration__PostConnectRedirectUrl` and `MailIntegration__PostConnectRedirectUrl` to `https://${APP_DOMAIN}/integrations/.../callback`.
3. Store `ClientId` / `ClientSecret` only in `deploy/.env`, never in git.
4. Smoke-test connect from dashboard Settings after deploy.

## Logs and monitoring

Implements [prod-13-logging-and-monitoring.md](../plans/production-readiness/prod-13-logging-and-monitoring.md).

### Tail logs (default sink)

Production and Staging emit **JSON** console logs (one line per entry). Docker captures stdout:

```bash
cd deploy
docker compose logs -f api
docker compose logs -f caddy
```

Filter API errors:

```bash
docker compose logs api --since 1h | grep '"LogLevel":"Error"'
```

Optional overrides via env (double underscore):

| Env var | Example |
|---------|---------|
| `Logging__LogLevel__Default` | `Warning` |
| `Logging__LogLevel__ApplyVault.Auth.JwtBearer` | `Debug` |

### Production log levels

| Category | Level | Purpose |
|----------|-------|---------|
| Default | Information | Application events |
| `Microsoft.AspNetCore` | Warning | Suppress noisy framework logs |
| `ApplyVault.Auth.JwtBearer` | Information | JWT validation failures and challenges |
| `ApplyVault.Api.Infrastructure.SupabaseJwtSigningKeyProvider` | Information | JWKS load and key resolution |
| `ApplyVault.Api.Infrastructure.RequestLoggingMiddleware` | Warning / Error | HTTP 4xx / 5xx with `TraceId` |

Configured in [`appsettings.Production.json`](../api/ApplyVault.Api/appsettings.Production.json).

### Never log

The API must **not** write these to logs at any level:

- Full `Authorization` header or JWT strings
- OAuth `ClientSecret` values
- Gmail message bodies at Information (only account ids / scrape result ids in warnings)

### Diagnosing auth failures (401)

| Symptom | Log category | Likely cause |
|---------|--------------|--------------|
| `invalid_token` / invalid issuer | `ApplyVault.Auth.JwtBearer` | `Supabase__Url` mismatch vs token issuer |
| Signing key not found / JWKS | `ApplyVault.Auth.JwtBearer` or `SupabaseJwtSigningKeyProvider` | JWKS fetch blocked or wrong Supabase project |
| JWT validated but no user id claim | `ApplyVault.Auth.JwtBearer` | Token missing `sub` claim |
| Authenticated but no Supabase user id | `ApplyVault.Api.Services.AppUserService` | Claim mapping issue after successful JWT |
| `no Authorization header` | `ApplyVault.Auth.JwtBearer` | Dashboard not sending Bearer token |

Reproduce and confirm in logs:

```bash
# Unauthenticated — expect JwtBearer Warning, no token in log line
curl -i "https://${API_DOMAIN}/api/scrape-results"

# Bad token — expect JwtBearer Warning with Reason=...
curl -i "https://${API_DOMAIN}/api/scrape-results" -H "Authorization: Bearer invalid"
```

Correlate API errors with dashboard reports using `TraceId` from JSON logs or from 502 JSON responses (`traceId` field).

### Platform log sink (optional)

| Platform | Approach |
|----------|----------|
| **Docker VPS (default)** | `docker compose logs` or ship stdout to your host agent (Vector, Fluent Bit, etc.) |
| **Azure App Service** | Enable Application Insights or Diagnostic Settings → capture container stdout |
| **Kubernetes** | Cluster logging stack (e.g. Loki, CloudWatch) on pod stdout |
| **Seq / ELK** | Forward JSON lines from the `api` container |

No extra NuGet sink is required for the baseline; add Application Insights or OpenTelemetry when you adopt a managed APM product.

### Alert baseline

Configure at least these checks on production:

| Alert | Signal | Suggested threshold |
|-------|--------|---------------------|
| **API unhealthy** | `GET https://${API_DOMAIN}/health` | Non-200 or `status` ≠ Healthy for **2+ consecutive** checks (1 min interval) |
| **5xx spike** | JSON logs: `RequestLoggingMiddleware` + `"LogLevel":"Error"` | More than **5** in 5 minutes (tune to traffic) |
| **Auth misconfiguration** | `ApplyVault.Auth.JwtBearer` Warning burst | More than **20** in 5 minutes after a deploy (often bad `Supabase__Url` or CORS/token issue) |

**Manual health alert test:** break `ConnectionStrings__ApplyVault` temporarily, restart `api`, confirm `GET /health` returns **503** and your monitor fires; restore connection string and redeploy.

**Docker healthcheck:** `deploy/docker-compose.yml` already restarts unhealthy containers when `/health` fails inside the container network.

## HTTPS and transport security

**Approach:** Caddy terminates TLS on ports 80/443 and proxies HTTP to the `api` container. The API does **not** call `UseHttpsRedirection` — redirecting inside the app would target the wrong host/port behind the proxy.

- Caddy obtains and renews certificates when `API_DOMAIN` resolves to this host and ports 80/443 are reachable.
- `deploy/Caddyfile` sends `Strict-Transport-Security` on HTTPS responses.
- Set `Cors__AllowedOrigins__0` to the dashboard origin (`https://${APP_DOMAIN}`). Startup fails if origins are missing or not HTTPS outside Development.
- JWT signing keys are fetched with `RequireHttpsMetadata=true` in Staging/Production.

### CORS preflight check

```bash
# Allowed origin — expect Access-Control-Allow-Origin matching APP_DOMAIN
curl -i -X OPTIONS "https://${API_DOMAIN}/health" \
  -H "Origin: https://${APP_DOMAIN}" \
  -H "Access-Control-Request-Method: GET"

# Untrusted origin — must not echo Access-Control-Allow-Origin for evil.example.com
curl -i -X OPTIONS "https://${API_DOMAIN}/health" \
  -H "Origin: https://evil.example.com" \
  -H "Access-Control-Request-Method: GET"
```

## Health and readiness probes

Orchestrators and load balancers should use different endpoints for liveness vs readiness.

| Probe | URL | Pass | Fail |
|-------|-----|------|------|
| **Readiness** | `GET /health` | HTTP **200**, JSON `status: "Healthy"`, `database` entry healthy | HTTP **503** — remove instance from rotation |
| **Liveness** | `GET /health/live` or `GET /api/health` | HTTP **200** | HTTP non-200 — restart container (if platform uses liveness) |

Readiness includes an EF Core database check. Liveness only confirms the process responds (no database dependency).

**Response format:** `/health` and `/health/live` return JSON with `status`, `totalDuration`, and per-check `entries`.

**Startup timing:** Production uses Option B migrations (`Database__MigrateAtStartup=false`). Run `deploy/scripts/migrate.sh` before starting the API so readiness probes are not blocked by long migrations. If you enable `Database__MigrateAtStartup=true`, increase probe `initialDelaySeconds` / Docker `start_period` beyond worst-case migration time.

### Docker Compose

The `api` service defines a readiness healthcheck hitting `http://localhost:8080/health` with `start_period: 30s`. Rebuild the image after pulling changes (`docker compose build api`).

### Platform examples

**Kubernetes:**

```yaml
readinessProbe:
  httpGet:
    path: /health
    port: 8080
  initialDelaySeconds: 10
  periodSeconds: 30
livenessProbe:
  httpGet:
    path: /health/live
    port: 8080
  initialDelaySeconds: 5
  periodSeconds: 30
```

**Azure App Service:** Configure **Health check path** to `/health` (readiness). Use platform restart policies for failed instances.

### Manual verification

```bash
# Readiness — expect 200 and database healthy
curl -fsS "https://${API_DOMAIN}/health" | jq .

# Liveness — expect 200 even when only checking process
curl -fsS "https://${API_DOMAIN}/health/live" | jq .

# Legacy alias — simple { "status": "ok" }
curl -fsS "https://${API_DOMAIN}/api/health"
```

To confirm failure behavior, stop SQL Server or break the connection string temporarily; `GET /health` must return **503** while `GET /health/live` still returns **200**.

## Rate limiting

Production and staging enable ASP.NET rate limiting (`RateLimiting:Enabled=true`). Health probe paths (`/health`, `/health/live`, `/api/health`) are exempt from the global `/api/*` per-IP limit.

| Policy | Endpoint(s) | Default limit | Partition |
|--------|-------------|---------------|-----------|
| Global | All `/api/*` | 200 / minute | Client IP (`X-Forwarded-For` when behind Caddy) |
| `scrape-ingest` | `POST /api/scrape-results` | 30 / minute | Authenticated user (`sub`), else IP |
| `eures-search` | `POST /api/eures/jobs/search` | 20 / minute (sliding) | User or IP |
| `oauth-callback` | `GET /api/calendar-connections/*/callback`, `GET /api/mail-connections/*/callback` | 10 / minute | Client IP |

Tune without redeploy via environment variables, for example:

```bash
RateLimiting__ScrapeIngest__PermitLimit=30
RateLimiting__EuresSearch__PermitLimit=20
RateLimiting__OAuthCallback__PermitLimit=10
RateLimiting__GlobalApi__PermitLimit=200
```

Rejected requests return **429** with a `Retry-After` header (seconds). Logs use category `ApplyVault.RateLimiting` at **Warning**, including `TraceId`.

### Manual verification

Burst authenticated requests against a limited endpoint until **429** appears, then confirm normal traffic stays under limits:

```bash
# Example: repeated scrape ingest (requires valid Bearer token)
for i in $(seq 1 40); do
  curl -s -o /dev/null -w "%{http_code}\n" -X POST "https://${API_DOMAIN}/api/scrape-results" \
    -H "Authorization: Bearer ${TOKEN}" \
    -H "Content-Type: application/json" \
    -d '{"jobUrl":"https://example.com/jobs/1","title":"Test"}'
done
```

Expect `429` responses with `Retry-After` once the per-user scrape limit is exceeded. Check API logs for `Rate limit exceeded`.

## Verification checklist

- [ ] `curl -fsS "https://${API_DOMAIN}/health"` returns HTTP 200 with JSON `status: "Healthy"` and database check healthy.
- [ ] `curl -fsS "https://${API_DOMAIN}/health/live"` returns HTTP 200.
- [ ] `curl -I "https://${API_DOMAIN}/health"` includes `Strict-Transport-Security`.
- [ ] CORS preflight from `https://${APP_DOMAIN}` succeeds; untrusted origin does not get `Access-Control-Allow-Origin`.
- [ ] Dashboard sign-in against prod API loads jobs.
- [ ] Rollback procedure exercised once on staging.

## Related steps

- **Step 8:** Frontend environment builds — set `apiUrl` to `https://${API_DOMAIN}`.
- **Step 10:** OAuth redirect URIs and secrets — [OAUTH.md](../plans/production-readiness/OAUTH.md).
- **Step 11:** CORS hardening for production domains — done; see [HTTPS and transport security](#https-and-transport-security).
- **Step 12:** Health/readiness probes — see [Health and readiness probes](#health-and-readiness-probes).
- **Step 13:** Logging and monitoring — see [Logs and monitoring](#logs-and-monitoring).
- **Step 14:** Rate limiting — see [Rate limiting](#rate-limiting).
