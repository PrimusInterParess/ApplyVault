# Production readiness plans (steps 4–17)

Step-by-step implementation plans for hosting ApplyVault beyond local development.

| Step | Plan | Phase |
|------|------|-------|
| 1–3 | [`../prod-01-scrape-ingest-auth.md`](../prod-01-scrape-ingest-auth.md) … [`../prod-03-api-integration-tests.md`](../prod-03-api-integration-tests.md) | A Security (done) |
| 4 | [prod-04-api-environment-configuration.md](prod-04-api-environment-configuration.md) · [ENV.md](ENV.md) | B Config (done) |
| 5 | [prod-05-database-and-migrations.md](prod-05-database-and-migrations.md) · [DATABASE.md](DATABASE.md) | B Config (done) |
| 6 | [prod-06-ci-pipeline.md](prod-06-ci-pipeline.md) | B Config (done) |
| 7 | [prod-07-deployment-and-hosting.md](prod-07-deployment-and-hosting.md) | C Staging (done) |
| 8 | [prod-08-frontend-environment-builds.md](prod-08-frontend-environment-builds.md) · [FRONTEND.md](FRONTEND.md) | C Staging (done) |
| 9 | [prod-09-extension-production-config.md](prod-09-extension-production-config.md) · [EXTENSION.md](EXTENSION.md) | C Staging (done) |
| 10 | [prod-10-oauth-redirects-and-secrets.md](prod-10-oauth-redirects-and-secrets.md) · [OAUTH.md](OAUTH.md) | D Hardening (done) |
| 11 | [prod-11-cors-and-transport-security.md](prod-11-cors-and-transport-security.md) | D Hardening (done) |
| 12 | [prod-12-health-checks-and-readiness.md](prod-12-health-checks-and-readiness.md) | D Hardening (done) |
| 13 | [prod-13-logging-and-monitoring.md](prod-13-logging-and-monitoring.md) | D Hardening (done) |
| 14 | [prod-14-rate-limiting.md](prod-14-rate-limiting.md) | D Hardening (done) |
| 15 | [prod-15-frontend-critical-path-tests.md](prod-15-frontend-critical-path-tests.md) | E Quality (done) |
| 16 | [prod-16-eures-cache-multi-instance.md](prod-16-eures-cache-multi-instance.md) | E Scale (done) |
| 17 | [prod-17-gmail-sync-multi-instance.md](prod-17-gmail-sync-multi-instance.md) | E Scale (done) |

Master tracker: [../production-readiness-tracker.md](../production-readiness-tracker.md)

Implement in numeric order unless the tracker marks a step as deferrable (16–17 when running a single API replica).
