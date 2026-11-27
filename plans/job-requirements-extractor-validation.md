# Job requirements extractor validation

Validated 2026-11-27 against the current coding standards and issue #87.

## Checks

- `dotnet build api/ApplyVault.Api/ApplyVault.Api.sln --configuration Release --no-restore -p:UseSharedCompilation=false`: passed, zero errors. Existing dependency audit warnings remain.
- Focused `JobRequirementsTests`: 30 passed, using SQLite with adapted SQL Server column types and a controlled AI boundary.
- Focused `JobRequirementsPersistenceIntegrationTests`: 2 passed, including HTTP ownership, internal-only persistence, and a concurrent description-save conflict.
- Full unit suite: 412 tests, 398 passed, 14 failed.
- Full HTTP integration suite: 23 tests, 11 passed, 12 failed.
- `dotnet ef migrations has-pending-model-changes --project api/ApplyVault.Api --configuration Release --no-build`: passed, no pending changes.
- Generated the additive SQL Server migration script and inspected its columns and index. Subsequent local deployment applied both pending migrations after explicit approval; see below.
- `git diff --check`: passed.

The .NET test runner needs local socket access, so test runs used the permitted
execution mode outside the restricted sandbox. No test uses a live AI provider.

## Baseline comparison

Built and ran the untouched pre-implementation commit
`40225555b58e6b5c5d4b514dd0e99289a83508b3` in a temporary directory using the
same SDK and cached dependencies. Compared failed test names in TRX results:

| Suite | Baseline | With extractor | New failures |
| --- | --- | --- | --- |
| Unit | 382 total, 14 failed | 412 total, 14 failed | 0 |
| HTTP integration | 21 total, 12 failed | 23 total, 12 failed | 0 |

The failing test-name sets match exactly. Existing failures involve PDF/font
setup, a missing local PDF corpus, CV change-summary expectations, and a live
Jobnet test. These unrelated areas were not changed.

## Standards

The standards review identified one missing-source-hash fallback that could
create an unfinishable processing claim. It was reproduced by a failing test,
then fixed by validating and repairing source state before leasing. Follow-up
review confirmed the fix. No documented-standard or smell findings remain.

## Spec

The independent review against issue #87 found no missing behavior, incorrect
implementation, or scope creep. Representative live provider output review
remains a release check rather than a claim made by these deterministic tests.

Review totals: Standards 0 remaining (1 resolved); Spec 0.

## Practical limits

SQLite verifies relational claims, leases, and stale-result guards. The migration
was subsequently applied to the local Docker SQL Server. Automated Gemini tests
use a fake HTTP handler; a harmless fixture request was also verified with the
configured live provider. Category and importance accuracy still need
representative live output review across professions before release. The adapter
follows the [Gemini generateContent API](https://ai.google.dev/api/generate-content).
The worker runs only when both Job requirements extraction and Google AI are
enabled.

## Local Docker deployment

- Rebuilt API and UI images using the existing local Compose environment file.
- Created a checksum-verified full database backup before migration, stored in
  the SQL volume at `/var/opt/mssql/backup/ApplyVault_before_job_requirements_20261127.bak`
  and copied to `/private/tmp/ApplyVault_before_job_requirements_20261127.bak`.
- With explicit approval, applied `RemoveInterviewPreparation` and
  `AddJobRequirementsExtraction`. Verified both history entries, eight new
  extraction columns, removal of the nine retired tables, and preservation of
  all five saved-job rows. Removed the stopped retired worker container.
- Verified API/database health and an HTTP 200 response from the UI.
- Deployment exposed a Gemini HTTP 400: the provider rejected the fact schema's
  `maxItems: 120` bound because its constraint grammar had too many states.
  Reproduced this with harmless fixture text. Removing only that provider-side
  bound made the same live fixture request succeed. Application validation still
  rejects more than 120 facts; regression tests cover both behaviors.
- Rebuilt and restarted the corrected API. Requeued one failed, empty extraction
  affected by the initial schema rejection. Gemini now returns HTTP 200, but the
  existing posting still fails response validation and follows normal retries.
  This live posting is not yet verified as a successful extraction.
- Automatic approval review rejected a separate manual diagnostic extraction of
  that saved posting. No such diagnostic request was executed; further manual
  provider diagnostics with its content await explicit approval.
