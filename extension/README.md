# ApplyVault Chrome extension

Manifest V3 extension that scrapes job listing details from the active tab and saves them to the ApplyVault API.

## Commands

```bash
npm install
npm run build              # extension/dist/ — local API
npm run build:staging
npm run build:production
npm run watch              # rebuild on change
npm test
npm run typecheck
```

## Load unpacked

1. Run `npm run build` (or `npm run watch`).
2. Open `chrome://extensions` → **Developer mode** → **Load unpacked** → select `extension/dist/`.

Production builds and Chrome Web Store steps: [`../plans/production-readiness/EXTENSION.md`](../plans/production-readiness/EXTENSION.md).
