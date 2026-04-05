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
- Sign in to the extension and dashboard with Supabase-backed authentication.
- Store saved results in LocalDB through EF Core migrations applied automatically at API startup.
- Browse saved jobs in the Angular dashboard and inspect a dedicated detail panel.
- Mark saved results as rejected, review key capture fields, and clean up Markdown descriptions from the dashboard.
- Save interview timing on a job and create linked calendar events for connected Google or Microsoft accounts.
- Connect a Gmail mailbox from settings and poll for job-related emails with a hosted background sync worker.
- Auto-apply Gmail-detected rejection and interview updates to matching saved jobs, including interview calendar follow-up when calendar providers are already connected.
- Show whether the latest rejection or interview update came from Gmail sync or a manual dashboard action.
- Render saved job descriptions as Markdown in the dashboard detail view.

## Repository Layout

- `src/`
  Chrome extension source, including popup UI, background service worker, content scripts, and shared contracts.
- `api/ApplyVault.Api/`
  ASP.NET Core API that stores and serves captured job results.
- `api/ApplyVault.Api.Tests/`
  xUnit coverage for mail sync, Gmail client behavior, job-status classification, and related API services.
- `frontend/applyvault-jobs-ui/`
  Angular application for reviewing saved results in a dashboard-style UI.
- `plans/`
  Product and implementation planning documents for upcoming roadmap work.
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

The unpacked extension output is generated in `dist/`.

### 2. Run the ASP.NET API

```bash
dotnet run --project api/ApplyVault.Api --launch-profile http
```

The API listens on `http://localhost:5173/api` and exposes:

- `GET /api/health`
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

`POST /api/scrape-results` is available for ingestion from the extension. The review, dashboard, and mail-connection management endpoints require authentication, except for the provider callback that completes OAuth.

Saved results include the raw scrape payload, structured job details, persisted `isRejected` state, optional interview metadata, linked calendar events, capture-quality metadata for reviewable fields, and status-sync metadata that records whether the latest rejection or interview update came from Gmail or a manual edit.

By default, the API uses the `ApplyVault` SQL Server LocalDB database via the `ApplyVault` connection string in `api/ApplyVault.Api/appsettings.json`. Startup applies EF Core migrations automatically with `Database.Migrate()`.

### 3. Configure API integrations

The API reads several option sections from `api/ApplyVault.Api/appsettings.json` and local environment overrides:

- `GoogleAi`
  Optional AI repair for low-confidence captures. Provide an API key and model when you want enrichment enabled.
- `ScrapeResultEnrichment`
  Turns the low-confidence enrichment pass on or off and controls whether AI failures should block saving.
- `Supabase`
  Configures JWT validation for authenticated dashboard and extension requests.
- `CalendarIntegration`
  Configures the OAuth client details and redirect URLs used to connect Google and Microsoft calendar providers.
- `MailIntegration`
  Enables Gmail sync, configures the Gmail OAuth client and callback URL, sets the Angular post-connect redirect, and controls poll cadence plus the initial mailbox lookback window used by the background sync worker.

Prefer local overrides, environment variables, or user secrets for development credentials instead of checking secrets into source-controlled config files.

### 4. Run the Angular dashboard

```bash
cd frontend/applyvault-jobs-ui
npm install
npm start
```

The dashboard runs on `http://localhost:4200/` and reads saved results from `http://localhost:5173/api`.

The saved-job detail view now supports:

- capture quality review with field-level confidence and review reasons
- Markdown description cleanup and rendering
- interview event editing
- calendar-event creation for connected providers
- status-source messaging that explains whether the latest interview or rejection change was synced from Gmail or saved manually

The settings page also supports:

- connecting and disconnecting Gmail mailboxes through the API
- showing mailbox sync status, last sync time, and the most recent sync error

### 5. Run backend tests

```bash
dotnet test api/ApplyVault.Api.Tests/ApplyVault.Api.Tests.csproj
```

This test project covers the Gmail mail client, mail sync processor, email classification rules, and the email-driven job/interview update services.

## Load The Extension In Chrome

1. Open `chrome://extensions`.
2. Enable Developer mode.
3. Click Load unpacked.
4. Select the root `dist` folder from this project.

## Supabase Email-Code Setup

The extension sign-in flow expects a typed numeric email OTP, not a clicked magic link.

In your Supabase project's `Magic Link` email template, include `{{ .Token }}` in the email body so users receive the code that `verifyOtp({ email, token, type: 'email' })` expects. If the template only uses `{{ .ConfirmationURL }}`, Supabase will send a magic link instead of the code required by the extension.

## Typical Flow

1. Start the API.
2. Build and load the extension in Chrome.
3. Open a supported job listing and click `Scrape current page`.
4. Review the scraped text and structured fields in the popup, make any needed edits, and click `Save`.
5. Let the API score capture quality and optionally enrich weak fields before persisting the result.
6. Start the Angular dashboard and review saved results in the browser.
7. Open a saved result to inspect capture confidence, review low-confidence fields, clean up the description, or mark it as rejected.
8. Optionally connect Gmail from dashboard settings after enabling `MailIntegration` and configuring Gmail OAuth credentials.
9. Let the background mail sync poll for new Gmail messages and auto-update matched jobs when interview or rejection emails arrive.
10. If the role progresses, save an interview time manually or let Gmail sync detect it, then push it to a connected calendar provider.

## Manual Verification

1. Open a supported job page in Chrome such as LinkedIn, Workday, Greenhouse, or Lever.
2. Use the extension to scrape the current page.
3. Confirm the popup fills in structured fields such as job title, company, location, description, summary, and contacts.
4. Edit one or more popup fields, save the result to the API, and confirm the request succeeds.
5. Open the dashboard and verify the new job appears in the list.
6. Check that the detail view shows capture confidence, review state, and field-level review guidance.
7. If the result is flagged for review, update the job title, company, or location and verify the reviewed state persists after refresh.
8. Edit the saved description in the dashboard and verify the rendered Markdown updates.
9. Save an interview event, refresh, and verify the interview timing persists.
10. If a calendar provider is connected, create a calendar event from the saved interview and verify the provider link is returned.
11. Toggle the rejected state and verify the change persists after refresh.
12. If `MailIntegration` is enabled, connect Gmail from settings and verify the callback returns to the dashboard with a success state.
13. Confirm the settings page shows mailbox sync status, last synced time, and any sync error details for the connected Gmail account.
14. Send or surface a recent Gmail rejection/interview email for a saved job, wait for the poll interval, and verify the job detail shows Gmail as the latest status source.
15. If Gmail sync detects interview details and a calendar provider is already connected, verify the linked interview can still be pushed to the provider from the dashboard flow.
16. Open a restricted page like `chrome://extensions` and confirm the extension reports a graceful error.

## Notes

- The Angular UI style prompt lives in `md/angular-ui-style-guidance.md`.
- The frontend app keeps its own local `README.md` for Angular CLI-specific commands.
