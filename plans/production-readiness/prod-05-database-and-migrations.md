---
name: Step 5 — Database and Migrations
overview: Define production database setup, connection strings, and a migration strategy that is safe for deployed environments.
todos:
  - id: prod-connection-string
    content: Document and configure production SQL Server connection via env vars (step 4)
    status: completed
  - id: migration-strategy
    content: Decide migrate-at-startup vs dedicated deploy job; document in runbook
    status: completed
  - id: backup-restore
    content: Add backup/restore notes for production database
    status: completed
  - id: localdb-to-sql
    content: Document moving from LocalDB/dev SQL to hosted SQL Server
    status: completed
  - id: migration-smoke-test
    content: Verify all migrations apply cleanly on empty production database
    status: completed
isProject: false
---

# Step 5 — Database and Migrations

**Tracker:** [production-readiness-tracker.md](../production-readiness-tracker.md) · **Prerequisites:** [prod-04-api-environment-configuration.md](prod-04-api-environment-configuration.md) · **Next:** [prod-06-ci-pipeline.md](prod-06-ci-pipeline.md)

## Problem

The API applies EF Core migrations at startup via [`RelationalDatabaseInitializer`](../../api/ApplyVault.Api/Data/RelationalDatabaseInitializer.cs) and [`InitializeApplyVaultDatabase`](../../api/ApplyVault.Api/Data/ApplyVaultDatabaseExtensions.cs). This works locally but needs an explicit production strategy:

- Which SQL Server instance hosts production data?
- Who runs migrations on deploy (app vs CI job)?
- How are backups handled before schema changes?

## Risk

| Risk | Impact |
|------|--------|
| Multiple replicas calling `Migrate()` concurrently | Migration race / deploy failure |
| No backup before migration | Irreversible data loss |
| LocalDB connection string in prod | API cannot start |

## Goal

1. Production uses a dedicated SQL Server (Azure SQL, RDS, or managed instance).
2. Documented migration runbook (single writer during deploy).
3. Clean migration from empty database through latest [`Migrations/`](../../api/ApplyVault.Api/Migrations/).

## Affected code

| Component | File |
|-----------|------|
| DbContext | [`ApplyVaultDbContext.cs`](../../api/ApplyVault.Api/Data/ApplyVaultDbContext.cs) |
| Startup init | [`ApplyVaultDatabaseExtensions.cs`](../../api/ApplyVault.Api/Data/ApplyVaultDatabaseExtensions.cs) |
| Migration gate | [`DatabaseOptions.cs`](../../api/ApplyVault.Api/Options/DatabaseOptions.cs), [`RelationalDatabaseInitializer.cs`](../../api/ApplyVault.Api/Data/RelationalDatabaseInitializer.cs) |
| Runbook | [DATABASE.md](DATABASE.md) |
| Latest tenancy migration | [`20260527055121_EnforceScrapeResultUserOwnership.cs`](../../api/ApplyVault.Api/Migrations/20260527055121_EnforceScrapeResultUserOwnership.cs) |

## Implementation (done)

### 1. Connection string

- Base `appsettings.json` has empty `ConnectionStrings:ApplyVault` (no LocalDB in committed defaults).
- Local connection string in `appsettings.Development.example.json`.
- Production rejects LocalDB connection strings when `ASPNETCORE_ENVIRONMENT` is not Development.

### 2. Migration strategy

- **`Database:MigrateAtStartup`** — `true` for local dev; `false` in `appsettings.Production.json` / Staging (Option B).
- Documented in [DATABASE.md](DATABASE.md).

### 3. Pre-flight checks

- Empty DB: `dotnet ef database update` documented.
- Existing dev DB: restart API with `MigrateAtStartup` true.
- Orphan `UserId IS NULL` cleanup documented for step 2 migration.

### 4. Backup

- Daily backup and pre-migration snapshot documented in [DATABASE.md](DATABASE.md).

## Verification

1. `dotnet ef database update` succeeds against a fresh SQL Server database.
2. API starts and `GET /health` reports database healthy.
3. Runbook documents who runs migrations and when.

## Production-grade notes

- Prefer **Option B** before running multiple API replicas.
- Keep integration tests on in-memory DB (`Testing:UseInMemoryDatabase`); do not point tests at production SQL.
