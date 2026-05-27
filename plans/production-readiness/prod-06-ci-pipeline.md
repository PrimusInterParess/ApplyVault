---
name: Step 6 — CI Pipeline
overview: Add continuous integration that builds the API, runs unit and integration tests, and blocks merges on failure.
todos:
  - id: github-workflow
    content: Add .github/workflows/api-ci.yml for build + test on push and PR
    status: completed
  - id: test-matrix
    content: Run ApplyVault.Api.Tests and ApplyVault.Api.IntegrationTests
    status: completed
  - id: no-secrets-in-ci
    content: Confirm CI uses test factory config only; no Supabase or SQL Server required
    status: completed
  - id: branch-protection
    content: Document required status checks for main branch (manual GitHub setting)
    status: completed
  - id: optional-frontend-ci
    content: Optional job for frontend lint/build (can follow step 8)
    status: pending
isProject: false
---

# Step 6 — CI Pipeline

**Tracker:** [production-readiness-tracker.md](../production-readiness-tracker.md) · **Prerequisites:** [prod-03-api-integration-tests.md](../prod-03-api-integration-tests.md), [prod-05-database-and-migrations.md](prod-05-database-and-migrations.md) · **Next:** [prod-07-deployment-and-hosting.md](prod-07-deployment-and-hosting.md)

## Problem

Tests exist locally but nothing enforces them on every change:

- [`ApplyVault.Api.Tests`](../../api/ApplyVault.Api.Tests/)
- [`ApplyVault.Api.IntegrationTests`](../../api/ApplyVault.Api.IntegrationTests/)

Regressions in auth, tenancy, or EURES can merge without running `dotnet test`.

## Risk

| Risk | Impact |
|------|--------|
| Broken JWT wiring merges | Production 401 for all users |
| Tenancy regression | Cross-user data leak |
| No CI feedback | Slower manual QA |

## Goal

GitHub Actions (or equivalent) workflow that on every PR:

1. Restores .NET SDK
2. Builds `api/ApplyVault.Api`
3. Runs unit + integration test projects
4. Fails the check on any test failure

## Implementation tasks

### 1. Workflow file

Create `.github/workflows/api-ci.yml`:

```yaml
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet test api/ApplyVault.Api.Tests/ApplyVault.Api.Tests.csproj --no-restore
      - run: dotnet test api/ApplyVault.Api.IntegrationTests/ApplyVault.Api.IntegrationTests.csproj --no-restore
```

Adjust paths if solution layout differs.

### 2. Integration test isolation

Confirm [`ApplyVaultWebApplicationFactory`](../../api/ApplyVault.Api.IntegrationTests/ApplyVaultWebApplicationFactory.cs):

- Uses in-memory DB
- Replaces Supabase JWT with `TestAuthHandler`
- Does not require network

### 3. Branch protection (manual, one-time)

In GitHub: **Settings → Branches → Branch protection rules** for `main`:

1. Enable **Require status checks to pass before merging**.
2. Select the check named **`api-ci`** (workflow job in [`.github/workflows/api-ci.yml`](../../.github/workflows/api-ci.yml)).
3. Optionally enable **Require branches to be up to date before merging**.

No repository secrets are required for this workflow.

### 4. Optional artifacts

- Publish test results TRX for failed-run debugging.

## Verification

1. Open a PR with a deliberate test failure → CI fails.
2. Fix test → CI passes.
3. Integration tests complete without Supabase or SQL credentials in CI secrets.

## Production-grade notes

- Can run in parallel with step 7 planning; must pass before relying on CI for deploy gates.
- Add frontend CI after step 8 when Angular env builds exist.
