---
name: Extension Connection Later
overview: Defer extension-to-user linking until after core app authentication is in place. Use a pairing flow where the logged-in web app issues a short-lived one-time code that the browser extension redeems for a bearer token tied to the authenticated user.
todos: []
isProject: false
---

# Extension Connection Later

## Goal

After app authentication is working, connect the browser extension to the authenticated app user without making the extension invent or send its own raw `UserId`.

## Recommended Approach

Use an extension pairing flow:

- user logs into the web app first
- user opens `Connect Extension` in the app
- backend creates a short-lived one-time pairing code for the current authenticated user
- extension redeems the pairing code
- backend returns an extension session or bearer token
- extension sends authenticated API requests with that token
- backend resolves the user from token claims and saves data under that user

## Why This Approach

- simpler than full OAuth directly inside the extension
- better UX than manual API keys
- more secure than trusting user IDs sent from the extension
- works cleanly with Supabase-based app auth

## Backend Pieces Needed Later

- `User` table in app DB
- `ScrapeResult.UserId`
- `ExtensionPairingSession` table or equivalent
- endpoint to create pairing code for current logged-in user
- endpoint to redeem pairing code from extension
- bearer-token validation for extension API requests

## Extension Flow Later

1. User signs into the web app.
2. User starts extension pairing from the web UI.
3. Extension receives or enters a one-time code.
4. Extension redeems the code.
5. Extension stores returned token securely.
6. Extension includes token on scrape/save API requests.

## Security Rules

- never trust a raw `userId` from the extension
- pairing codes should be short-lived
- pairing codes should be one-time-use
- derive the current user only from validated token claims
- support revoke/disconnect later

## Dependency On Current Work

Do this only after Supabase auth is working end-to-end for the web app and API.

## Next Phase After Current Work

Once Supabase auth is implemented, convert this plan into:

- concrete DB schema
- specific API endpoints
- extension token storage strategy
- exact browser extension onboarding flow

