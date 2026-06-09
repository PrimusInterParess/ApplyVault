# ApplyVault

ApplyVault is a job-capture workspace built from three connected parts:

- a Chrome Manifest V3 extension that scrapes job listing details from the active tab
- a local ASP.NET Core API backed by EF Core and SQL Server LocalDB
- an Angular dashboard for reviewing saved jobs, searching public listings, editing structured CV content, and managing interview follow-up

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
- Connect a GitHub account from settings via OAuth (stored server-side; tokens are never exposed to the browser).
- Browse connected GitHub repositories from the dashboard and generate CV-ready personal-project summaries with Google AI from repo metadata and README content.
- Persist generated CV project summaries per user (title, summary, bullets, tech stack) with regenerate and delete; summaries are removed when GitHub is disconnected.
- Upload one PDF CV per user from the dashboard, store the file on local disk or Azure Blob Storage, and auto-extract structured sections on upload (Google AI with heuristic fallback) plus optional profile-photo extraction.
- Edit structured CV sections (Experience, Projects, Education, Skills, Summary, Custom) with inline editing, drag-and-drop reorder, re-import from the stored PDF, Google AI section updates, and reviewable improvement suggestions.
- Export a formatted PDF from structured CV content using selectable HTML templates (Classic, Modern, Minimal ATS, Creative, Professional).
- Preview the original uploaded PDF in the browser with replace and delete flows.
- Connect a Gmail mailbox from settings and poll for job-related emails with a hosted background sync worker.
- Auto-apply Gmail-detected rejection and interview updates to matching saved jobs, including interview calendar follow-up when calendar providers are already connected.
- Show whether the latest rejection or interview update came from Gmail sync or a manual dashboard action.
- Render saved job descriptions as Markdown in the dashboard detail view.
- Search public job listings from a unified **Search** page that supports **EURES** and **Work in Denmark** (Jobnet) with a source toggle, multi-keyword filters, keyword expansion for common IT terms (for example `.net`), country pickers, and shareable URL state (`/search?source=eures|jobnet&keywords=...`).
- Rank and paginate EURES and Jobnet results on the API with a short-lived distributed cache (Redis when `ConnectionStrings:Redis` is set; in-memory fallback for a single API replica) so paging and load-more requests reuse the same ranked result set.
- Browse external listings with shared card and detail components, load-more and scroll-based fetching, keyboard list navigation, and saved-listing indicators.
- Inspect EURES and Work in Denmark listing details in the dashboard, save listings into ApplyVault, and open the original posting in a new tab.
- Assess Work in Denmark listing descriptions for quality on the API: native Jobnet detail responses render as full HTML, while EURES-imported listings and noisy scraped text fall back to a short excerpt with a quality reason and a link to the source posting.
- Show external listing descriptions in a shared `job-description-panel` that switches between full sanitized HTML, preview-only excerpt mode, and an empty state.
- Filter saved jobs by debounced search term, source hostname, and workflow state (needs review, interview, rejected, hide rejected), with sort options for saved date, title, company, and interview date.
- Show a live filter summary and workspace stats that reflect the full saved dataset, not just the filtered subset.
- Share one authenticated app shell across Jobs, Search, My CV, Projects, and Settings with active nav highlighting and sign-out.
- Render untrusted HTML safely in job and external-listing detail views via centralized sanitization.
- Confirm destructive actions in modal dialogs before deleting saved jobs, removing an uploaded CV, or disconnecting calendar, GitHub, and mail providers.
- Schedule and edit interview events in a modal dialog from the job detail panel.
- Show loading skeletons and section status chips while lists, detail panels, and settings integrations load.

## Repository Layout

- `extension/`
  Chrome extension package (`package.json`, manifests, build scripts) and source under `extension/src/` — popup UI, background service worker, content scripts, and shared contracts.
