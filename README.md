# ApplyVault

ApplyVault is a job-capture workspace built from three connected parts:

- a Chrome Manifest V3 extension that scrapes job listing details from the active tab
- a local ASP.NET Core API backed by EF Core and SQL Server LocalDB
- an Angular dashboard for reviewing saved jobs, fixing weak captures, and managing interview follow-up

## Current Capabilities

- Scrape raw page text plus structured job details such as title, company, location, description, summary, and hiring-manager contacts.
- Review and edit extracted fields in the extension popup before saving them to the API.
- Score capture quality for key fields and flag low-confidence results for follow-up review.
- Run optional Google AI enrichment to repair low-confidence fields before the result is stored.
- Persist original values, effective values, user overrides, overall confidence, and review status for each saved result.
- Sign in to the extension and dashboard with Supabase-backed authentication; scrape ingest and dashboard API calls use the same Bearer JWT.
- Store saved results in LocalDB through EF Core migrations applied automatically at API startup.
- Browse saved jobs in the Angular dashboard and inspect a dedicated detail panel.
- Mark saved results as rejected, review key capture fields, and clean up Markdown descriptions from the dashboard.
- Save interview timing on a job and create linked calendar events for connected Google or Microsoft accounts.
- Connect a Gmail mailbox from settings and poll for job-related emails with a hosted background sync worker.
- Auto-apply Gmail-detected rejection and interview updates to matching saved jobs, including interview calendar follow-up when calendar providers are already connected.
- Show whether the latest rejection or interview update came from Gmail sync or a manual dashboard action.
- Render saved job descriptions as Markdown in the dashboard detail view.
- Search public EURES job listings from the dashboard with multi-keyword filters, keyword expansion for common IT terms (for example `.net`), country pickers, and shareable URL state.
- Rank and paginate EURES results on the API with a short-lived in-memory cache so paging and load-more requests reuse the same ranked result set.
- Browse EURES listings with dedicated card and detail components, load-more and scroll-based fetching, keyboard list navigation, and saved-listing indicators.
- Inspect EURES listing details in the dashboard, save listings into ApplyVault, and open the original posting in a new tab.
- Filter saved jobs by debounced search term, source hostname, and workflow state (needs review, interview, rejected, hide rejected), with sort options for saved date, title, company, and interview date.
- Show a live filter summary and workspace stats that reflect the full saved dataset, not just the filtered subset.
- Share one authenticated app shell across Jobs, EURES, and Settings with active nav highlighting and sign-out.
- Render untrusted HTML safely in job and EURES detail views via centralized sanitization.
- Confirm destructive actions in modal dialogs before deleting saved jobs or disconnecting calendar and mail providers.
- Schedule and edit interview events in a modal dialog from the job detail panel.
- Show loading skeletons and section status chips while lists, detail panels, and settings integrations load.

## Repository Layout

- `src/`
  Chrome extension source, including popup UI, background service worker, content scripts, and shared contracts.
- `api/ApplyVault.Api/`
  ASP.NET Core API that stores and serves captured job results. Startup wiring lives in `Program.cs`; cross-cutting registration is in `Infrastructure/` (`ServiceCollectionExtensions`, `WebApplicationExtensions`, Supabase JWT auth) and database setup in `Data/ApplyVaultDatabaseExtensions.cs`.
- `api/ApplyVault.Api.Tests/`
  Fast unit tests for mail sync, Gmail client behavior, job-status classification, EURES job search, and related API services.
- `api/ApplyVault.Api.IntegrationTests/`
  HTTP integration tests (`WebApplicationFactory`) for auth and tenancy; separate project so unit tests stay fast.
- `frontend/applyvault-jobs-ui/`
  Angular application for reviewing saved results, searching EURES listings, and managing integrations. Feature areas live under `src/app/features/` (for example `job-results`, `eures-jobs`, `settings`) with presentation components in each feature’s `presentation/` folder.
- `plans/production-readiness/`
  Step-by-step plans for production hardening steps 4–17 (config, deploy, security, scale). Steps 1–3 live in `plans/prod-0N-*.md`.
