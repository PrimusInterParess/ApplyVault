# ADR-0014: Additive EURES public-search filters (sort, period, schedule)

## Status

Accepted (2026-08-03 — operate `job-search-filters-slice2-2026-08-03` / GitHub #6)

## Context

Public job search (`eures-jobnet-search`) already accepted keywords, location, page, `resultsPerPage`, and `sortSearch`, but the UI hardcoded page size and never set sort. The outbound EURES payload modeled `PublicationPeriod` and `PositionScheduleCodes` without forwarding them. Slice 1 (#15) fixed IA/honesty only. Slice 2 needs real discovery power without portal facet parity or a new search service.

## Decision

1. **Extend the existing EURES search request** (no new controller/service). Additive public fields: `publicationPeriod` (`LAST_WEEK` | `LAST_MONTH` | omit) and `positionScheduleCodes` (`fulltime` | `parttime`, capped, allowlisted). Tighten `sortSearch` to `MOST_RECENT` | `BEST_MATCH` (multi-keyword still forces `BEST_MATCH`).
2. **Forward facets** through `BuildSearchPayload`. `PublicationPeriod` is an upstream **string** (or null), not an object.
3. **Ranked-cache key** includes publication period and canonical schedule codes. **Do not** put `page` / `resultsPerPage` in the ranked-cache key (local pagination of the snapshot).
4. **Jobnet:** expose client page size only; no public WorkInDenmark / radius / sort in this decision.
5. **Defer** occupation URIs and contract/offering codes on the public request.
6. **Product adjustment:** “Last 3 months” is not offered — upstream has no accepted period code for it.
7. **URL shareability:** additive query keys `sort`, `pageSize`, `published`, `schedule` (omit defaults; strip EURES-only keys on Jobnet).

## Consequences

- FE can ship wired sort, page size, published, and schedule controls without inventing board capabilities.
- Invalid facet/sort values return validation errors (field-keyed `ValidationProblem`).
- Cache fragmentation grows with facet combinations; acceptable for small allowlists.
- Dirty compose risk remains: Published/Schedule update facade signals immediately, so Sort/Per page re-search can apply unsaved facet edits — documented limitation, follow-up polish if seekers are surprised.
- Supersedes the prior “filters are models only / null PublicationPeriod” behavior for the accepted allowlists.