- `api/ApplyVault.Api/`
  ASP.NET Core API that stores and serves captured job results. Startup wiring lives in `Program.cs`; cross-cutting registration is in `Infrastructure/` (`ServiceCollectionExtensions`, `DistributedInfrastructureExtensions`, `WebApplicationExtensions`, Supabase JWT auth) and database setup in `Data/ApplyVaultDatabaseExtensions.cs`. External job search lives in `Services/Eures/` and `Services/Jobnet/`. CV PDF and structured editing are handled in `Services/CvDocuments/` with pluggable local or Azure Blob storage and optional HTML-template PDF export.
- `api/ApplyVault.Api.Tests/`
  Fast unit tests for mail sync, Gmail client behavior, job-status classification, EURES and Jobnet job search (including description quality heuristics, search payload cache, detail fetcher, and detail composition), CV structured import/export, and related API services.
- `api/ApplyVault.Api.IntegrationTests/`
  HTTP integration tests (`WebApplicationFactory`) for auth and tenancy; separate project so unit tests stay fast.
- `frontend/applyvault-jobs-ui/`
  Angular application for reviewing saved results, searching EURES and Work in Denmark listings, editing structured CV content, generating CV project summaries from GitHub, and managing integrations. Feature areas live under `src/app/features/` (for example `job-results`, `job-search`, `cv-projects`, `settings`) with presentation components in each feature’s `presentation/` folder. Legacy `eures-jobs` code remains for reference but routes use `job-search`. Critical-path specs live alongside features and in `src/testing/` (auth mocks, API fixtures).
- `plans/production-readiness/`
  Step-by-step plans for production hardening steps 4–17 (config, deploy, security, scale). Steps 1–3 live in `plans/prod-0N-*.md`.
- `plans/`
  Product roadmap and production-hardening tracker. Start with [`production-readiness-tracker.md`](plans/production-readiness-tracker.md) for the ordered checklist (`prod-01` … `prod-17`). GitHub portfolio integration is tracked in [`github_integration_plan.md`](plans/github_integration_plan.md) (Phase 1 OAuth and live repo browsing with AI CV summaries are done; background repo sync and deeper curation are pending).
- `md/`
  Project notes and reusable prompt or style guidance documents.

## Extension Architecture

Paths below are under `extension/src/`:

- `popup/`
  UI for triggering a scrape, reviewing extracted fields, editing the payload, and saving it.
- `background/`
  Service worker that coordinates scrape and save flows.
- `content/`
  Content script plus DOM extraction logic.
- `content/jobDetailsExtraction`
  Modular extraction pipeline for descriptions, contacts, metadata, JSON-LD, page-type detection, and shared helpers.
- `application/`
  Use-case orchestration layer.
- `infrastructure/`
  Chrome API gateway and ASP.NET API adapter.
- `shared/`
  Shared contracts, models, and utilities.

## Getting Started

### 1. Build the Chrome extension

```bash
cd extension
npm install
npm run build
```

The unpacked extension output is generated in `extension/dist/`. For staging or production API targets, see [`plans/production-readiness/EXTENSION.md`](plans/production-readiness/EXTENSION.md) (`npm run build:staging`, `npm run build:production`).

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
- `GET /api/github-connections`
- `POST /api/github-connections/github/start`
- `GET /api/github-connections/github/callback`
- `DELETE /api/github-connections/{id}`
- `GET /api/github/repos` — list repositories for the connected GitHub account (`page`, `perPage`)
- `GET /api/cv-projects` — list saved CV project summaries for the signed-in user
- `GET /api/cv-projects/{id}`
- `POST /api/cv-projects/generate` — generate or regenerate a summary from a repo `fullName` (for example `owner/repo`)
- `DELETE /api/cv-projects/{id}`
- `GET /api/cv-documents/current` — metadata for the signed-in user’s uploaded CV PDF (404 when none)
- `POST /api/cv-documents/current` — upload or replace the current CV (PDF only, max 5 MB by default); returns document metadata and structured import summary
- `GET /api/cv-documents/current/content` — stream the current CV PDF for preview/download (alias of original content)
- `GET /api/cv-documents/current/content/original`
- `GET /api/cv-documents/current/content/original/download`
- `GET /api/cv-documents/current/profile-photo`
- `DELETE /api/cv-documents/current`
- `GET /api/cv-documents/current/structured`
- `PUT /api/cv-documents/current/structured`
- `POST /api/cv-documents/current/structured/reimport`
- `POST /api/cv-documents/current/structured/ai-update`
- `POST /api/cv-documents/current/structured/ai-suggestions`
- `GET /api/cv-documents/current/export/download?templateId=1&maxPages=2` — export formatted PDF from structured content; optional `maxPages` compacts layout to fit when possible and returns `X-Cv-Export-*` page-count/warning headers
- `GET /api/mail-connections`
- `POST /api/mail-connections/gmail/start`
- `GET /api/mail-connections/gmail/callback`
- `DELETE /api/mail-connections/{id}`
- `POST /api/eures/jobs/search`
- `GET /api/eures/jobs/{id}`
- `POST /api/eures/jobs/{id}/save`
- `POST /api/jobnet/jobs/search`
- `GET /api/jobnet/jobs/{id}` — includes `descriptionSource`, `descriptionQuality`, and optional `descriptionExcerpt` / `descriptionQualityReason` when heuristics flag scraped or low-quality text
- `POST /api/jobnet/jobs/{id}/save`