- `plans/`
  Product roadmap and production-hardening tracker. Start with [`production-readiness-tracker.md`](plans/production-readiness-tracker.md) for the ordered checklist (`prod-01` … `prod-17`).
- `md/`
  Project notes and reusable prompt or style guidance documents.

## Extension Architecture

- `src/popup`
  UI for triggering a scrape, reviewing extracted fields, editing the payload, and saving it.
- `src/background`
  Service worker that coordinates scrape and save flows.
- `src/content`
  Content script plus DOM extraction logic.
- `src/content/jobDetailsExtraction`
  Modular extraction pipeline for descriptions, contacts, metadata, JSON-LD, page-type detection, and shared helpers.
- `src/application`
  Use-case orchestration layer.
- `src/infrastructure`
  Chrome API gateway and ASP.NET API adapter.
- `src/shared`
  Shared contracts, models, and utilities.

## Getting Started

### 1. Build the Chrome extension

```bash
npm install
npm run build
```

The unpacked extension output is generated in `dist/`. For staging or production API targets, see [`plans/production-readiness/EXTENSION.md`](plans/production-readiness/EXTENSION.md) (`npm run build:staging`, `npm run build:production`).

### 2. Run the ASP.NET API

```bash
dotnet run --project api/ApplyVault.Api --launch-profile http
```

The API listens on `http://localhost:5173/api` and exposes:

- `GET /health` — readiness probe (JSON; includes EF Core database check; returns 503 when unhealthy)
- `GET /health/live` — liveness probe (JSON; process-only, no database check)
- `GET /api/health` — legacy liveness alias (`{ "status": "ok" }`)
- `GET /api/auth/session` — resolves the signed-in Supabase user to a local `AppUser` record
- `GET /api/scrape-results`
- `GET /api/scrape-results/{id}`
- `POST /api/scrape-results`
- `PATCH /api/scrape-results/{id}/rejection`
- `PATCH /api/scrape-results/{id}/description`
- `PATCH /api/scrape-results/{id}/capture-review`
- `PUT /api/scrape-results/{id}/interview-event`
- `DELETE /api/scrape-results/{id}/interview-event`
- `POST /api/scrape-results/{id}/calendar-events`
- `GET /api/mail-connections`
- `POST /api/mail-connections/gmail/start`
- `GET /api/mail-connections/gmail/callback`
- `DELETE /api/mail-connections/{id}`
- `POST /api/eures/jobs/search`
- `GET /api/eures/jobs/{id}`
- `POST /api/eures/jobs/{id}/save`

Authenticated endpoints require a Supabase JWT (`Authorization: Bearer <access_token>`), including extension ingest (`POST /api/scrape-results`), saved-job CRUD, EURES search/save, and mail/calendar connection management. Unauthenticated requests receive **401 Unauthorized**.

`POST /api/scrape-results` requires authentication (production step 1). OAuth provider callbacks (`GET .../gmail/callback`, Google/Microsoft calendar callbacks) stay unauthenticated so the provider can complete the redirect.

Extension saves must be signed in; the popup sends the access token from [`aspNetApiClient.ts`](src/infrastructure/api/aspNetApiClient.ts). The Angular dashboard attaches the same Supabase access token through [`auth.interceptor.ts`](frontend/applyvault-jobs-ui/src/app/core/auth/auth.interceptor.ts) and loads the local app user from `GET /api/auth/session`.

#### Supabase JWT validation on the API

The API validates Supabase access tokens as Bearer JWTs:

- issuer: `{Supabase:Url}/auth/v1`
- audience: `authenticated` (override with `Supabase:Audience`)
- signing keys: fetched from Supabase JWKS (`/auth/v1/.well-known/jwks.json`) because current Supabase tokens use **ES256**
- user id: read from the JWT `sub` claim and mapped to a local `AppUser` on first request

Relevant files:

- `Infrastructure/ConfigureSupabaseJwtBearerOptions.cs`
- `Infrastructure/SupabaseJwtSigningKeyProvider.cs`
- `Infrastructure/SupabaseClaimTypes.cs`

JWT failures are logged under the `ApplyVault.Auth.JwtBearer` category. Restart the API after auth config changes.

