# ApplyVault

ApplyVault is a job-capture workspace built from three connected parts:

- a Chrome Manifest V3 extension that scrapes job listing details from the active tab
- a local ASP.NET Core API backed by EF Core and SQL Server LocalDB
- an Angular dashboard for browsing and filtering saved job results

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
  UI for triggering a scrape and displaying the captured result.
- `src/background`
  Service worker that coordinates scrape and save flows.
- `src/content`
  Content script plus DOM extraction logic.
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

## Typical Flow

1. Start the API.
2. Build and load the extension in Chrome.
3. Open a job listing and click `Scrape current page`.
4. Review the scraped fields and click `Save to API`.
5. Start the Angular dashboard and review saved results in the browser.

## Manual Verification

1. Open a normal website or job page in Chrome.
2. Use the extension to scrape the current page.
3. Save the result to the API and confirm the request succeeds.
4. Open the dashboard and verify the new job appears in the list.
5. Use the search and source filters to confirm filtering works.
6. Open a restricted page like `chrome://extensions` and confirm the extension reports a graceful error.

## Notes

- The Angular UI style prompt lives in `md/angular-ui-style-guidance.md`.
- The frontend app keeps its own local `README.md` for Angular CLI-specific commands.
