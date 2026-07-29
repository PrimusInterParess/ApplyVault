# Integration Policy — ApplyVault

## Contract boundaries

- Extension and Angular clients talk to the API with Bearer JWT; do not bypass auth.
- Scrape ingest and CV/document APIs must preserve per-user tenancy (`SupabaseUserId`).
- Shared catalog (`shared/cv-section-catalog`) is the schema source of truth for structured CV.
- AI clients must keep untrusted content boundaries; validate structured outputs before persistence.

## Cross-agent integration

1. Specialists return handoffs with paths touched, contracts affected, and evidence.
2. `principal-software-architect` performs integration review before declaring a milestone complete.
3. Breaking API/DTO/catalog changes require coordinated frontend/extension follow-up tasks.
4. Platform changes (CI, Redis, storage provider) require backend awareness and QA note on residual risk.

## Environments

- Do not invent hosting providers. Use verified config key names only.
- Local defaults may reference `localhost:5173/api` and `localhost:4200` as documented in project READMEs.