**Frontend + API must use the same Supabase project.** The dashboard reads `frontend/applyvault-jobs-ui/src/app/core/config/supabase.config.ts`; the API reads `Supabase:Url` from layered configuration (see §3 below).

Saved results include the raw scrape payload, structured job details, persisted `isRejected` state, optional interview metadata, linked calendar events, capture-quality metadata for reviewable fields, and status-sync metadata that records whether the latest rejection or interview update came from Gmail or a manual edit.

**Local SQL:** copy `appsettings.Development.example.json` to `appsettings.Development.json` — it sets `ConnectionStrings:ApplyVault` (LocalDB by default). Base `appsettings.json` leaves the connection string empty so production cannot accidentally use dev SQL.

**Migrations:** when `Database:MigrateAtStartup` is `true` (default for local dev), startup runs `Database.Migrate()`. Production/Staging templates set it to `false`; run `dotnet ef database update` in deploy before traffic. See [`plans/production-readiness/DATABASE.md`](plans/production-readiness/DATABASE.md).

Integration tests set `Testing:UseInMemoryDatabase` to `true`, which uses `EnsureCreated()` and does not require a real connection string.

### 3. Configure API integrations

Configuration is layered: `appsettings.json` (safe defaults) → `appsettings.{Environment}.json` → environment variables → user secrets (Development). Nested keys use the `__` convention in env vars (`Supabase:Url` → `Supabase__Url`). Full key reference: [`plans/production-readiness/ENV.md`](plans/production-readiness/ENV.md).

**Local development:** copy `api/ApplyVault.Api/appsettings.Development.example.json` to `appsettings.Development.json`, set `Supabase:Url`, and store API keys/OAuth secrets with `dotnet user-secrets` from `api/ApplyVault.Api`.

**Production / Staging:** set `ASPNETCORE_ENVIRONMENT` accordingly; startup requires `Supabase:Url` and at least one `Cors:AllowedOrigins` entry (via env vars or host secrets). Templates: `appsettings.example.json`, `appsettings.Production.json`.

Option sections (validated at startup when enabled):

- `GoogleAi`
  Optional AI repair for low-confidence captures. Provide an API key and model when you want enrichment enabled.
- `ScrapeResultEnrichment`
  Turns the low-confidence enrichment pass on or off and controls whether AI failures should block saving.
- `Supabase`
  Configures JWT validation for authenticated dashboard and extension requests. Set `Url` to the project root (for example `https://your-project.supabase.co`), not `/auth/v1`. The API derives issuer and JWKS URLs from that value.
- `CalendarIntegration`
  Configures the OAuth client details and redirect URLs used to connect Google and Microsoft calendar providers.
- `MailIntegration`
  Enables Gmail sync, configures the Gmail OAuth client and callback URL, sets the Angular post-connect redirect, and controls poll cadence plus the initial mailbox lookback window. The Gmail background sync worker is registered only when `MailIntegration:Enabled` is `true`.
- `EuresIntegration`
  Configures the EURES API base URL, default country/location code, max results per page, and request timeout used by the job search endpoints. Search requests accept `page` and `resultsPerPage`; ranked results for a keyword/location/session are cached in memory for five minutes before server-side pagination is applied.
- `Cors`
  Allowed browser origins for production. Leave `AllowedOrigins` empty in Development to allow any origin; set explicit origins (for example `http://localhost:4200`) before exposing the API publicly.
- `Database`
  `MigrateAtStartup` controls whether EF migrations run on API startup. Production defaults to `false`; use `dotnet ef database update` in deploy (see [DATABASE.md](plans/production-readiness/DATABASE.md)).
- `Testing`
  Test-only switches. Integration tests set `Testing:UseInMemoryDatabase` and `Testing:InMemoryDatabaseName` to run against an isolated in-memory database.

Do not commit `appsettings.Development.json` or other files containing real secrets; use user secrets or `appsettings.*.local.json` (also gitignored).

### 4. Run the Angular dashboard

```bash
cd frontend/applyvault-jobs-ui
npm install
npm start
```

