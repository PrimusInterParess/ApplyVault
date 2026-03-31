# ApplyVault

ApplyVault is a Chrome Manifest V3 extension that captures job listing details from the active tab through a popup action and can save the result to a local ASP.NET API backed by SQL Server through EF Core.

## Architecture

- `src/popup`
  - UI for triggering a scrape and displaying the result.
- `src/background`
  - Service worker that coordinates the scrape use case.
- `src/content`
  - Content script plus pure DOM text extraction logic.
- `src/application`
  - Use-case orchestration layer for scrape and save flows.
- `src/infrastructure`
  - Chrome API gateway and ASP.NET API adapter.
- `src/shared`
  - Shared contracts, models, and utilities.
- `api/ApplyVault.Api`
  - Local ApplyVault ASP.NET Core controller-based API that accepts captured results.
- `api/ApplyVault.Api/Controllers`
  - HTTP controllers for health checks and scrape result persistence.

## Build

```bash
npm install
npm run build
```

The unpacked extension output is generated in `dist/`.

## Load In Chrome

1. Open `chrome://extensions`.
2. Enable Developer mode.
3. Click Load unpacked.
4. Select the `dist` folder from this project.

## Manual Verification

1. Open a normal website, such as a documentation page or article.
2. Click the extension icon.
3. Press `Scrape current page`.
4. Confirm the popup button changes to `Save to API`.
5. Press `Save to API` and confirm the popup reports a successful save.
6. Open a restricted page like `chrome://extensions` and confirm the popup reports a graceful error.

## Run The ASP.NET API

```bash
dotnet run --project api/ApplyVault.Api --launch-profile http
```

The ApplyVault extension posts to `http://localhost:5173/api/scrape-results`.

By default, the API uses the `ApplyVault` SQL Server LocalDB database via the `ApplyVault` connection string in `api/ApplyVault.Api/appsettings.json`.
Startup applies EF Core migrations automatically with `Database.Migrate()`.

Useful endpoints:

- `GET /api/health`
- `GET /api/scrape-results`
- `GET /api/scrape-results/{id}`
- `POST /api/scrape-results`

## Extension To API Flow

1. Click `Scrape current page` in the popup.
2. Review or edit the scraped fields.
3. Click `Save to API`.
4. The popup sends the `ScrapeResult` payload to the local ASP.NET API.