Authenticated endpoints require a Supabase JWT (`Authorization: Bearer <access_token>`), including extension ingest (`POST /api/scrape-results`), saved-job CRUD, EURES and Jobnet search/save, CV document upload/preview/structured editing/export, GitHub repo listing and CV project summary endpoints, and GitHub/mail/calendar connection management. Unauthenticated requests receive **401 Unauthorized**.

`POST /api/scrape-results` requires authentication (production step 1). OAuth provider callbacks (`GET .../github/callback`, `GET .../gmail/callback`, Google/Microsoft calendar callbacks) stay unauthenticated so the provider can complete the redirect.

Extension saves must be signed in; the popup sends the access token from [`aspNetApiClient.ts`](extension/src/infrastructure/api/aspNetApiClient.ts). The Angular dashboard attaches the same Supabase access token through [`auth.interceptor.ts`](frontend/applyvault-jobs-ui/src/app/core/auth/auth.interceptor.ts) and loads the local app user from `GET /api/auth/session`.

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
  Optional AI repair for low-confidence captures, CV structured import/update/suggestions, CV project summary generation, and CV export polish. Provide an API key and model when you want enrichment enabled. CV AI features require `GoogleAi:Enabled` to be `true`.
- `CvImportAi`, `CvUpdateAi`, `CvSuggestionsAi`, `CvExportAi`
  Prompt templates for CV structured import, AI section updates, improvement suggestions, and export copy polish. Defaults are suitable for most setups; override only when you want different tone or structure.
- `GitHubProjectAi`
  Prompt templates for CV project summary generation (`SystemPrompt`, `UserPromptTemplate`). Defaults are suitable for most setups; override only when you want different tone or structure.
- `ScrapeResultEnrichment`
  Turns the low-confidence enrichment pass on or off and controls whether AI failures should block saving.
- `Supabase`
  Configures JWT validation for authenticated dashboard and extension requests. Set `Url` to the project root (for example `https://your-project.supabase.co`), not `/auth/v1`. The API derives issuer and JWKS URLs from that value.
- `CalendarIntegration`
  Configures the OAuth client details and redirect URLs used to connect Google and Microsoft calendar providers.
- `GitHubIntegration`
  Enables GitHub OAuth connect/disconnect from settings and live repo listing for CV project generation. Set `Enabled` to `true`, register a GitHub OAuth App with callback `http://localhost:5173/api/github-connections/github/callback` (local), and provide `ClientId`, `ClientSecret`, `RedirectUri`, `PostConnectRedirectUrl` (`http://localhost:4200/integrations/github/callback`), and `Scopes` (default `read:user repo`). GitHub is a connected account, not app login; tokens are stored server-side only. Background repo sync into a local mirror is not implemented yet.
