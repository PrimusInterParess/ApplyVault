# Backend implement note — EURES filters slice 2 (#6)

## Public API → upstream

| Public field | Allowlist / omit | Upstream JSON |
|--------------|------------------|---------------|
| `sortSearch` | `MOST_RECENT` \| `BEST_MATCH` (multi-keyword forced `BEST_MATCH`) | `sortSearch` string |
| `resultsPerPage` | clamped `[1, MaxResultsPerPage]` (options default max 50) | used for **local** ranked pagination only |
| `publicationPeriod` | omit/null = any; `LAST_WEEK` \| `LAST_MONTH` | `publicationPeriod` string or `null` |
| `positionScheduleCodes` | omit/null/[] = any; `fulltime`, `parttime` (max 5, deduped, sorted) | `positionScheduleCodes` string[] |

Do **not** send `positionOfferingCodes` / `occupationUris` on the public request (human deferred).

## FE URL ↔ API mapping

| URL key | URL values | Request body |
|---------|------------|--------------|
| `sort` | `MOST_RECENT` (default, omit) · `BEST_MATCH` | `sortSearch` |
| `pageSize` | `5` · `10` (default) · `20` | `resultsPerPage` |
| `published` | omit = any · `week` · `month` | omit · `LAST_WEEK` · `LAST_MONTH` |
| `schedule` | omit = any · `fulltime` · `parttime` | omit · `["fulltime"]` · `["parttime"]` |

### Labels (UI)

| Control | Label | API code |
|---------|-------|----------|
| Publication | Any | omit |
| Publication | Last week | `LAST_WEEK` (`published=week`) |
| Publication | Last month | `LAST_MONTH` (`published=month`) |
| Publication | Last 3 months | **not available** — upstream rejects; do not invent |
| Schedule | Any | omit |
| Schedule | Full-time | `fulltime` |
| Schedule | Part-time | `parttime` |

## Cache

Ranked-cache key includes `publicationPeriod` + canonical sorted schedule codes. Does **not** include `page` / `resultsPerPage`.
