# ADR-0050: Durable internal Job requirements extraction

## Status

Accepted (2026-11-27), implementing [issue #87](https://github.com/PrimusInterParess/ApplyVault/issues/87).

## Context

Future interview preparation needs explicit posting facts backed by source quotes.
Saving a job must remain independent of provider availability, while description
changes must immediately invalidate old facts. An in-memory queue could lose work
on restart, and a separate broker would add deployment infrastructure and a
second write that would need coordination with saved-job persistence.

## Decision

Store source revision, SHA-256 fingerprint, typed workflow state, result JSON,
attempt count, retry time, and processing lease on the saved-job row. New saves
and effective-description changes synchronize these fields in the same database
write. A revision concurrency token prevents an older save from replacing newer
description state. No public response contract includes extraction fields.

A bounded polling worker uses conditional database updates to acquire leases and
publish results. Publication checks job ownership, deletion, source revision and
fingerprint, lease identity, and lease expiry. Interrupted work becomes eligible
after its lease expires without spending a failure attempt. Backfill initializes
only legacy rows; it never resets exhausted or completed work.

The feature owns an internal reader that returns current successful requirements
or a processing/unavailable state. Gemini has a dedicated structured operation;
capture repair, cover letters, and the Structured CV remain separate inputs and
operations. Fact values are source excerpts, and evidence must match the posting
after whitespace normalization. Semantic category and importance accuracy still
need representative live-output evaluation.

## Consequences

- No broker is required; work survives process restarts and multiple API instances.
- Queue and result persistence evolve with saved-job storage. Existing jobs are
  backfilled gradually without a large deployment-time provider burst.
- Both `JobRequirements:Enabled` and `GoogleAi:Enabled` must be true for automatic
  processing. Disabled processing leaves durable work pending for later.
- Read results and lease checks prevent stale requirements from becoming current;
  future consumers must use the feature's reader rather than query raw JSON.
- This supplies input for a future design and does not reopen the retired
  preparation feature in ADR-0049.
