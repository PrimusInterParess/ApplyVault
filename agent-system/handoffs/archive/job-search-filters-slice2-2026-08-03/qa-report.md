# Job-search filters slice 2 (#6) — QA acceptance report

**Task:** `job-search-filters-slice2-2026-08-03`  
**Agent:** `qa-engineer`  
**Mode:** Static acceptance review (code + handoffs). No builds, no test authoring, no suite execution.  
**Date:** 2026-08-03  
**Overall status:** **READY** (shippable with documented limitations)

---

## 1. Executive Summary

Static review of API + Angular job-search against #6 AC, UX placement §9, and BE/FE wire maps finds the slice **implemented end-to-end**: allowlisted sort / publication / schedule, ranked-cache facets, EURES filter-row + results-chrome placement, Jobnet Per page only, URL keys with defaults omitted, multi-kw Best match honesty, and no “Last 3 months”. No must-fix defects evidenced. Residual risk: dirty Published/Schedule can ride along on immediate Sort/Per page re-search (severity; medium severity). Automated tests and live browser not run.

---

## 2. Scope Confirmation

| In | Out |
|----|-----|
| `api/ApplyVault.Api` EURES search contract / normalizer / cache / payload | Extension |
| `frontend/.../features/job-search` page, facades, URL/filter utils | `ng`/`dotnet` build/test/install |
| Active handoff notes + #6 AC | Inventing CI green / creating tests |
| UX §9 placement checklist (static) | Manual browser E2E |

---

## 3. Checklist

| # | Check | Result | Evidence |
|---|-------|--------|----------|
| 1 | EURES API accept/forward sort, resultsPerPage, publicationPeriod, schedule with allowlists | **PASS** | Allowlists: `EuresSearchFilterCodes.cs`. Normalizer rejects invalid sort/period/schedule → `ValidationProblem` (`EuresJobSearchRequestNormalizer.cs`, `EuresJobsController.cs`). Forward: `EuresApiClient.BuildSearchPayload` sets `SortSearch`, `PublicationPeriod`, `PositionScheduleCodes`. `resultsPerPage` clamped locally (not upstream fetch size); used in `PaginateResults`. |
| 2 | Cache key includes period/schedule; excludes page/pageSize | **PASS** | `EuresJobSearchService.BuildCacheKey` fingerprints keywords, terms, location, sort, language, publication, schedule — no `page` / `resultsPerPage`. Pagination via `PaginateResults(rankedSnapshot, page, resultsPerPage)`. |
| 3 | EURES UI: Published+Schedule in filter row; Sort+Per page in results chrome; wired | **PASS** | Filter row: `job-search-page.component.html` Published/Schedule under `source === 'eures'`, `(change)` → facade. Results chrome: Sort (EURES) + Per page; `(change)` → `updateSortSearch` / `updateResultsPerPage` → `fetchPage(1)`. Request body includes fields in `eures-jobs.facade.ts` `buildSearchRequest`. |
| 4 | Default pageSize 10; options 5/10/20; Jobnet Per page only | **PASS** | `JOB_SEARCH_DEFAULT_PAGE_SIZE = 10`, `JOB_SEARCH_PAGE_SIZE_OPTIONS = [5,10,20]` (`job-search-filter.utils.ts`). Jobnet: no Sort/Published/Schedule in HTML; board context only; `jobnet-jobs.facade` only `resultsPerPage` among new knobs. |
| 5 | URL keys sort/pageSize/published/schedule; omit defaults; Jobnet strips EURES-only | **PASS** | `buildJobSearchUrlQueryParams` omits default sort/`pageSize=10`; `published`/`schedule`/`sort` null when not EURES (`job-search-url-state.utils.ts`). Invalid URL → `filterInitWarning` messages in `eures-jobs.facade.ts`. |
| 6 | No Last 3 months option | **PASS** | UI options week/month only (`job-search-page.component.html`). API allowlist `LAST_WEEK`/`LAST_MONTH` only. Principal/BE lock: upstream rejects 3-month. |
| 7 | Multi-kw Best match honesty | **PASS** | `isMultiKeywordSortForced` when ≥2 keywords; Most recent disabled; helper “Multiple keywords use best match.”; API forces `BEST_MATCH` (`TryNormalizeSortSearch` / `resolveEuresSortSearch`). |
| 8 | Changing sort/pageSize/facets resets page 1 | **PASS** | Sort/Per page: `page.set(1)` + `fetchPage(1)` when `hasSearched` (`eures-jobs.facade.ts` ~633–658; Jobnet Per page ~327). Published/Schedule: compose until Search (UX A2/§9.6); `search` / `clearPagedResults` reset page to 1 before fetch. |
| 9 | #15 honesty not obviously regressed | **PASS** | One visible results summary (`eures-page__results-summary`); live region `visually-hidden`. Jobnet static “Work in Denmark · Denmark”. No Remote/tags invent in facades (optional model fields; templates gated on truthy; no assignment in data-access mappers). No occupation/contract chrome. |
| 10 | Dirty compose risk (Published/Schedule dirty + Sort/Per page immediate) | **LIMITATION** (not FAIL) | Severity **medium**. Facet selects write signals immediately; Sort/Per page re-search uses current signals (`buildSearchRequest`), so dirty facets apply without Search. Documented in `handoff-frontend-engineer.yaml` residual_risks. Matches UX compose-vs-immediate split; seeker surprise possible. |

