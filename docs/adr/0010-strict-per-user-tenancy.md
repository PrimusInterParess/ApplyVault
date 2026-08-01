# ADR-0010: Strict per-user data isolation

## Status

Accepted (production readiness step 2 — completed)

## Context

Legacy scrape ingest allowed `UserId == null` rows. Several services treated **all null-user rows as visible to every authenticated user** (`UserId == userId || UserId == null`), which broke multi-tenant isolation.

## Decision

1. Application data that is user-owned (including scrape/job results and CV documents) is **strictly scoped to the authenticated user**.
2. Remove shared **null-`UserId` visibility pools**; require `userId` on store save contracts.
3. Persist required `UserId` on owned entities; FK delete behavior must not reintroduce orphan sharing (e.g. Restrict rather than SetNull where that enabled the pool).
4. Migrate or delete legacy orphan rows rather than preserving a global shared pool.

## Consequences

- Anonymous or orphan legacy rows are not a product feature; cleanup is part of tenancy hardening.
- New queries must not reintroduce `UserId == null` OR-filters for “shared” reads.
- Plan: `plans/prod-02-tenancy-isolation.md`.
