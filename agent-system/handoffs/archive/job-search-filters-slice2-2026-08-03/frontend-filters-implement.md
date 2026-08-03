# Frontend implement note — job-search filters slice 2 (#6)

## Placement

| Control | Location | Apply |
|---------|----------|-------|
| Published / Schedule | EURES filter row | Compose → Search |
| Sort | EURES results chrome | Immediate (page 1) |
| Per page | Results chrome (both boards) | Immediate (page 1) |

Published options: Any / Last week / Last month only (no “Last 3 months” — Principal lock).

## URL → API

| URL | Body |
|-----|------|
| `sort=BEST_MATCH` (omit `MOST_RECENT`) | `sortSearch` |
| `pageSize=5\|20` (omit `10`) | `resultsPerPage` |
| `published=week\|month` | `publicationPeriod=LAST_WEEK\|LAST_MONTH` |
| `schedule=fulltime\|parttime` | `positionScheduleCodes=["…"]` |

Jobnet URL omits `sort` / `published` / `schedule`.

## Multi-keyword

≥2 keywords → auto `BEST_MATCH`, Most recent disabled, helper “Multiple keywords use best match.”
