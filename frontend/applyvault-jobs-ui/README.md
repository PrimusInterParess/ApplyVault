# ApplyvaultJobsUi

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 19.2.23.

## Development server

To start a local development server, run:

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4200/`. The application will automatically reload whenever you modify any of the source files.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

Configuration is baked in at build time via `src/environments/`. See [plans/production-readiness/FRONTEND.md](../../plans/production-readiness/FRONTEND.md).

```bash
npm run build:production   # dist/applyvault-jobs-ui/browser/
npm run build:staging
ng build --configuration development
```

Before a production or staging build, set `apiBaseUrl` and `supabase` in `environment.production.ts` or `environment.staging.ts` to match your deployed API and Supabase project.

## Running unit tests

To execute unit tests with the [Karma](https://karma-runner.github.io) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
