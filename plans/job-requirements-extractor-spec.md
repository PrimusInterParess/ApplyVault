# Automatically extract Job requirements from saved postings

## Problem Statement

Saved job postings contain useful interview preparation context in unstructured
descriptions. Future preparation needs reliable, source-backed facts about a role,
without treating inferred interview topics as employer requirements or making
seekers extract those facts themselves.

## Solution

Automatically extract and store internal Job requirements through a background
worker after a job is saved or its description changes. Process existing saved
jobs gradually. Every fact includes a supporting quote, and only results matching
the current saved description are available to future preparation. No extraction
interface is added.

## User Stories

1. As a seeker, I want my saved posting processed automatically, so that future interview preparation has job context without another action.
2. As a seeker, I want existing saved postings processed too, so that this context is available for jobs saved before launch.
3. As a seeker, I want responsibilities and expected outcomes captured, so that preparation reflects what the role involves.
4. As a seeker, I want skills, tools, and knowledge captured, so that preparation can address the employer's stated needs.
5. As a seeker, I want required and preferred qualifications distinguished, so that preparation can respect their stated importance.
6. As a seeker, I want unspecified importance kept unspecified, so that extraction does not invent priorities.
7. As a seeker, I want experience and seniority captured when stated, so that preparation can reflect the role's expectations.
8. As a seeker, I want education, certifications, and languages captured when stated, so that relevant qualifications are available to preparation.
9. As a seeker, I want team, product, and industry context captured, so that preparation can reflect the working context.
10. As a seeker, I want an explicitly described interview process captured, so that preparation can account for its stated format.
11. As a seeker, I want extracted facts backed by posting quotes, so that preparation can remain grounded in the employer's words.
12. As a seeker, I want absent information left unknown, so that preparation does not rely on invented requirements.
13. As a seeker, I want changes to my saved description processed automatically, so that future preparation uses current information.
14. As a seeker, I want outdated results excluded immediately, so that preparation cannot consume requirements from a superseded description.
15. As a seeker, I want job saving to succeed independently of extraction, so that provider failures do not interrupt saving a job.
16. As a seeker, I want temporary extraction failures retried automatically, so that recovery does not require another action from me.
17. As a seeker, I want a missing description handled without invented content, so that adding the description later enables accurate extraction.
18. As a seeker, I want my saved-job context isolated from other users, so that preparation only consumes my own job data.

## Implementation Decisions

### Agreed product behavior

- Extract six categories: responsibilities and expected outcomes; skills, tools,
  and knowledge; experience and seniority; education, certifications, and
  languages; team, product, or industry context; and explicitly stated interview
  process.
- Every fact retains a short supporting quote from the saved posting.
  Qualifications carry required, preferred, or unspecified importance. Missing
  categories remain empty; absence is not a provider failure.
- Extraction is internal. No requirements UI, direct editing, manual extraction
  action, or public requirements endpoint is introduced.
- Save and description changes trigger automatic background processing. Existing
  saved jobs are processed gradually. Missing or unusable descriptions produce an
  internal description-needed state; a later usable description triggers work.
- Description changes immediately invalidate previous requirements. An older
  in-flight result cannot overwrite or become available as the current result.
- Retry extraction failures a limited number of times, then retain an internal
  failed state. Extraction failures never undo a successful job save.

### Proposed implementation choices

- Add a dedicated extraction module with an internal operation to read current
  requirements for an owned saved job. Keep provider calls, processing state,
  source validation, and persistence behind that boundary.
- Use the existing Google AI HTTP integration conventions for a dedicated
  structured extraction call. Do not repurpose capture-quality repair or alter
  its existing behavior. Send the current effective saved description, including
  a user override, without the Structured CV or live web retrieval.
- Persist versioned structured requirements and durable work state associated
  with the saved job, including source revision/fingerprint, attempts, next retry
  time, and processing lease. Add a database migration without restoring retired
  interview preparation tables.
- Record pending work durably alongside description persistence, so a process
  restart between saving and worker execution does not lose work. Cover every
  save/import route through shared persistence behavior.
- Use bounded worker batches and atomic leases so multiple application instances
  cannot publish competing results. Expired leases allow interrupted work to
  resume. Backfill is resumable and excludes deleted jobs.
- Default to three attempts per source revision with delayed retries and a
  bounded provider timeout. Shutdown cancellation releases or expires work
  without exhausting its retry budget. A changed source starts fresh work;
  exhausted work is not repeatedly requeued by backfill.
- Publish only after validating the response shape and quotes against the source
  text, allowing defined whitespace normalization. Treat the posting as data,
  never as instructions. Unsupported facts or fabricated evidence must not
  become consumable requirements.
- Bind results to a monotonically changing source revision and source
  fingerprint; unchanged content does not cause duplicate extraction. Check
  ownership, deletion, current revision, and processing claim when publishing or
  reading. Description-change invalidation occurs in the same transaction as
  the change. Deletion makes results unavailable and prevents in-flight revival.
- The internal consumer gets current successful requirements or an explicit
  unavailable status (pending, processing, description needed, or failed), never
  a stale fallback. Future preparation decides how to handle unavailability.
- Exact schema placement and configuration names remain implementation choices;
  these lifecycle and consistency guarantees are acceptance requirements.

## Testing Decisions

- Primary test boundary: save or edit a job using the existing
  application/persistence operations, drive a worker iteration, then read current
  requirements through the internal consumer operation. Use a controllable fake
  AI provider. Assert observable behavior rather than private methods, queue
  implementation, or exact prompt wording.
- Reuse the API integration-test host and saved-job tenancy fixtures as prior
  art; supplement them with a database supporting real transactions and atomic
  claims for worker concurrency tests.
- Verify all six categories, required/preferred/unspecified distinctions, missing
  categories, source quotes, malformed output, and unsupported evidence.
- Verify new saves, description overrides, unchanged descriptions, missing
  descriptions later supplied, gradual backfill, retries, exhaustion, and save
  success during provider failure.
- Pause the fake provider while editing or deleting a job; its old result must
  never become available. Confirm old results stay unavailable if replacement
  extraction fails.
- Verify restart recovery, concurrent worker claims, ownership isolation, and
  idempotent backfill. Use focused persistence integration tests where required
  to prove these guarantees.
- Review representative provider outputs for different professions before
  release. Deterministic fake-provider tests prove lifecycle behavior, not the
  semantic accuracy of live extraction.

## Out of Scope

- Building or restoring interview preparation, generating questions, predicting
  interview topics, assessing CV gaps, or changing cover-letter generation.
- Extraction UI, user-visible processing states, direct fact editing, manual
  retry controls, and public requirements APIs.
- Live browsing, re-scraping postings, external enrichment, or supplying missing
  facts from general knowledge.
- Salary, benefits, and application-fit scoring as additional extraction
  categories in this first version.

## Further Notes

- The domain glossary defines Job requirements as explicit posting facts.
- ADR-0049 retired the previous interview preparation implementation; those
  retired designs do not impose requirements on this feature.
- Product scope and the testing approach were confirmed in the design discussion.
