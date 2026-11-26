# ApplyVault Jobs UI

Angular dashboard for reviewing saved job captures, searching EURES and Work in Denmark (Jobnet) listings, uploading a CV PDF, generating CV project summaries from GitHub, and managing calendar/mail integrations.

## Development server

```bash
npm install
npm start
```

Open `http://localhost:4200/`. The app calls the API at `http://localhost:5173/api` (see `src/environments/environment.ts`).

### Interview preparation packs

Route: `/interview-prep`. `?jobId=<scrape-result-guid>` preselects a saved job; `?packId=<guid>` opens a saved pack.

1. Run the API with live Gemini enabled (`GoogleAi:Enabled`, API key, and `InterviewPrep:Ai:Enabled`). The explicit `InterviewPrep:Ai:UseFakeProvider=true` option renders a labeled demo, not personalized material.
2. Sign in and build or import a Structured CV.
3. Choose an optional saved job; without one, specify target role and level. Select relevant saved project summaries and add any additional facts, weaknesses, formats, and study time (1–90 preparation days, 15–240 minutes per day). The form shows the remaining requirements if planning is disabled.
4. Click **Plan preparation documents**, then open a document. Answer its 1–3 clarification questions, shown one at a time. **Help me answer** explains how to form an answer, offers a blank starting structure and a hypothetical example, and lets you ask for more guidance. **I don't know** and **Skip** are valid responses.
5. Use **Save context** to pause, or **Save answers and write this document** to produce its teaching material. Saved answers are available across the pack. Help responses and drafts never automatically become facts about the user.
6. Attempt the exercises and record readiness and a review log. Use **Save progress and review log** to persist those changes. Download the pack as Markdown; unwritten documents are clearly marked.
7. Refine a document's answers and rewrite that document individually. Other documents and their readiness remain; study days using the rewritten document reset. **Create a new outline** replaces the whole pack after confirmation, resetting its answers and progress. Failed AI operations preserve saved material.

Existing packs remain readable and can gain a discovery step for individual documents without rebuilding the pack. Opening a later unwritten document refreshes unanswered questions when shared context has advanced, while drafts already in progress stay intact.

The UI has one document experience; the former practice chat and study-outline interfaces are retired. Live generation can take several minutes. The API validates links, prerequisite order, and daily time limits; it does not browse references or execute reference code.

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
| `job-search-page.component.spec.ts` | Unified search page (EURES and Jobnet source switching) |
| `job-search.facade.spec.ts` | Search facade request/response handling |
| `job-search-url-state.utils.spec.ts` | Shareable `/search` URL query state |
| `job-description-display.util.spec.ts` | Full vs preview-only description display mode |
| `job-description-render.util.spec.ts` | Sanitized HTML description rendering |

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
