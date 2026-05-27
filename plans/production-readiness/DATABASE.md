# ApplyVault — database and migrations

Production SQL Server setup, migration strategy, and operator runbook. Implements [prod-05-database-and-migrations.md](prod-05-database-and-migrations.md).

## Production database

Use a dedicated SQL Server instance (Azure SQL, Amazon RDS, or a managed SQL Server). Do not use LocalDB or `(localdb)` in deployed environments.

Set the connection string via host configuration:

| Key | Env var |
|-----|---------|
| `ConnectionStrings:ApplyVault` | `ConnectionStrings__ApplyVault` |

Example (Azure SQL):

```
Server=tcp:your-server.database.windows.net,1433;Database=ApplyVault;User ID=...;Password=...;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
```

## Migration strategy

The API registers `IDatabaseInitializer`. For SQL Server:

- **`Database:MigrateAtStartup` = true (Option A):** `RelationalDatabaseInitializer` calls `Database.Migrate()` on startup. Use for **one API instance** only. During deploy, scale to a single instance until migration completes, then scale out.
- **`Database:MigrateAtStartup` = false (Option B):** migrations are **not** applied at startup. Run them in CI or a deploy job **before** switching traffic. Required before running **multiple API replicas** (see steps 16–17).

`appsettings.Production.json` defaults to Option B (`MigrateAtStartup: false`).

### Option A — migrate at startup

1. Set `Database__MigrateAtStartup=true` (or omit; base default is true).
2. Deploy one API instance; wait for startup and `GET /health` (database check healthy).
3. Scale out only after migration succeeded on the first instance.

### Option B — CI / deploy job (recommended for production)

From the API project directory, with `ConnectionStrings__ApplyVault` set to the target database:

```bash
cd api/ApplyVault.Api
dotnet ef database update
```

Then deploy the API with `Database__MigrateAtStartup=false` (Production template).

## EF Core migrations

All migrations live in `api/ApplyVault.Api/Migrations/`. Latest tenancy migration: `20260527055121_EnforceScrapeResultUserOwnership`.

### Fresh (empty) database

```bash
cd api/ApplyVault.Api
$env:ConnectionStrings__ApplyVault = "<production connection string>"
dotnet ef database update
```

Verify the API starts and `GET /health` reports the database check as healthy.

### Existing development database

Restart the API with `MigrateAtStartup` enabled (default in Development). No manual SQL beyond a normal deploy restart.

### Upgrading old databases (step 2 tenancy)

Migration `EnforceScrapeResultUserOwnership` runs before schema change:

1. Soft-deletes orphan rows: `UserId IS NULL` and `IsDeleted = 0`
2. Hard-deletes rows still with `UserId IS NULL`
3. Makes `UserId` non-nullable

If migration fails, inspect `ScrapeResults` for unexpected `UserId IS NULL` rows and resolve before retrying.

## Backups

- **Production:** enable automated daily backups on the SQL host (Azure SQL automated backup, RDS backup window, etc.).
- **Before manual schema change:** take a full backup or point-in-time snapshot immediately before `dotnet ef database update` or a deploy that enables `MigrateAtStartup`.

## Moving from LocalDB / dev SQL to hosted SQL

1. Back up the dev database if you need to preserve data.
2. Provision production SQL Server and set `ConnectionStrings__ApplyVault`.
3. On an empty production database, run `dotnet ef database update` (Option B) or start a single API instance with `MigrateAtStartup=true` (Option A).
4. To **migrate data** (optional): use bacpac export/import, `SqlPackage`, or a one-time data script — schema must already match the latest migration.
5. Point the API at the new connection string; confirm `GET /health`.

## Tests

Integration tests use `Testing:UseInMemoryDatabase=true` and never connect to production SQL. Do not set production connection strings in test configuration.

## Related configuration

See [ENV.md](ENV.md) for `Database:MigrateAtStartup` and connection string env vars.
