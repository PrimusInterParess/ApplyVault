---
name: Step 15 — Frontend Critical-Path Tests
overview: Add automated tests for dashboard login, session load, jobs list, and EURES search against a mocked or test API.
todos:
  - id: choose-test-tool
    content: Select Playwright or Cypress for Angular E2E (or Jest component tests for narrower scope)
    status: completed
  - id: mock-api
    content: Mock HTTP layer or use test API fixture for auth/session and jobs list
    status: completed
  - id: auth-flow-test
    content: Test login guard redirects and auth interceptor attaches token
    status: completed
  - id: jobs-list-test
    content: Test /jobs loads list and handles empty state
    status: completed
  - id: eures-search-test
    content: Test /eures search submits keywords and renders results
    status: completed
  - id: ci-frontend
    content: Add frontend test job to CI workflow (step 6 extension)
    status: completed
isProject: false
---

# Step 15 — Frontend Critical-Path Tests

**Tracker:** [production-readiness-tracker.md](../production-readiness-tracker.md) · **Prerequisites:** [prod-08-frontend-environment-builds.md](prod-08-frontend-environment-builds.md) · **Next:** [prod-16-eures-cache-multi-instance.md](prod-16-eures-cache-multi-instance.md)

## Problem

Backend has unit and integration tests; the Angular dashboard has no automated coverage for:

- Auth guard + [`auth.interceptor.ts`](../../frontend/applyvault-jobs-ui/src/app/core/auth/auth.interceptor.ts)
- `GET /api/auth/session` → shell renders user
- Jobs list and EURES search happy paths

UI regressions (401 handling, routing) ship without detection.

## Risk

| Risk | Impact |
|------|--------|
| Auth interceptor break | Dashboard unusable after refactor |
| Route guard regression | Open routes or redirect loops |
| EURES URL state break | Shared links fail |

## Goal

Minimal E2E or integration tests covering:

1. Unauthenticated user → redirected to `/login`
2. Authenticated session → `/jobs` renders
3. EURES search triggers API call (mocked)

## Affected code

| Area | Path |
|------|------|
| Auth | [`auth.service.ts`](../../frontend/applyvault-jobs-ui/src/app/core/auth/auth.service.ts), [`auth.guard.ts`](../../frontend/applyvault-jobs-ui/src/app/core/auth/auth.guard.ts) |
| Jobs | `frontend/applyvault-jobs-ui/src/app/features/job-results/` |
| EURES | `frontend/applyvault-jobs-ui/src/app/features/eures-jobs/` |

## Implementation tasks

### 1. Tooling

Add Playwright to `frontend/applyvault-jobs-ui` (or separate `e2e/` project).

### 2. Mock strategy

Prefer **HttpClientTestingModule** component tests for speed, or Playwright with:

- Mock Supabase (hard) → stub `AuthService` in test harness
- Intercept `http://localhost:5173/api/**` with Playwright route

### 3. Critical paths

| Test | Assert |
|------|--------|
| Guest visits `/jobs` | Redirect to `/login` |
| Session mock | App shell shows user email |
| Jobs API mock | List renders N cards |
| EURES search mock | Results panel visible after search |

### 4. CI

Run headless in GitHub Actions after `npm ci` + `ng build`.

## Verification

1. Tests pass locally and in CI.
2. Intentional auth interceptor break fails test.
3. Tests do not require real Supabase or production API.

## Production-grade notes

- Can defer briefly for solo beta per tracker, but add before onboarding external users.
- Keep test count small; focus on regressions that already happened (401 auth wiring).
