# ApplyVault

ApplyVault is a job-capture workspace built from three connected parts:

- a Chrome Manifest V3 extension that scrapes job listing details from the active tab
- a local ASP.NET Core API backed by EF Core and SQL Server LocalDB
- an Angular dashboard for browsing and filtering saved job results

## Current Capabilities

- Scrape page text plus structured job details such as title, company, location, description, summary, and hiring-manager contacts.
- Review and edit extracted fields in the extension popup before saving them to the API.
- Sign in to the extension with a one-time Supabase email code instead of entering a password.
- Store saved results in LocalDB through EF Core migrations applied automatically at API startup.
- Browse saved jobs in the Angular dashboard and inspect a dedicated detail panel.
- Mark saved results as rejected and update the saved job description from the dashboard.
- Render saved job descriptions as Markdown in the dashboard detail view.

## Repository Layout

- `src/`
  Chrome extension source, including popup UI, background service worker, content scripts, and shared contracts.
- `api/ApplyVault.Api/`
  ASP.NET Core API that stores and serves captured job results.
- `frontend/applyvault-jobs-ui/`
  Angular application for reviewing saved results in a dashboard-style UI.
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

Saved results include the raw scrape payload plus structured job details and a persisted `isRejected` flag.

By default, the API uses the `ApplyVault` SQL Server LocalDB database via the `ApplyVault` connection string in `api/ApplyVault.Api/appsettings.json`. Startup applies EF Core migrations automatically with `Database.Migrate()`.

### 3. Run the Angular dashboard

```bash
cd frontend/applyvault-jobs-ui
npm install
npm start
```

The dashboard runs on `http://localhost:4200/` and reads saved results from `http://localhost:5173/api`.

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
5. Start the Angular dashboard and review saved results in the browser.
6. Open a saved result to update the description or mark it as rejected.

## Manual Verification

1. Open a supported job page in Chrome such as LinkedIn, Workday, Greenhouse, or Lever.
2. Use the extension to scrape the current page.
3. Confirm the popup fills in structured fields such as job title, company, location, description, and contacts.
4. Edit one or more popup fields, save the result to the API, and confirm the request succeeds.
5. Open the dashboard and verify the new job appears in the list.
6. Use the search and source filters to confirm filtering works.
7. Open a saved result, toggle its rejected state, and verify the change persists after refresh.
8. Edit the saved description in the dashboard and verify the rendered Markdown updates.
9. Open a restricted page like `chrome://extensions` and confirm the extension reports a graceful error.

## Notes

- The Angular UI style prompt lives in `md/angular-ui-style-guidance.md`.
- The frontend app keeps its own local `README.md` for Angular CLI-specific commands.
