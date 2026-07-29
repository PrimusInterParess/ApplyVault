# Quality Gates — ApplyVault

## Before marking work done

1. Changes respect ownership matrix paths.
2. CV-related changes use `CONTEXT.md` vocabulary and ADR-0001 / catalog — no hardcoded section-type sprawl.
3. API changes include or update unit tests under `api/ApplyVault.Api.Tests` when logic is non-trivial; tenancy-sensitive paths consider integration tests.
4. Angular critical-path changes include/adjust specs where the feature already has them; run targeted checks when authorized.
5. Extension changes include/adjust Vitest specs when extraction/quality logic changes.
6. No secrets in diffs, prompts, or handoffs.
7. Prefer project skills: `/tdd` at agreed seams, then `/code-review`.
8. Do not claim full suite green unless executed or CI evidence is cited.

## Known gaps (do not ignore)

- Extension tests are not in `.github/workflows/api-ci.yml` — call out residual risk when extension behavior changes.