The dashboard runs on `http://localhost:4200/` and reads saved results from `http://localhost:5173/api`.

Authenticated dashboard routes (`/jobs`, `/eures`, `/settings`) render inside a shared app shell with primary navigation, the signed-in user, and sign-out. Login and OAuth callback routes stay outside the shell.

The saved jobs page (`/jobs`) supports:

- debounced live search and source filtering across title, company, location, and page type
- workflow filters: all, needs review, interview, rejected, and hide rejected
- sorting by saved date, title, company, or interview date
- a live filter summary (for example “Showing 3 of 12 jobs”) alongside workspace stats for total results, companies, sources, and rejected jobs (stats reflect the full saved dataset, not the filtered subset)
- distinct empty states for “no saved jobs” vs “no matches for current filters”
- deep-link selection via `?selected=<job-id>` (used after saving from EURES)
- capture quality review with field-level confidence and review reasons
- sanitized Markdown description cleanup and rendering
- interview event editing in a modal dialog and calendar-event creation for connected providers
- modal delete confirmation before removing a saved job
- loading skeletons and post-load status banners for the list and detail panels
- status-source messaging that explains whether the latest interview or rejection change was synced from Gmail or saved manually
- optimistic in-place updates after calendar sync and other mutations (no full list reload)

The settings page also supports:

- connecting and disconnecting Google, Microsoft, and Gmail integrations through the API
- section status chips (loading, connected count, sync health) and loading skeletons while connection data loads
- mailbox sync status, last sync time, and the most recent sync error for connected Gmail accounts
- modal confirmation before disconnecting a calendar or mail provider

The EURES job search page (`/eures`) supports:

- multi-keyword search with removable keyword chips and popular IT suggestion groups
- country picker with common ISO codes (Denmark, Sweden, Germany, and others)
- shareable URL state for keywords, location, and selected listing (for example `/eures?keywords=software,angular&location=dk&selected=<listing-id>`)
- server-ranked results with **Load more** and automatic fetch when the list or page scroll nears the bottom; retry on load-more errors
- dedicated `eures-job-card` and `eures-job-detail` presentation components for list selection, saved-state badges, and detail actions
- re-search when keywords or country change after an initial search
- formatted publication dates, mobile Results/Detail tabs, keyboard arrow navigation in the list, and focus management after search
- sanitized HTML rendering for listing descriptions
- **Save to ApplyVault** on the detail panel, with retry on error and a link to the saved job on `/jobs`
- loading skeletons during search, detail fetch, and incremental load-more

Unknown routes render a 404 page with a link back to `/jobs` when signed in or `/login` when signed out.

### 5. Run backend tests

Unit tests (fast, no web host):

```bash
dotnet test api/ApplyVault.Api.Tests/ApplyVault.Api.Tests.csproj
```

Integration tests (HTTP + auth pipeline):

```bash
dotnet test api/ApplyVault.Api.IntegrationTests/ApplyVault.Api.IntegrationTests.csproj
```

Unit tests cover the Gmail mail client, mail sync processor, email classification rules, the email-driven job/interview update services, and the EURES job search client, keyword expander, ranked-result caching, mapper, relevance scoring, and request normalization. Integration tests cover scrape-result auth and per-user tenancy over HTTP.

## Load The Extension In Chrome

1. Open `chrome://extensions`.
2. Enable Developer mode.
3. Click Load unpacked.
4. Select the root `dist` folder from this project.

## Supabase Email-Code Setup

The extension sign-in flow expects a typed numeric email OTP, not a clicked magic link.

In your Supabase project's `Magic Link` email template, include `{{ .Token }}` in the email body so users receive the code that `verifyOtp({ email, token, type: 'email' })` expects. If the template only uses `{{ .ConfirmationURL }}`, Supabase will send a magic link instead of the code required by the extension.

The dashboard uses email/password sign-in through Supabase Auth. After sign-in, it calls `GET /api/auth/session` to create or load the local app user record.

## Troubleshooting dashboard/API auth

Symptoms in the browser Network tab:

| Status / message | Meaning | What to check |
|------------------|---------|---------------|
| `401` + CORS response header present | Auth failed, not CORS | Token missing, expired, or rejected by API JWT validation |
| Console says `blocked by CORS policy` | CORS misconfiguration | API environment, `Cors:AllowedOrigins`, include `http://localhost:4200` outside Development |
| `invalid_token` / invalid issuer | Supabase URL mismatch | `Supabase:Url` in API config must match frontend `supabase.config.ts` |
| `signature key was not found` | JWKS validation issue | API must reach `{Supabase:Url}/auth/v1/.well-known/jwks.json`; restart API after auth code changes |
| `401` on `/api/auth/session` after sign-in | Token valid but user not resolved | Check API logs for `ApplyVault.Auth.JwtBearer` and `AppUserService` claim warnings |

Useful checks:

1. Confirm the request sends `Authorization: Bearer eyJ...`.
2. Confirm the API runs with `ASPNETCORE_ENVIRONMENT=Development` for local CORS defaults.
3. Restart the API after changing JWT/auth code or Supabase settings.
4. Sign out and sign in again if the Supabase session is stale.

## Typical Flow

1. Start the API.
2. Build and load the extension in Chrome.
3. Sign in to ApplyVault from the extension (Supabase email OTP).
4. Open a supported job listing and click `Scrape current page`.
5. Review the scraped text and structured fields in the popup, make any needed edits, and click `Save`.
6. Let the API score capture quality and optionally enrich weak fields before persisting the result.
7. Start the Angular dashboard, sign in with the same Supabase account, and review saved results in the browser.
8. Open a saved result to inspect capture confidence, review low-confidence fields, clean up the description, or mark it as rejected.
9. Optionally connect Gmail from dashboard settings after enabling `MailIntegration` and configuring Gmail OAuth credentials.
10. Let the background mail sync poll for new Gmail messages and auto-update matched jobs when interview or rejection emails arrive.
11. If the role progresses, save an interview time manually or let Gmail sync detect it, then push it to a connected calendar provider.
12. Optionally open the EURES job search page, run a keyword search for a country, load additional pages of results, save a listing to ApplyVault, and inspect listing details before opening the source posting.

## Manual Verification

1. Open a supported job page in Chrome such as LinkedIn, Workday, Greenhouse, or Lever.
2. Sign in from the extension and confirm save is blocked with a clear message when signed out.
3. Use the extension to scrape the current page.
4. Confirm the popup fills in structured fields such as job title, company, location, description, summary, and contacts.
5. Edit one or more popup fields, save the result to the API, and confirm the request succeeds.
6. Open the dashboard, sign in, and confirm `GET /api/auth/session` returns **200** with your user id/email.
7. Verify the new job appears in the list for the signed-in user only.
8. Check that the detail view shows capture confidence, review state, and field-level review guidance.
9. If the result is flagged for review, update the job title, company, or location and verify the reviewed state persists after refresh.
10. Edit the saved description in the dashboard and verify the rendered Markdown updates.
11. Save an interview event, refresh, and verify the interview timing persists.
12. If a calendar provider is connected, create a calendar event from the saved interview and verify the provider link is returned.
13. Toggle the rejected state and verify the change persists after refresh.
14. If `MailIntegration` is enabled, connect Gmail from settings and verify the callback returns to the dashboard with a success state.
15. Confirm the settings page shows mailbox sync status, last synced time, and any sync error details for the connected Gmail account.
16. Send or surface a recent Gmail rejection/interview email for a saved job, wait for the poll interval, and verify the job detail shows Gmail as the latest status source.
17. If Gmail sync detects interview details and a calendar provider is already connected, verify the linked interview can still be pushed to the provider from the dashboard flow.
18. Open a restricted page like `chrome://extensions` and confirm the extension reports a graceful error.
19. On `/jobs`, filter by search term, source, and workflow (for example **Needs review**); sort by title or interview date and confirm the filter summary updates while stats stay based on the full saved dataset.
20. Clear filters and confirm the full list returns; open interview editing and confirm the modal dialog saves and dismisses correctly.
21. Open `/eures`, run a keyword search with a country from the picker, and verify results load in the card list with a selectable detail panel.
22. Click **Load more** (or scroll near the bottom) and confirm additional listings append without losing the current selection.
23. Refresh `/eures?keywords=software&location=dk` and confirm keywords, location, and selection restore from the URL.
24. Select a listing, confirm sanitized description content renders, and verify the external listing link opens the source posting.
25. Click **Save to ApplyVault**, confirm success (or graceful duplicate handling), and follow the link to `/jobs?selected=...`.
26. Delete a saved job and confirm the modal confirmation step is required before removal.
27. On `/settings`, connect or disconnect an integration and confirm the disconnect confirmation modal appears before removal.
28. Visit an unknown path such as `/does-not-exist` and confirm the 404 page links to the correct home route for your auth state.
29. Sign out from any authenticated page and confirm you return to `/login`.

