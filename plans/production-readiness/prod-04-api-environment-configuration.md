---
name: Step 4 — API Environment Configuration
overview: Split API configuration by environment, remove secrets from committed files, and document how Development, Staging, and Production settings are loaded.
todos:
  - id: write-plan
    content: Create this plan and link from production-readiness tracker
    status: completed
  - id: appsettings-split
    content: Add appsettings.Production.json and optional Staging; keep appsettings.json as safe defaults
    status: completed
  - id: secrets-out-of-repo
    content: Move API keys and OAuth secrets to user secrets or environment variables; scrub appsettings.Development.json
    status: completed
  - id: env-var-doc
    content: Add appsettings.example.json or ENV.md listing all configuration keys and env var names
    status: completed
  - id: validate-production
    content: Extend ValidateOnStart for Supabase URL in non-Development environments
    status: completed
  - id: update-readme
    content: Document local vs deployed configuration in README
    status: completed
isProject: false
---

# Step 4 — API Environment Configuration

**Tracker:** [production-readiness-tracker.md](../production-readiness-tracker.md) · **Prerequisites:** [prod-03-api-integration-tests.md](../prod-03-api-integration-tests.md) · **Next:** [prod-05-database-and-migrations.md](prod-05-database-and-migrations.md)

## Problem

Configuration is split between `appsettings.json` and `appsettings.Development.json`, but:

- Secrets (Google AI key, OAuth client secrets) live in committed Development config.
- There is no `Production` or `Staging` profile template.
- Operators cannot tell which keys must be set per environment without reading code.

Recent refactors added typed options and `ValidateOnStart()` in [`ServiceCollectionExtensions.cs`](../../api/ApplyVault.Api/Infrastructure/ServiceCollectionExtensions.cs), but production env wiring is incomplete.

## Risk

| Risk | Impact |
|------|--------|
| Secrets in git history | Credential leak |
| Missing prod config | API starts with dev defaults or fails opaquely at runtime |
| Wrong Supabase URL in prod | All dashboard users get 401 |

## Goal

1. `appsettings.json` contains **safe defaults only** (empty secrets, disabled integrations).
2. `appsettings.Development.json` overrides for local dev (gitignored or secret-free template).
3. `appsettings.Production.json` documents structure; real values come from env vars / host config.
4. One reference table maps each options section → env var → purpose.

## Affected code

| Area | Files |
|------|-------|
| Options binding | [`ServiceCollectionExtensions.cs`](../../api/ApplyVault.Api/Infrastructure/ServiceCollectionExtensions.cs) |
| Option types | [`Options/`](../../api/ApplyVault.Api/Options/) |
| Base config | [`appsettings.json`](../../api/ApplyVault.Api/appsettings.json) |
| Integration test overrides | [`ApplyVaultWebApplicationFactory.cs`](../../api/ApplyVault.Api.IntegrationTests/ApplyVaultWebApplicationFactory.cs) |

## Implementation tasks

### 1. Safe base config

- Strip secrets from [`appsettings.json`](../../api/ApplyVault.Api/appsettings.json).
- Set `GoogleAi:Enabled` false, `MailIntegration:Enabled` false by default in base file.
- Keep `Supabase:Url` as placeholder or empty in base; require env override in Production.

### 2. Environment-specific files

Create:

- `appsettings.Production.json` — production structure, no secrets
- Optional `appsettings.Staging.json` if you use a staging slot

### 3. Secrets strategy

Pick one primary approach:

- **Local dev:** `dotnet user-secrets` for [`ApplyVault.Api.csproj`](../../api/ApplyVault.Api/ApplyVault.Api.csproj)
- **Deployed:** environment variables (`ConnectionStrings__ApplyVault`, `Supabase__Url`, `GoogleAi__ApiKey`, etc.)

Add `appsettings.example.json` (or `plans/production-readiness/ENV.md`) listing all keys.

### 4. Production validation

In `AddApplyVaultOptions`, when `!IHostEnvironment.IsDevelopment()`:

- Require non-empty `Supabase:Url`
- Require `Cors:AllowedOrigins` has at least one entry

### 5. Do not break tests

Integration tests already override config in [`ApplyVaultWebApplicationFactory`](../../api/ApplyVault.Api.IntegrationTests/ApplyVaultWebApplicationFactory.cs). Confirm `Testing:UseInMemoryDatabase` still works after base config changes.

## Verification

1. Fresh clone + `dotnet run` with Development profile still works using user secrets or local Development file.
2. `ASPNETCORE_ENVIRONMENT=Production` with missing Supabase URL fails fast at startup with a clear validation message.
3. No API keys or OAuth secrets remain in tracked JSON files.

## Production-grade notes

- Never commit `appsettings.Development.json` if it contains real secrets; use `.gitignore` or a `*.local.json` pattern.
- Document the double-underscore env var convention for nested options in README.
