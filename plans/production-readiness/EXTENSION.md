# ApplyVault Chrome extension — production builds

Implements [prod-09-extension-production-config.md](prod-09-extension-production-config.md).

## Environment files

| File | Used when |
|------|-----------|
| `extension/src/environments/environment.ts` | `npm run build`, `npm run watch` (local API) |
| `extension/src/environments/environment.staging.ts` | `npm run build:staging` |
| `extension/src/environments/environment.production.ts` | `npm run build:production` |

Before a hosted build, edit the staging or production file (or copy values from your deploy host):

- `apiBaseUrl` — `https://<API_DOMAIN>/api` (same host as [deploy/.env.example](../../deploy/.env.example) `API_DOMAIN`)
- `supabase.url` — must match API `Supabase__Url` and the dashboard (project root, not `/auth/v1`)
- `supabase.anonKey` — Supabase **anon** / publishable key (public in the bundle; never use service role)

Also update the matching manifest so `host_permissions` include the API origin and Supabase project URL:

| Manifest | Used when |
|----------|-----------|
| `extension/manifest.json` | development (`localhost:5173` + local Supabase) |
| `extension/manifest.staging.json` | `npm run build:staging` |
| `extension/manifest.production.json` | `npm run build:production` (no localhost) |

Extension, dashboard, and API must share the same Supabase project. Email OTP template must include `{{ .Token }}` (see root [README.md](../../README.md)).

## Build commands

From the extension package:

```bash
cd extension
npm ci
npm run build              # extension/dist/ — local API (localhost:5173)
npm run build:staging      # extension/dist/ — staging API + manifest.staging.json
npm run build:production   # extension/dist/ — production API + manifest.production.json
```

`aspNetApiClient.ts` and `supabaseAuth.ts` read URLs from `apiConfig.ts` / `supabaseConfig.ts`, which resolve the active environment file at build time via `extension/scripts/build.mjs`.

## Load unpacked (dev / internal)

1. Run `npm run build` (or `npm run watch`) from `extension/`.
2. Open `chrome://extensions`, enable **Developer mode**, click **Load unpacked**, select the `extension/dist/` folder.

## Release / store publish

1. Set `extension/src/environments/environment.production.ts` and `extension/manifest.production.json` to your real API and Supabase origins.
2. Bump `version` in `extension/package.json` and the manifest used for the release build.
3. Run `npm run build:production` from `extension/`.
4. Zip the contents of `extension/dist/` (not the folder itself) for [Chrome Web Store](https://chrome.google.com/webstore/devconsole) upload, or distribute the zip to testers.

### Chrome Web Store checklist (public listing)

- [ ] Privacy policy URL (extension handles user data and auth)
- [ ] 128×128 icon (`assets/applyvault-icon.png`) and store screenshots
- [ ] Single purpose description aligned with job capture + ApplyVault save
- [ ] `host_permissions` limited to supported job boards + your API + Supabase (no localhost)
- [ ] Test sign-in (email OTP) and save on a supported job page against production API
- [ ] Confirm unsigned-out save shows a clear error

Consider a separate unpacked **dev** extension ID (development build) vs store **prod** ID for internal testing.

## Verification

1. After `npm run build:production`, inspect bundled `extension/dist/background/background.js` — API URL should be your HTTPS origin, not localhost.
2. Load `extension/dist/` in Chrome, sign in with the same Supabase account as the dashboard.
3. Scrape a job page and save; `POST /api/scrape-results` should return **201** and the job appears in the dashboard for that user.
4. Sign out and confirm save is blocked with a clear message.

## Related

- Dashboard builds: [FRONTEND.md](FRONTEND.md)
- Next step: [prod-10-oauth-redirects-and-secrets.md](prod-10-oauth-redirects-and-secrets.md)
