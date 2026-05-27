# ApplyVault Jobs UI

Angular dashboard for reviewing saved job captures, searching EURES listings, generating CV project summaries from GitHub, and managing calendar/mail integrations.

## Development server

```bash
npm install
npm start
```

Open `http://localhost:4200/`. The app calls the API at `http://localhost:5173/api` (see `src/environments/environment.ts`).

## Building

Configuration is baked in at build time via `src/environments/`. See [plans/production-readiness/FRONTEND.md](../../plans/production-readiness/FRONTEND.md).

```bash
npm run build:production   # dist/applyvault-jobs-ui/browser/
npm run build:staging
ng build --configuration development
```

Before a production or staging build, set `apiBaseUrl` and `supabase` in `environment.production.ts` or `environment.staging.ts` to match your deployed API and Supabase project.

## Tests

Critical-path component tests use [Karma](https://karma-runner.github.io) + Jasmine with `HttpClientTestingModule` and mocked auth — no live Supabase or API required.

```bash
npm test          # watch mode (ng test)
npm run test:ci   # single run, headless Chrome (CI)
```

| Spec | What it covers |
|------|----------------|
| `auth.guard.spec.ts` | Guest redirect to `/login`; authenticated access |
| `auth.interceptor.spec.ts` | `Authorization: Bearer` attached when session present |
| `app-shell.component.spec.ts` | Signed-in user email in the shell |
| `job-results-page.component.spec.ts` | Empty state and saved job cards |
| `eures-jobs-page.component.spec.ts` | EURES search request and result rendering |

Shared fixtures and auth mocks: `src/testing/`.

## Code scaffolding

```bash
ng generate component component-name
ng generate --help
```

## Additional resources

- Root project README: [`../../README.md`](../../README.md)
- Production frontend config: [`../../plans/production-readiness/FRONTEND.md`](../../plans/production-readiness/FRONTEND.md)
- [Angular CLI overview](https://angular.dev/tools/cli)