## Production readiness

ApplyVault is developed for local use first; production hardening is tracked explicitly so staging and multi-user hosting can be done in order.

**Tracker:** [`plans/production-readiness-tracker.md`](plans/production-readiness-tracker.md) — 17 steps in implementation order. Do not skip steps 1–2 before exposing the API to multiple users.

| Step | Status | Plan |
|------|--------|------|
| 1 Scrape ingest authentication | Done | [`prod-01-scrape-ingest-auth.md`](plans/prod-01-scrape-ingest-auth.md) |
| 2 Multi-tenant data isolation | Done | [`prod-02-tenancy-isolation.md`](plans/prod-02-tenancy-isolation.md) |
| 3 API integration tests (tenancy) | Done | [`prod-03-api-integration-tests.md`](plans/prod-03-api-integration-tests.md) |
| 4 API environment configuration | Done | [`production-readiness/prod-04-api-environment-configuration.md`](plans/production-readiness/prod-04-api-environment-configuration.md) |
| 5 Database and migrations | Done | [`production-readiness/prod-05-database-and-migrations.md`](plans/production-readiness/prod-05-database-and-migrations.md) · [`DATABASE.md`](plans/production-readiness/DATABASE.md) |
| 6 CI pipeline | Done | [`production-readiness/prod-06-ci-pipeline.md`](plans/production-readiness/prod-06-ci-pipeline.md) |
| 7 Deployment and hosting | Done | [`production-readiness/prod-07-deployment-and-hosting.md`](plans/production-readiness/prod-07-deployment-and-hosting.md) |
| 8 Frontend environment builds | Done | [`production-readiness/FRONTEND.md`](plans/production-readiness/FRONTEND.md) |
| 9 Extension production config | Done | [`production-readiness/EXTENSION.md`](plans/production-readiness/EXTENSION.md) |
| 10 OAuth redirects and secrets | Done | [`production-readiness/OAUTH.md`](plans/production-readiness/OAUTH.md) |
| 11 CORS and transport security | Done | [`deploy/RUNBOOK.md`](deploy/RUNBOOK.md) |
| 12 Health checks and readiness | Done | [`deploy/RUNBOOK.md`](deploy/RUNBOOK.md#health-and-readiness-probes) |
| 13–17 Logging, rate limit, tests, scale | Pending | [`production-readiness/README.md`](plans/production-readiness/README.md) (next: step 13) |

**Completed (steps 1–3):** Authenticated scrape ingest; per-user data isolation in store and DB; HTTP integration tests (`ScrapeResultsTenancyIntegrationTests`) prove 401/201/404 tenancy via `WebApplicationFactory` with test JWT auth and in-memory DB.

**Local foundations in place:** Config-driven CORS with HTTPS origin validation, HSTS at the Caddy edge, tagged readiness/liveness probes at `/health` and `/health/live`.

**Not production-ready yet for multi-instance:** EURES cache and Gmail sync horizontal-scale fixes (steps 16–17) when running more than one API replica.

**After pulling step 2:** restart the API so the new migration runs (`Database.Migrate()` at startup). Orphan `UserId IS NULL` rows are soft-deleted then deleted before the column becomes required.

## Notes

- The Angular UI style prompt lives in `md/angular-ui-style-guidance.md`.
- The frontend app keeps its own local `README.md` for Angular CLI-specific commands.