- `MailIntegration`
  Enables Gmail sync, configures the Gmail OAuth client and callback URL, sets the Angular post-connect redirect, and controls poll cadence plus the initial mailbox lookback window. The Gmail background sync worker is registered only when `MailIntegration:Enabled` is `true`.
- `EuresIntegration`
  Configures the EURES API base URL, default country/location code, max results per page, upstream scan limits, and request timeout used by the job search endpoints. Search requests accept `page` and `resultsPerPage`; ranked results for a keyword/location/session are cached for five minutes (Redis when configured) before server-side pagination is applied.
- `JobnetIntegration`
  Configures the Jobnet BFF base URL (`https://jobnet.dk/bff` by default), search/detail paths, Work in Denmark filtering, radius and sort defaults, upstream scan limits, classification detail-fetch limits (`MaxClassificationDetailFetches` verifies Work in Denmark during search for native GUID listings; `MaxDetailFetchConcurrency` caps parallel checks), search retry attempts, and ranked/classification cache TTLs used by Work in Denmark search endpoints. Search caches each ranked job’s upstream search payload by id (`JobnetSearchPayloadCache`, same TTL as `RankedCacheTtlMinutes`) so detail and save requests reuse search-time data instead of re-querying Jobnet. Detail responses include `descriptionSource` (`nativeDetail` or `searchFallback`), `descriptionQuality` (`full` or `previewOnly`), and optional excerpt/reason fields when heuristics detect scraped or navigation-heavy text.
- `CvDocumentStorage`
  Stores each user’s uploaded CV PDF. `Provider` is `Local` (default; files under `App_Data/cv-documents`, gitignored) or `AzureBlob` (set `AzureBlob:ConnectionString` and `ContainerName` for production). `MaxFileSizeBytes` defaults to 5 MB. Only one CV per user; a new upload replaces the previous file and storage object and triggers structured re-import.
- `CvHtmlExport`
  Controls HTML-template PDF export (`EnableHtmlTemplates`, `TemplatesSubfolder`, `MaxConcurrentExports`). Templates are numbered 1–5 (Classic through Professional).
- `ConnectionStrings:Redis`
  Optional shared Redis for EURES/Jobnet ranked-result caches and Gmail sync locking when running more than one API replica. Omit for single-replica local dev (in-memory cache and in-process lock fallback).
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

Authenticated dashboard routes (`/jobs`, `/search`, `/my-cv`, `/cv-projects`, `/settings`) render inside a shared app shell with primary navigation, the signed-in user, and sign-out. Legacy paths `/eures` and `/workindenmark` redirect to `/search` with the appropriate `source` query param. Login and OAuth callback routes stay outside the shell.

The saved jobs page (`/jobs`) supports:

- debounced live search and source filtering across title, company, location, and page type
- workflow filters: all, needs review, interview, rejected, and hide rejected
- sorting by saved date, title, company, or interview date
- a live filter summary (for example “Showing 3 of 12 jobs”) alongside workspace stats for total results, companies, sources, and rejected jobs (stats reflect the full saved dataset, not the filtered subset)
- distinct empty states for “no saved jobs” vs “no matches for current filters”
- deep-link selection via `?selected=<job-id>` (used after saving from Search)
- capture quality review with field-level confidence and review reasons
- sanitized Markdown description cleanup and rendering
- interview event editing in a modal dialog and calendar-event creation for connected providers
- modal delete confirmation before removing a saved job
- loading skeletons and post-load status banners for the list and detail panels
- status-source messaging that explains whether the latest interview or rejection change was synced from Gmail or saved manually
- optimistic in-place updates after calendar sync and other mutations (no full list reload)

The settings page also supports:

- connecting and disconnecting Google, Microsoft, GitHub, and Gmail integrations through the API
- a GitHub section that shows connection status (login, display name, email) and a connect/disconnect flow with callback handling at `/integrations/github/callback`
- section status chips (loading, connected count, sync health) and loading skeletons while connection data loads
- mailbox sync status, last sync time, and the most recent sync error for connected Gmail accounts
- modal confirmation before disconnecting a calendar, GitHub, or mail provider

