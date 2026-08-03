# Architecture design — Job search filters slice 2 (#6)

**Task:** `job-search-filters-slice2-2026-08-03`  
**Agent:** `architecture-engineer`  
**Mode:** Discuss/design only (BRIDGE) — no `api/` / `frontend/` feature code  
**Issue:** [#6](https://github.com/PrimusInterParess/ApplyVault/issues/6)  
**Contract:** `eures-jobnet-search` (`APPROVED_EXISTING`)

---

## Architecture design summary

- **Request:** redesign / contract extension (discovery-power filters)
- **Status:** PARTIAL — recommended target locked for preferred product path; open decisions remain on final facet allowlist + `PublicationPeriod` upstream encoding
- **Recommendation:** Extend existing `EuresJobSearchRequest` + `BuildSearchPayload` + ranked-cache key; ship **sort + pageSize + publication period + schedule/contract**; **defer occupation URIs**; Jobnet gets pageSize (+ optional later knobs only if product asks)

---

## 1. Current-state evidence

### Public EURES request (already on the wire model)

`api/ApplyVault.Api/Models/EuresContracts.cs` — `EuresJobSearchRequest` already has:

| Field | Default | Notes |
|-------|---------|--------|
| `keywords` / `keyword` | — | required after normalize |
| `locationCode` | options default `dk` | |
| `page` | 1 | client page over **ranked cache**, not upstream page |
| `resultsPerPage` | 20 | clamped by `EuresIntegrationOptions.MaxResultsPerPage` (50) |
| `requestLanguage` | `en` | |
| `sortSearch` | `MOST_RECENT` | forced `BEST_MATCH` when >1 keyword |

No publication / schedule / contract / occupation fields on the public request.

### Outbound EURES payload (modeled, mostly unused)

`EuresSearchPayload` (`Services/Eures/EuresApiModels.cs`) already includes:

- `PublicationPeriod` (`object?`)
- `OccupationUris`, `SkillUris`, `RequiredExperienceCodes`
- `PositionScheduleCodes`, `SectorCodes`, `EducationAndQualificationLevelCodes`
- `PositionOfferingCodes`, `EuresFlagCodes`, `OtherBenefitsCodes`, `RequiredLanguages`
- `MinNumberPost`

`EuresApiClient.BuildSearchPayload` only fills keywords, location, page/size, sort, session, language — and **hard-sets `PublicationPeriod = null`**. Facet arrays stay at type defaults (empty).

### Normalization / sort behavior

`EuresJobSearchRequestNormalizer`:

- Requires ≥1 keyword
- Clamps `resultsPerPage` to `[1, MaxResultsPerPage]`
- Multi-keyword → `SortSearch = "BEST_MATCH"` (overrides client)
- Single-keyword empty sort → `MOST_RECENT`

`EuresJobSearchService.ResolveSortSearch` repeats the multi-keyword force. **No allowlist** for unknown sort strings today.

### Cache / pagination (critical)

Ranked snapshot is cached by:

```
keywords | expandedTerms | location | sort | requestLanguage
```

(`EuresJobSearchService.BuildCacheKey`)

Then **local** `PaginateResults(page, resultsPerPage)`. Implications:

- `resultsPerPage` / `page` **must not** be in the ranked-cache key (already correct)
- Any new facet that changes upstream result sets **must** join the cache key
- Upstream scan uses `MaxResultsPerPage` / `MaxUpstreamScanPages` / `MaxCachedRankedResults` — client page size only slices the cached ranked list

### Jobnet (optional knobs)

`JobnetJobSearchRequest`: keywords, page, resultsPerPage, requestLanguage — **no** public sort/filter facets.

Server options (`JobnetIntegrationOptions`): `WorkInDenmarkOnly` (default true), `DefaultKmRadius`, `DefaultOrderType` — used inside `JobnetApiClient` / search service; **not** request fields. Cache key: `searchString | wid|all | language`.

### Frontend

| Concern | Evidence |
|---------|----------|
| Page size hardcoded | `EURES_RESULTS_PER_PAGE = 5`, `JOBNET_RESULTS_PER_PAGE = 5` |
| Sort never sent | `EuresJobsFacade.buildSearchRequest` omits `sortSearch` even though FE model has it |
| URL state | `JOB_SEARCH_URL_QUERY_KEYS`: `source`, `keywords`, `country`, `location`, `selected` — no sort/pageSize/facets |
| Filter model stub | `job-search-filters.model.ts` only has suggestion-group passthrough |
| Slice 1 | Archive `job-search-ux-redesign-2026-08-03`: IA honesty shipped under #15; sort/filter chrome deferred to #6 |

### Contract registry

`eures-jobnet-search` = `APPROVED_EXISTING`, owner `backend-engineer`. Additive public fields = **proposed** delta until human accepts.

---

## 2. Recommended target seam

**Primary seam (minimal):** extend the existing EURES search pipeline end-to-end.

```
UI controls
  → JobSearchPage / EuresJobsFacade signals
  → URL query sync (job-search-url-state)
  → POST /api/eures/jobs/search (EuresJobSearchRequest)
  → EuresJobSearchRequestNormalizer (allowlists + clamps)
  → EuresJobSearchService (cache key includes facets; pageSize still local)
  → EuresApiClient.BuildSearchPayload (forward facets)
  → upstream EURES search
```

**Do not** add new services, controllers, or a separate “filters API”. Prefer extending:

1. `EuresJobSearchRequest` (public contract)
2. `IEuresJobSearchRequestNormalizer` / normalizer
3. `EuresApiClient.BuildSearchPayload` signature
4. `EuresJobSearchService.BuildCacheKey` (+ payload construction call sites)
5. FE: `eures-job.model.ts`, facade, `job-search-url-state.utils.ts`, page controls (after BE)

**Jobnet seam (this slice):** expose **client `resultsPerPage`** control only (request field already exists). Defer public `workInDenmark` / `kmRadius` / sort unless product explicitly opts in — those are options-backed today; flipping them per-request needs cache-key + payload changes and risks contradicting #15 “Work in Denmark” honesty copy.

---

## 3. Proposed vs approved contract fields

### Approved today (EURES search body — keep)

- `keywords` / `keyword`
- `locationCode`
- `page`, `resultsPerPage`
- `requestLanguage`
- `sortSearch` (behavior already partially enforced server-side)

### Proposed additive (EURES) — `ARCHITECT_PROPOSED` until accepted

| Field | Type (public) | Upstream mapping | Ship in preferred path? |
|-------|---------------|------------------|-------------------------|
| `publicationPeriod` | string enum preset | `EuresSearchPayload.PublicationPeriod` | **Yes** |
| `positionScheduleCodes` | `string[]` (0–N, capped) | `PositionScheduleCodes` | **Yes** |
| `positionOfferingCodes` | `string[]` (0–N, capped) | `PositionOfferingCodes` | **Yes** |
| `occupationUris` | `string[]` | `OccupationUris` | **No — defer** (catalog cost) |

### Sort / pageSize (already approved shape; behavior tightening proposed)

| Field | Proposed rules |
|-------|----------------|
| `sortSearch` | Allowlist: `MOST_RECENT`, `BEST_MATCH` (reject/400 unknown). Multi-keyword still forces `BEST_MATCH` (document in API validation message / UX disable). |
| `resultsPerPage` | Keep clamp; document FE presets e.g. `5 \| 10 \| 20` within server max. |

### Jobnet proposed (optional, product-gated)

| Field | Status |
|-------|--------|
| `resultsPerPage` | Already approved — FE should stop hardcoding |
| `workInDenmark`, `kmRadius`, public sort | **Out of preferred path**; only if product explicitly expands #6 |

### Response contract

**No change** to `EuresJobSearchResponse` / listing DTOs required for filters. Detail already surfaces `contractType` / `workHours` from profile codes — listing honesty remains #15.

---

## 4. Validation sketch

Normalize in `EuresJobSearchRequestNormalizer` (single gate before search):

1. **Existing:** ≥1 keyword; location default; page ≥ 1; resultsPerPage clamp.
2. **`sortSearch`:** trim → uppercase; allowlist `{MOST_RECENT, BEST_MATCH}`; else validation failure.
3. **Multi-keyword:** keep force `BEST_MATCH`; optionally ignore client sort (current) — FE should disable sort control when >1 keyword.
4. **`publicationPeriod`:** null/omit = no filter; else allowlist of presets (see open question on wire value). Reject unknown → 400.
5. **`positionScheduleCodes` / `positionOfferingCodes`:**
   - optional arrays; empty = omit / empty upstream
   - distinct case-insensitive; max length e.g. 5 each
   - allowlist from known EURES codes (seed from detail mapping evidence: schedule `FULLTIME` (+ likely `PARTTIME` etc.); offering `PERMANENT` (+ likely `TEMPORARY` / others)
   - **UNDECIDED exact full code lists** — implementer must confirm against EURES docs or a read-only probe before freezing allowlist constants (server-owned, FE mirrors for controls)
6. **Never invent** sector / skill / education / euresFlag in this slice.

Error shape: existing `ValidationProblem` from controller.

---

## 5. Cache / pagination impact

| Change | Cache key? | Why |
|--------|------------|-----|
| `sortSearch` | Already included | Upstream order + merge behavior |
| `publicationPeriod` | **Add** | Changes upstream JV set |
| `positionScheduleCodes` | **Add** (canonical sorted join) | Same |
| `positionOfferingCodes` | **Add** (canonical sorted join) | Same |
| `occupationUris` (if ever) | Would add | Same — reason to keep deferred until catalog ready |
| `page`, `resultsPerPage` | **Do not add** | Local slice of ranked snapshot |
| `requestLanguage`, location, keywords | Already included | |

Also: `sessionId` is derived from cache key hash — facet changes naturally get a new session id. Good.

Risk: cache fragmentation grows with facet combinations — acceptable for small allowlists; monitor Redis/memory if multi-select expands later.

---

## 6. FE facade / URL sync shape

### Signals (EURES facade)

Add alongside existing `keywords` / `locationCode` / `page` / `resultsPerPage`:

- `sortSearch` (default `MOST_RECENT`)
- `publicationPeriod` (`null` \| preset)
- `positionScheduleCodes` / `positionOfferingCodes` (arrays)

`buildSearchRequest` must send all of the above. Changing any filter/sort/pageSize resets `page` to 1 and clears append/load-more state (same as keyword change).

### URL query keys (extend `JOB_SEARCH_URL_QUERY_KEYS`)

Proposed additive keys (EURES-relevant; ignored or stripped for Jobnet):

| Key | Example | Notes |
|-----|---------|--------|
| `sort` | `MOST_RECENT` | omit when default |
| `pageSize` | `10` | omit when default `5` |
| `published` | preset code | omit when null |
| `schedule` | comma-joined | omit when empty |
| `contract` | comma-joined | omit when empty |

Keep `source`, `keywords`, `country`, `selected`. Do **not** put occupation in URL this slice.

Jobnet: sync `pageSize` only (shared key OK if both facades read it).

### UI honesty

- Controls appear **only** for wired fields (aligns #6 / #15: no dead chrome).
- Multi-keyword: disable or hide sort; show short helper (“Best match when using multiple keywords”).
- Occupation: no control until a catalog endpoint or static curated list exists.

---

## 7. BE → FE sequencing

1. **Product / Principal:** accept filter set + allowlists (+ PublicationPeriod encoding). `/to-spec` on #6 if still `needs-triage`.
2. **backend-engineer:** public request fields → normalizer allowlists → `BuildSearchPayload` forward → cache key → validation messages. No FE yet.
3. **frontend-engineer:** models + API body + facade signals + URL sync + sort/pageSize/facet controls on job-search page (EURES); pageSize for Jobnet.
4. **ui-ux-designer (light):** place controls in post-#15 filter section (one job: refine discovery) — avoid reopening IA rewrite.
5. **qa-engineer:** empty/invalid combos, multi-keyword sort force, cache isolation (different facets → different results), URL round-trip.

Ownership: contract/impl BE primary; FE owns chrome/URL; registry remains `eures-jobnet-search` → `backend-engineer`.

---

## 8. Options considered

### Recommended — A: Additive fields on existing request + payload forward

Smallest BRIDGE change; matches issue acceptance (“sort + one of publication / schedule-contract / occupation”); works if product ships publication + schedule/contract and defers occupation.

### Rejected — B: New `/api/eures/facets` + occupation catalog first

High cost (ESCO/EURES URI catalog, search UX, caching). Blocks slice 2 for little discovery-power gain vs schedule/contract/recency. Revisit as follow-on issue.

### Rejected — C: Client-only re-filter of cached listings

Does not match upstream facets; would lie about totals/`upstreamTotalResults`. Violates “no invented board capabilities.”

### Rejected — D: Federated shared filter DTO across EURES+Jobnet

#17 non-goal. Sources differ; keep source-specific fields on each request model.

### Rejected — E: Expose all `EuresSearchPayload` arrays in one PR

Portal parity; validation/catalog explosion; out of #6 non-goals.

---

## 9. Occupation URI catalog cost (explicit)

Deferring `occupationUris` because:

- Values are **URI identifiers**, not free text — need curated picker or typeahead against EURES/ESCO
- No existing ApplyVault catalog endpoint or static allowlist in-repo
- Wrong URIs silently empty results (bad UX)
- Cache key + URL encoding complexity without product-ready IA

When revisited: separate ticket for catalog source + picker + cache key; do not block preferred path.

---

## 10. ADR need

**Yes — recommend short ADR after human acceptance** (Principal writes numbered file under `docs/adr/`; do not write now).

Proposed ADR title (text only):

> **ADR-00XX — Public EURES search filter allowlist (additive)**
>
> **Status:** Proposed  
> **Context:** #6 discovery-power; outbound payload already modeled unused facets; public API only exposed keywords/location/sort/page.  
> **Decision:** Extend `EuresJobSearchRequest` with allowlisted `publicationPeriod`, `positionScheduleCodes`, `positionOfferingCodes`; tighten `sortSearch` allowlist; include facets in ranked-cache key; defer `occupationUris` until a catalog exists. Client `page`/`resultsPerPage` remain local pagination over ranked cache.  
> **Consequences:** Additive JSON fields (backward compatible); FE must sync URL; multi-keyword continues to force `BEST_MATCH`; Jobnet per-request geography knobs stay options-only unless separately approved.

No ADR for Jobnet pageSize-only FE wiring.

---

## 11. Risks and open decisions

1. **`PublicationPeriod` upstream encoding UNDECIDED** — typed `object?`, always null in-repo. Need confirmed preset → JSON shape (string code vs `{ startDate, endDate }` vs board enum). **Blocks BE implement until known.**
2. **Exact schedule/contract code allowlists** — seed from mapper tests (`FULLTIME`, `PERMANENT`); full set needs EURES confirmation.
3. **Product filter set** — PM parallel; this design assumes publication + schedule + contract; if product drops one, omit that field from contract delta.
4. **Sort vocabulary** — only `MOST_RECENT` / `BEST_MATCH` evidenced; other EURES sort tokens may exist but are out of scope until verified.
5. **Multi-keyword vs user sort** — keep server force; document in UX.
6. **Jobnet optional knobs** — exposing `WorkInDenmarkOnly=false` increases classification fetch cost (`MaxClassificationDetailFetches`); treat as separate decision.

---

## 12. Next actions for implementers (after acceptance)

- BE: implement proposed fields + cache key + payload; document allowlists next to normalizer (constants class OK).
- FE: wire sort + pageSize + accepted facets; URL sync; no occupation chrome.
- Principal: write ADR; update `contract-registry` notes / proposed→approved when accepted; `/to-spec` #6 if still needed.
- Do not touch extension/ or CV catalog.

---

## Impacted modules (path index)

- `api/ApplyVault.Api/Models/EuresContracts.cs`
- `api/ApplyVault.Api/Services/Eures/EuresJobSearchRequestNormalizer.cs`
- `api/ApplyVault.Api/Services/Eures/EuresApiClient.cs` (`BuildSearchPayload`)
- `api/ApplyVault.Api/Services/Eures/EuresApiModels.cs` (typed `PublicationPeriod` if shape known)
- `api/ApplyVault.Api/Services/Eures/EuresJobSearchService.cs` (cache key + call sites)
- `frontend/.../job-search/models/eures-job.model.ts`
- `frontend/.../job-search/data-access/eures-jobs.facade.ts` (+ Jobnet facade pageSize)
- `frontend/.../job-search/utils/job-search-url-state.utils.ts`
- `frontend/.../job-search/pages/job-search-page/*` (controls)
- `agent-system/governance/contract-registry.yaml` (status note after accept)
