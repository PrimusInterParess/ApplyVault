---
name: Step 10 — OAuth Redirects and Secrets
overview: Configure production OAuth redirect URIs and store client secrets securely for Google, Microsoft, and Gmail integrations.
todos:
  - id: google-calendar-oauth
    content: Register production redirect URI for Google calendar callback
    status: completed
  - id: microsoft-calendar-oauth
    content: Register production redirect URI for Microsoft calendar callback
    status: completed
  - id: gmail-oauth
    content: Register production redirect URI for Gmail mail callback
    status: completed
  - id: secrets-storage
    content: Store ClientId/ClientSecret via env vars (CalendarIntegration__*, MailIntegration__*)
    status: completed
  - id: post-connect-urls
    content: Set PostConnectRedirectUrl to production Angular routes
    status: completed
  - id: oauth-smoke-test
    content: End-to-end connect flow for each provider in staging
    status: completed
isProject: false
---

# Step 10 — OAuth Redirects and Secrets

**Tracker:** [production-readiness-tracker.md](../production-readiness-tracker.md) · **Prerequisites:** [prod-09-extension-production-config.md](prod-09-extension-production-config.md) · **Next:** [prod-11-cors-and-transport-security.md](prod-11-cors-and-transport-security.md) · **Runbook:** [OAUTH.md](OAUTH.md)

## Problem

Calendar and mail integrations use OAuth with localhost redirect URIs in [`CalendarIntegration`](../../api/ApplyVault.Api/Options/CalendarProviderOptions.cs) and [`MailIntegrationOptions`](../../api/ApplyVault.Api/Options/MailIntegrationOptions.cs):

- `http://localhost:5173/api/calendar-connections/google/callback`
- `http://localhost:5173/api/mail-connections/gmail/callback`
- Post-connect redirects to `http://localhost:4200/...`

Production requires HTTPS callback URLs on the **API host** and post-connect redirects on the **frontend host**.

## Risk

| Risk | Impact |
|------|--------|
| OAuth redirect mismatch | Connect flows fail with redirect_uri_mismatch |
| Secrets in git | Provider account compromise |
| HTTP callbacks in prod | OAuth providers may reject non-HTTPS |

## Goal

Each provider accepts production callback URLs; secrets live only in host env vars.

## Callback routes (unchanged paths, new domain)

| Provider | Callback |
|----------|----------|
| Google Calendar | `GET /api/calendar-connections/google/callback` |
| Microsoft Calendar | `GET /api/calendar-connections/microsoft/callback` |
| Gmail | `GET /api/mail-connections/gmail/callback` |

Example production URL: `https://api.your-domain.com/api/calendar-connections/google/callback`

## Implementation tasks

### 1. Google Cloud Console

- Add authorized redirect URIs for prod (and staging if applicable).
- Create separate OAuth client for prod vs dev if desired.

### 2. Microsoft Entra / Azure AD

- Register redirect URI for Microsoft calendar provider.
- Set tenant as needed (`common` or single tenant).

### 3. Gmail

- Same Google project or dedicated Gmail OAuth client.
- Enable Gmail API; add prod redirect URI.

### 4. API configuration

Set via environment variables (see [OAUTH.md](OAUTH.md) and [`deploy/.env.example`](../../deploy/.env.example)):

```
CalendarIntegration__Google__RedirectUri=https://api.../api/calendar-connections/google/callback
CalendarIntegration__Google__ClientId=...
CalendarIntegration__Google__ClientSecret=...
MailIntegration__Gmail__RedirectUri=https://api.../api/mail-connections/gmail/callback
CalendarIntegration__PostConnectRedirectUrl=https://app.../integrations/calendar/callback
MailIntegration__PostConnectRedirectUrl=https://app.../integrations/mail/callback
```

Startup validation in [`OAuthIntegrationOptionsValidation.cs`](../../api/ApplyVault.Api/Infrastructure/OAuthIntegrationOptionsValidation.cs) requires complete provider config and HTTPS URLs in Staging/Production.

### 5. Keep callbacks anonymous

OAuth callbacks must remain `[AllowAnonymous]` on [`CalendarConnectionsController`](../../api/ApplyVault.Api/Controllers/CalendarConnectionsController.cs) and [`MailConnectionsController`](../../api/ApplyVault.Api/Controllers/MailConnectionsController.cs).

## Verification

1. From staging dashboard settings, connect Google calendar → returns to frontend success route.
2. Repeat for Microsoft and Gmail (if enabled).
3. No OAuth secrets in repository.

## Production-grade notes

- Use separate OAuth clients for dev/staging/prod to limit blast radius.
- Rotate secrets if they were ever committed (check git history).
