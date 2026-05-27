# ApplyVault API — configuration reference

Configuration is loaded in this order (later wins):

1. `appsettings.json` — safe defaults, no secrets
2. `appsettings.{Environment}.json` — e.g. `Development`, `Staging`, `Production`
3. Environment variables
4. User secrets (Development only, when `UserSecretsId` is set)

Nested JSON keys map to environment variables with `__` (double underscore). Example: `Supabase:Url` → `Supabase__Url`.

## Required outside Development

When `ASPNETCORE_ENVIRONMENT` is not `Development`, the API validates at startup:

| Key | Env var | Purpose |
|-----|---------|---------|
| `Supabase:Url` | `Supabase__Url` | Supabase project root URL (same project as the Angular dashboard) |
| `Cors:AllowedOrigins` | `Cors__AllowedOrigins__0`, … | Browser origins allowed to call the API |

## Connection string

| Key | Env var | Purpose |
|-----|---------|---------|
| `ConnectionStrings:ApplyVault` | `ConnectionStrings__ApplyVault` | SQL Server connection for EF Core (required when not using in-memory DB) |

## Database / migrations

| Key | Env var | Purpose |
|-----|---------|---------|
| `Database:MigrateAtStartup` | `Database__MigrateAtStartup` | When `true`, applies EF migrations on API startup. Production template sets `false`; run `dotnet ef database update` in deploy instead. |

Runbook: [DATABASE.md](DATABASE.md).

## Optional integrations

| Section | Key | Env var | When required |
|---------|-----|---------|---------------|
| `GoogleAi` | `Enabled` | `GoogleAi__Enabled` | — |
| `GoogleAi` | `ApiKey` | `GoogleAi__ApiKey` | When `GoogleAi:Enabled` is true |
| `GoogleAi` | `Model` | `GoogleAi__Model` | When `GoogleAi:Enabled` is true |
| `ScrapeResultEnrichment` | `Enabled` | `ScrapeResultEnrichment__Enabled` | — |
| `ScrapeResultEnrichment` | `FailOnAiError` | `ScrapeResultEnrichment__FailOnAiError` | — |
| `Supabase` | `Audience` | `Supabase__Audience` | Defaults to `authenticated` |
| `CalendarIntegration` | `PostConnectRedirectUrl` | `CalendarIntegration__PostConnectRedirectUrl` | OAuth UI return URL when any calendar provider is configured |
| `CalendarIntegration:Google` | `ClientId`, `ClientSecret`, `RedirectUri` | `CalendarIntegration__Google__*` | When Google calendar connect is used |
| `CalendarIntegration:Microsoft` | `ClientId`, `ClientSecret`, `TenantId`, `RedirectUri` | `CalendarIntegration__Microsoft__*` | When Microsoft calendar connect is used |
| `MailIntegration` | `Enabled` | `MailIntegration__Enabled` | — |
| `MailIntegration` | `PostConnectRedirectUrl` | `MailIntegration__PostConnectRedirectUrl` | OAuth UI return URL when `MailIntegration:Enabled` is true |
| `MailIntegration:Gmail` | `ClientId`, `ClientSecret`, `RedirectUri` | `MailIntegration__Gmail__*` | When `MailIntegration:Enabled` is true |
| `MailIntegration` | `PollIntervalSeconds`, etc. | `MailIntegration__*` | Gmail sync tuning |
| `EuresIntegration` | `BaseUrl`, `DefaultLocationCode`, … | `EuresIntegration__*` | EURES job search |

## Test-only

| Key | Env var | Purpose |
|-----|---------|---------|
| `Testing:UseInMemoryDatabase` | `Testing__UseInMemoryDatabase` | Integration tests; use in-memory EF store |
| `Testing:InMemoryDatabaseName` | `Testing__InMemoryDatabaseName` | Isolated in-memory database name |

## Local development setup

1. Copy `api/ApplyVault.Api/appsettings.Development.example.json` to `appsettings.Development.json` (gitignored).
2. Set `Supabase:Url` to your project URL.
3. Store secrets with user secrets:

```bash
cd api/ApplyVault.Api
dotnet user-secrets set "GoogleAi:ApiKey" "<your-key>"
dotnet user-secrets set "CalendarIntegration:Google:ClientSecret" "<secret>"
# ... other OAuth secrets as needed
```

See also `appsettings.example.json` for a full structural template.

OAuth redirect URIs and provider console setup: [OAUTH.md](OAUTH.md). In Staging and Production, configured OAuth URLs must use HTTPS (validated at startup).