The My CV page (`/my-cv`) supports:

- uploading a single PDF CV (validated server-side; replaces any previous upload and auto-imports structured sections)
- inline metadata (original filename, size, uploaded time, import status) and an iframe preview of the original PDF
- structured section panels with inline edit, add entry, drag-and-drop reorder, and save per section or for section order
- **Re-import from PDF** to refresh structured content from the stored upload
- **AI update** panel for natural-language edits with optional focus sections (`POST /api/cv-documents/current/structured/ai-update`)
- **Improvement suggestions** panel to generate, review, and apply AI suggestions (`POST /api/cv-documents/current/structured/ai-suggestions`)
- **Preview/download formatted CV** with template picker, optional compact-fit page target, and page-count metadata via `GET /api/cv-documents/current/export/download`
- **Replace CV** and **Delete CV** actions with a confirmation dialog before removal
- loading skeletons and separate error banners for metadata load, upload, structured save, export, preview, and delete failures

The Projects page (`/cv-projects`) supports:

- a connect-GitHub prompt when no GitHub account is linked, with a shortcut to settings
- live repository listing from the connected GitHub account with refresh and paginated **Load more**
- client-side search across loaded repo name, description, and primary language
- optional inclusion of forks and archived repositories
- one-at-a-time **Generate summary** / **Regenerate** actions backed by Google AI (`POST /api/cv-projects/generate`)
- a saved-summaries panel showing CV title, summary, bullets, tech stack, and GitHub link
- per-summary remove actions and automatic cleanup when GitHub is disconnected
- loading skeletons and inline error banners for repo load, generation, and summary fetch failures

The Job Search page (`/search`) supports:

- source toggle between **EURES** and **Work in Denmark** (Jobnet) when multiple providers are enabled
- multi-keyword search with removable keyword chips and popular IT suggestion groups
- country picker with common ISO codes (Denmark, Sweden, Germany, and others) for EURES searches
- shareable URL state for source, keywords, location, and selected listing (for example `/search?source=jobnet&keywords=software,angular&location=dk&selected=<listing-id>`)
- server-ranked results with **Load more** and automatic fetch when the list or page scroll nears the bottom; retry on load-more errors
- shared `external-job-card` and `external-job-detail` presentation components for list selection, saved-state badges, and detail actions
- re-search when keywords, country, or source change after an initial search
- formatted publication dates, mobile Results/Detail tabs, keyboard arrow navigation in the list, and focus management after search
- a shared `job-description-panel` that renders full sanitized HTML when the API marks a description as `full`, shows a short excerpt plus quality reason with a **Read full listing** link when quality is `previewOnly`, and handles missing descriptions gracefully
- **Save to ApplyVault** on the detail panel, with retry on error and a link to the saved job on `/jobs`
- loading skeletons during search, detail fetch, and incremental load-more
- optimistic detail rendering for Work in Denmark: listing title, employer, and location appear immediately on selection; only the description area waits for or retries the detail request

#### Jobnet (Work in Denmark) data flow

Jobnet listings use two id shapes from the upstream BFF:

- **GUID ids** (for example `b2b58b21-...`) — native Jobnet postings; full detail comes from `/FindJob/JobAdDetails/{id}`. When `WorkInDenmarkOnly` is enabled, search may fetch native detail for GUID candidates to confirm the `WorkInDenmark` classification before they appear in results.
- **E-prefixed ids** (for example `E11069412`) — EURES-imported listings shown on Jobnet; native detail returns 400, so description and metadata come from the search payload.

During search, ranked listings are mapped to `JobnetJobListingDto` for the list UI and the raw search payload is cached by job id. When the dashboard requests detail (`GET /api/jobnet/jobs/{id}`), `JobnetJobDetailFetcher` reads the cache first (for E-prefixed jobs) or the native detail endpoint (for GUID jobs), then `JobnetDescriptionQualityAssessor` marks descriptions as `full` or `previewOnly` based on content heuristics (scraped nav junk), not the id prefix alone.

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

