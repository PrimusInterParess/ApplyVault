# ApplyVault API — OAuth redirects and secrets

Implements [prod-10-oauth-redirects-and-secrets.md](prod-10-oauth-redirects-and-secrets.md).

OAuth callbacks hit the **API host** (HTTPS). After connect, users return to the **dashboard host** (Angular routes).

## URL patterns

Replace placeholders with your domains:

| Purpose | Example |
|---------|---------|
| API base | `https://api.example.com` |
| Dashboard base | `https://app.example.com` |

| Provider | Authorized redirect URI (register in provider console) |
|----------|--------------------------------------------------------|
| Google Calendar | `https://api.example.com/api/calendar-connections/google/callback` |
| Microsoft Calendar | `https://api.example.com/api/calendar-connections/microsoft/callback` |
| Gmail | `https://api.example.com/api/mail-connections/gmail/callback` |

| Post-connect redirect (API config, not provider console) | Value |
|----------------------------------------------------------|-------|
| Calendar | `https://app.example.com/integrations/calendar/callback` |
| Mail | `https://app.example.com/integrations/mail/callback` |

The dashboard sends `returnUrl` from `window.location.origin` when starting connect; `PostConnectRedirectUrl` is the fallback when that value is missing.

## Environment variables

Set in `deploy/.env` (or host config). Never commit secrets.

```bash
# Calendar — set only for providers you enable
CalendarIntegration__PostConnectRedirectUrl=https://app.example.com/integrations/calendar/callback
CalendarIntegration__Google__ClientId=
CalendarIntegration__Google__ClientSecret=
CalendarIntegration__Google__RedirectUri=https://api.example.com/api/calendar-connections/google/callback
CalendarIntegration__Microsoft__ClientId=
CalendarIntegration__Microsoft__ClientSecret=
CalendarIntegration__Microsoft__TenantId=common
CalendarIntegration__Microsoft__RedirectUri=https://api.example.com/api/calendar-connections/microsoft/callback

# Gmail — required when MailIntegration__Enabled=true
MailIntegration__Enabled=false
MailIntegration__PostConnectRedirectUrl=https://app.example.com/integrations/mail/callback
MailIntegration__Gmail__ClientId=
MailIntegration__Gmail__ClientSecret=
MailIntegration__Gmail__RedirectUri=https://api.example.com/api/mail-connections/gmail/callback
```

Staging: use staging API and app hostnames in the same keys with `ASPNETCORE_ENVIRONMENT=Staging`.

## Provider console setup

### Google Calendar

1. [Google Cloud Console](https://console.cloud.google.com/) → APIs & Services → Credentials.
2. Create or select an OAuth 2.0 **Web application** client (separate clients for dev/staging/prod recommended).
3. **Authorized redirect URIs:** add the Google Calendar callback URL above.
4. Enable **Google Calendar API** for the project.
5. Copy Client ID and Client secret into env vars.

### Microsoft Calendar

1. [Microsoft Entra admin center](https://entra.microsoft.com/) → App registrations → New registration.
2. Platform → Web → redirect URI: Microsoft Calendar callback URL above.
3. Certificates & secrets → New client secret.
4. API permissions: `Calendars.ReadWrite`, `offline_access`, `openid`, `profile`, `email` (delegated).
5. Set `CalendarIntegration__Microsoft__TenantId` to `common` (multi-tenant) or your tenant ID.

### Gmail

1. Same Google Cloud project or a dedicated OAuth client.
2. Enable **Gmail API**.
3. Add the Gmail callback URL to authorized redirect URIs.
4. OAuth consent screen: add scopes for Gmail read (and any scopes used by the app).
5. Set `MailIntegration__Enabled=true` only after all Gmail env vars are set.

## Local development

Use `http://localhost:5173` for API callback URIs and `http://localhost:4200` for post-connect URLs (defaults in `appsettings.json`).

Store secrets with user secrets:

```bash
cd api/ApplyVault.Api
dotnet user-secrets set "CalendarIntegration:Google:ClientSecret" "<secret>"
dotnet user-secrets set "MailIntegration:Gmail:ClientSecret" "<secret>"
```

See [ENV.md](ENV.md) for the full configuration reference.

## Verification

1. API starts in Production/Staging with no OAuth secrets in git.
2. From dashboard **Settings**, connect Google calendar → browser returns to `/integrations/calendar/callback?provider=google&success=true`.
3. Repeat for Microsoft and Gmail if configured.
4. Provider console shows no `redirect_uri_mismatch` errors.

Callbacks remain anonymous on `CalendarConnectionsController` and `MailConnectionsController` (`[AllowAnonymous]` on `GET {provider}/callback`).

## Security notes

- Use separate OAuth clients per environment to limit blast radius.
- Rotate secrets if they were ever committed; check git history.
- Production and Staging require **HTTPS** redirect and post-connect URLs (validated at startup).
