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
| `ConnectionStrings:ApplyVault` | `ConnectionStrings__ApplyVault` | SQL Server connection for EF Core |

## Optional integrations

| Section | Key | Env var | When required |
|---------|-----|---------|---------------|
| `GoogleAi` | `Enabled` | `GoogleAi__Enabled` | — |
| `GoogleAi` | `ApiKey` | `GoogleAi__ApiKey` | When `GoogleAi:Enabled` is true |
| `GoogleAi` | `Model` | `GoogleAi__Model` | When `GoogleAi:Enabled` is true |
| `ScrapeResultEnrichment` | `Enabled` | `ScrapeResultEnrichment__Enabled` | — |
| `ScrapeResultEnrichment` | `FailOnAiError` | `ScrapeResultEnrichment__FailOnAiError` | — |
| `Supabase` | `Audience` | `Supabase__Audience` | Defaults to `authenticated` |
| `CalendarIntegration` | `PostConnectRedirectUrl` | `CalendarIntegration__PostConnectRedirectUrl` | OAuth UI return URL |
| `CalendarIntegration:Google` | `ClientId`, `ClientSecret`, `RedirectUri` | `CalendarIntegration__Google__*` | When using Google calendar |
| `CalendarIntegration:Microsoft` | `ClientId`, `ClientSecret`, `TenantId`, `RedirectUri` | `CalendarIntegration__Microsoft__*` | When using Microsoft calendar |
| `MailIntegration` | `Enabled` | `MailIntegration__Enabled` | — |
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