Unit tests cover the Gmail mail client, mail sync processor, email classification rules, the email-driven job/interview update services, EURES and Jobnet job search (client, keyword expander, ranked-result caching, search payload cache, mapper, relevance scoring, request normalization, description quality assessor, heuristic rules, detail fetcher, and detail composer), and CV structured import/export/update services. Integration tests cover scrape-result auth and per-user tenancy over HTTP.

### 6. Run frontend critical-path tests

Component tests use Karma + Jasmine with a mocked HTTP layer (no real Supabase or API required):

```bash
cd frontend/applyvault-jobs-ui
npm install
npm run test:ci
```

Use `npm test` (or `ng test`) for watch mode during development.

Coverage includes auth guards, the auth interceptor, app shell session display, saved jobs list/empty state, unified job search flows (EURES and Jobnet source switching), job-search URL state helpers, and job description display/render utilities. Shared test helpers live in `frontend/applyvault-jobs-ui/src/testing/`.

### 7. CI

GitHub Actions workflow [`.github/workflows/api-ci.yml`](.github/workflows/api-ci.yml) runs on push/PR to `main`:

- **api-ci** — `dotnet build` + unit and integration tests
- **frontend-ci** — `npm ci` + `npm run test:ci` in `frontend/applyvault-jobs-ui`

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
9. Optionally upload a CV PDF from **My CV** (`/my-cv`); local dev stores files under `App_Data/cv-documents` when `CvDocumentStorage:Provider` is `Local`, and structured sections are extracted automatically on upload.
10. Edit structured CV sections, run AI updates or suggestions, and download a formatted export when ready.
11. Optionally connect GitHub from dashboard settings after enabling `GitHubIntegration` and configuring a GitHub OAuth App, then open **Projects** (`/cv-projects`) to browse repositories and generate AI-written personal-project entries (requires `GoogleAi:Enabled`).
12. Optionally connect Gmail from dashboard settings after enabling `MailIntegration` and configuring Gmail OAuth credentials.
13. Let the background mail sync poll for new Gmail messages and auto-update matched jobs when interview or rejection emails arrive.
14. If the role progresses, save an interview time manually or let Gmail sync detect it, then push it to a connected calendar provider.
15. Optionally open **Search** (`/search`), pick EURES or Work in Denmark, run a keyword search, load additional pages of results, save a listing to ApplyVault, and inspect listing details before opening the source posting.

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
14. If `GitHubIntegration` is enabled, connect GitHub from settings and verify the OAuth callback returns to `/integrations/github/callback?provider=github&success=true`, then confirm the connected account appears on the settings page.
15. Open `/my-cv`, upload a PDF, and confirm metadata, structured section import status, and an inline preview appear; replace the file and confirm structured content refreshes.
16. Edit a structured section, reorder sections, and confirm changes persist after refresh; use **Re-import from PDF** and confirm sections update from the stored upload.
17. With `GoogleAi:Enabled`, run an AI section update and generate improvement suggestions; apply at least one suggestion and confirm the structured content changes.
18. Download a formatted CV export with a non-default template and confirm a PDF downloads successfully.
19. Delete the CV and confirm the confirmation dialog is required; verify the empty state returns and `GET /api/cv-documents/current` returns **404**.
20. With GitHub connected and `GoogleAi:Enabled`, open `/cv-projects`, load repositories, generate a summary for one repo, and confirm the saved panel shows title, summary, bullets, and tech stack.
21. Regenerate the same repository and confirm the saved summary updates; remove it and confirm it disappears from the saved panel.
22. Disconnect GitHub and confirm the disconnect confirmation modal appears before removal; verify saved CV project summaries are cleared after disconnect.
23. If `MailIntegration` is enabled, connect Gmail from settings and verify the callback returns to the dashboard with a success state.
24. Confirm the settings page shows mailbox sync status, last synced time, and any sync error details for the connected Gmail account.
25. Send or surface a recent Gmail rejection/interview email for a saved job, wait for the poll interval, and verify the job detail shows Gmail as the latest status source.
26. If Gmail sync detects interview details and a calendar provider is already connected, verify the linked interview can still be pushed to the provider from the dashboard flow.
27. Open a restricted page like `chrome://extensions` and confirm the extension reports a graceful error.
28. On `/jobs`, filter by search term, source, and workflow (for example **Needs review**); sort by title or interview date and confirm the filter summary updates while stats stay based on the full saved dataset.
29. Clear filters and confirm the full list returns; open interview editing and confirm the modal dialog saves and dismisses correctly.
30. Open `/search?source=eures`, run a keyword search with a country from the picker, and verify results load in the card list with a selectable detail panel.
31. Switch to **Work in Denmark** (`/search?source=jobnet`) and run a search; confirm Jobnet listings load with the same card/detail UX.
32. Click **Load more** (or scroll near the bottom) and confirm additional listings append without losing the current selection.
33. Refresh `/search?source=eures&keywords=software&location=dk` and confirm source, keywords, location, and selection restore from the URL.
34. Select a listing, confirm sanitized description content renders (full HTML when quality is `full`), and verify the external listing link opens the source posting.
35. Select a Work in Denmark listing whose description quality is `previewOnly` (common for EURES-imported jobs) and confirm the detail panel shows an excerpt, quality reason, and **Read full listing** link instead of raw scraped page content.
36. Click **Save to ApplyVault**, confirm success (or graceful duplicate handling), and follow the link to `/jobs?selected=...`.
37. Delete a saved job and confirm the modal confirmation step is required before removal.
38. On `/settings`, connect or disconnect a calendar, GitHub, or mail integration and confirm the disconnect confirmation modal appears before removal.
39. Visit an unknown path such as `/does-not-exist` and confirm the 404 page links to the correct home route for your auth state.
40. Sign out from any authenticated page and confirm you return to `/login`.

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
| 13 Logging and monitoring | Done | [`deploy/RUNBOOK.md`](deploy/RUNBOOK.md) |
| 14 Rate limiting | Done | [`production-readiness/prod-14-rate-limiting.md`](plans/production-readiness/prod-14-rate-limiting.md) |
| 15 Frontend critical-path tests | Done | [`production-readiness/prod-15-frontend-critical-path-tests.md`](plans/production-readiness/prod-15-frontend-critical-path-tests.md) |
| 16 EURES cache (multi-instance) | Done | [`production-readiness/prod-16-eures-cache-multi-instance.md`](plans/production-readiness/prod-16-eures-cache-multi-instance.md) |
| 17 Gmail sync (multi-instance) | Done | [`production-readiness/prod-17-gmail-sync-multi-instance.md`](plans/production-readiness/prod-17-gmail-sync-multi-instance.md) |