---

## 4. UX §9 (placement) — static

| §9 item | Result |
|---------|--------|
| 1 Published+Schedule EURES only | PASS |
| 2 Sort+Per page EURES; Jobnet Per page only | PASS |
| 3 One non-sr-only summary | PASS |
| 4 One filled Search CTA | PASS |
| 5 Multi-kw Best match + helper | PASS |
| 6 Facets require Search | PASS (compose; dirty ride-along on chrome is § risk) |
| 7 Sort/Per page immediate | PASS |
| 8 URL round-trip / Jobnet strip | PASS (static); live browser not run |
| 9 No occupation/contract/WiD/radius | PASS |
| 10 `--app-*` tokens | PASS (no new DS; scss breakpoints only — not deeply audited) |

---

## 5. Verified Facts

- Public contract fields: `SortSearch`, `PublicationPeriod`, `PositionScheduleCodes`, `ResultsPerPage` on `EuresJobSearchRequest`.
- Invalid facet/sort → 400 `ValidationProblem` with field-keyed messages.
- Upstream payload encodes `publicationPeriod` as string|null (not object).
- FE URL ↔ API mapping matches `backend-filters-implement.md`.
- BE + FE handoffs both **READY**.

---

## 6. Assumptions

- Static wiring implies runtime behavior for allowlist/cache/URL; no live EURES/Jobnet probe in this QA turn.
- Human accept’s “3 months” was superseded by Principal lock + upstream 400 (BE residual); ship set is week/month.
- Clamping invalid `resultsPerPage` on API (vs 4xx) is acceptable because FE/URL closed-set already gates seekers.

---

## 7. Decisions

- Verdict **READY** — no blocking AC fails on static evidence.
- Dirty compose flagged as **accepted residual risk** (medium), not BLOCKED.
- Recommend Principal **Close** with limitations; write ADR for additive EURES filter fields at Close.

---

## 8. Deliverables

- This report: `qa-report.md`
- YAML: `handoff-qa-engineer.yaml`

---

## 9. Contracts covered

- `eures-jobnet-search` (public EURES search request + Jobnet pageSize-only client behavior)

---

## 10. Security

- No secrets inspected or invented.
- Server-side allowlists present for sort/period/schedule.
- Auth on EURES controller unchanged (`[Authorize]`).

---

## 11. Validation evidence status

| Layer | Status |
|-------|--------|
| Static API | Reviewed |
| Static Angular | Reviewed |
| Automated tests | **Not run** (task constraint; no-automatic-tests) |
| Browser / manual | **Not run** |
| CI | **Not claimed** |

---

## 12. Risks / gaps

1. **Dirty compose (medium):** Sort/Per page can apply unpublished Published/Schedule values.
2. **Stale specs:** FE handoff notes existing `*.spec.ts` still assert pre-slice shapes — not updated.
3. **No executed tests:** Confidence is static-only until normalizer/URL specs or manual QA.
4. **API `ResultsPerPage` default 20** on contract class vs FE default 10 — FE always sends; low risk.
5. **human-acceptance.yaml** still lists 3 months — product docs drift vs ship set (process nit, not runtime defect).

---

## 13. Handoffs

| To | Ask |
|----|-----|
| Principal | Integration Close; ADR for additive public filter fields; accept dirty-compose limitation or follow-up ticket |
| code-review-engineer | Diff/intent review if PR opened (QA does not own PR findings) |
| (optional) future QA | Manual URL round-trip + dirty-compose scenario; update specs when tests authorized |

---

## 14. Status

**READY** — acceptance checklist PASS with one documented LIMITATION (#10 dirty compose). Recommend Close **with limitations**.