**Completed (steps 1–17):** Authenticated scrape ingest; per-user data isolation; HTTP integration tests; environment config and migrations; CI; Docker/Caddy deploy; frontend/extension production builds; OAuth, CORS/HSTS, health probes, structured logging, rate limiting; Karma specs for dashboard auth, jobs, and job search critical paths; distributed EURES/Jobnet ranked-result cache and Gmail sync locking via optional Redis.

**Local foundations in place:** Config-driven CORS with HTTPS origin validation, HSTS at the Caddy edge, tagged readiness/liveness probes at `/health` and `/health/live`, JSON request logging, and partition-based rate limits (global API, scrape ingest, EURES search, Jobnet search, OAuth callbacks) with `429` + `Retry-After`.

**Multi-instance hosting:** Set `ConnectionStrings:Redis` when running more than one API replica so EURES/Jobnet ranked caches and Gmail sync use shared Redis instead of per-process memory. See [`deploy/RUNBOOK.md`](deploy/RUNBOOK.md#multi-instance-redis).

**After pulling step 2:** restart the API so the new migration runs (`Database.Migrate()` at startup). Orphan `UserId IS NULL` rows are soft-deleted then deleted before the column becomes required.

## Notes

- The Angular UI style prompt lives in `md/angular-ui-style-guidance.md`.
- The frontend app keeps its own local `README.md` for Angular CLI-specific commands.
