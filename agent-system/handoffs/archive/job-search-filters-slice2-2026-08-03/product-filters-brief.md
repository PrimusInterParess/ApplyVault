# Product brief — Public job-search filters (slice 2 / #6)

**Task:** `job-search-filters-slice2-2026-08-03`  
**Agent:** `product-manager`  
**Mode:** Discuss/design only — BRIDGE; prepare for `/to-spec` on #6  
**Contract:** `eures-jobnet-search`  
**Linked:** #6 (this slice) · #15 (IA/honesty — out) · #12 (Jobnet suggestion lists — out)

---

## 1. Executive Summary

Slice 1 (#15) fixed hierarchy and honest chrome. Discovery still feels weak because seekers cannot sort, change page size, or narrow by freshness / work schedule — even though EURES models already carry those knobs outbound and the public request already accepts `SortSearch` + `ResultsPerPage`.

**Recommended ship set for #6:** EURES **sort** + **page size** + **publication period** + **schedule (full/part-time)**; sync those params in the URL; raise default page size from 5 → **10**. **Jobnet knobs out** this slice (server already scopes Work-in-Denmark). Defer occupation URI catalog and contract-type facet. No dead chrome — UI controls ship only when API+URL wired.

Human can accept the defaults in §5 in one pass, then Principal runs `/to-spec` on #6.

---

## 2. Task / Scope Confirmation

| In (#6 slice 2) | Out |
|-----------------|-----|
| Agree EURES filter/sort ship set + validation rules | #15 IA / honest chrome / dead Remote·tags UI |
| Jobnet decision for this slice (in / out / defer) | #12 Jobnet-specific suggestion **lists** |
| URL-state for shipped params only | Full EURES portal facet parity |
| Testable acceptance criteria draft for #6 | AI fit (#14), relevance scoring (#7), federated search (#17) |
| Defaults for human one-pass accept | Saved-jobs Phases 2–4; extension; inventing radius UX |
| Handoff READY for `/to-spec` after human accept | Application code changes in this operate turn |

**Journey:** Search EURES or Work in Denmark → inspect → save (FR-03). Goal remains find → read → save — not apply-in-place and not full portal clone.

---

## 3. Verified Facts

| Fact | Evidence |
|------|----------|
| #6 open, `needs-triage`; AC still need full `/to-spec` | `gh issue view 6` |
| #15 owns IA, Jobnet country honesty, FE dead Remote/tags; defers sort/page-size/API facets to #6 | Issue #15 body; archive summary |
| Public `EuresJobSearchRequest` already has `keywords`, `locationCode`, `page`, `resultsPerPage`, `SortSearch` (default `MOST_RECENT`) | `api/.../Models/EuresContracts.cs` |
| Multi-keyword searches force `BEST_MATCH` in normalizer (overrides user sort) | `EuresJobSearchRequestNormalizer` |
| Outbound `EuresSearchPayload` models `PublicationPeriod`, `OccupationUris`, `PositionScheduleCodes`, `PositionOfferingCodes`, `SectorCodes`, etc.; client hardcodes `PublicationPeriod = null` and empty lists | `EuresApiModels.cs`, `EuresApiClient.BuildSearchPayload` |
| FE hardcodes page size on both boards (constant `*RESULTS_PER_PAGE`); no sort UI | facades under `features/job-search/` |
| Jobnet public request: keywords + page + resultsPerPage only — **no** `WorkInDenmark` / radius on the request DTO | `JobnetContracts.cs` |
| Jobnet WiD scope is **server config** (`WorkInDenmarkOnly`, default true), not a user request knob today | `JobnetIntegrationOptions`, search service |
| URL state today: `source`, `keywords`, `country`/`location`, `selected` — no sort/pageSize/facets | `job-search-url-state.utils.ts` |
| FR-03: shareable URL state for public search | `project-specification.md` |
| Prior UX: do not invent sort/page-size chrome until API+URL wired | archived UX brief D4/open Q3 |

---

## 4. Assumptions

- **A1:** Solo authenticated job-seeker; public search stays board-scoped (EURES XOR Jobnet), not federated.
- **A2:** EURES schedule codes include at least `FULLTIME` / part-time equivalent (test data uses `FULLTIME`); exact closed allow-list is a BE/arch confirm before `/to-spec` locks wire values.
- **A3:** `PublicationPeriod` wire shape (string enum vs object) is not yet documented in-repo; BE confirms against live EURES API during implementation design — product locks **meaning** (freshness buckets), not JSON shape.
- **A4:** Raising default page size 5 → 10 improves “listings feel thin” without changing list|detail IA from #15.
- **A5:** Keeping multi-keyword → `BEST_MATCH` override is acceptable if UI states it honestly (see D5).
- **A6:** “Radius” for Jobnet is **not** an existing public API field; treating it as invent would violate issue non-goals.

---

## 5. Decisions / Proposals (recommended defaults)

### D1 — Recommended ship set (EURES)

| Control | Ship? | Default | Notes |
|---------|-------|---------|-------|
| **Sort** | **Yes** | `MOST_RECENT` (single keyword); multi-keyword → `BEST_MATCH` (existing API) | UI: Most recent · Best match |
| **Page size** | **Yes** (both boards) | **10**; choices **5 / 10 / 20** | Server already clamps to `MaxResultsPerPage` (50); do not offer 50 in UI |
| **Publication period** | **Yes** (EURES only) | **Any** (null / unset) | Closed buckets: Any · Last week · Last month · Last 3 months (exact codes = BE) |
| **Schedule** | **Yes** (EURES only) | **Any** | Full-time · Part-time · Any → `PositionScheduleCodes` |
| **Contract / offering** | **Defer** | — | Prefer schedule this slice (clearer seeker language; one facet row) |
| **Occupation URIs** | **Defer** | — | Catalog/UX cost; easy to invent dead chrome |
| **Sector / skills / education / languages / benefits / EURES flags** | **Out** | — | Portal parity anti-goal |

### D2 — Rejected alternatives

| Alternative | Why reject now |
|-------------|----------------|
| Full EURES facet parity | Explicit #6 non-goal; effort blow-up |
| Occupation in this slice | Needs URI catalog + picker; high dead-chrome risk |
| Schedule **and** contract together | Two opaque code lists in one ship; pick one high-value facet |
| Contract instead of schedule | Weaker everyday language; schedule maps to existing detail `WorkHours` vocabulary |
| Expose Jobnet WorkInDenmark toggle | Not on public request today; board already WiD-only via config; #15 already honest static copy |
| Invent Jobnet radius / geo knobs | No verified public request field |
| FE-only sort/page-size without API+URL | Creates dead chrome (#15/#6 packaging forbids) |
| Keep hardcoded page size 5 | Makes results feel sparse; API already defaults 20 server-side |
| Sync `page` (load-more index) into URL | Not in current URL contract; out of scope — reset to page 1 on new filter/sort |

### D3 — Jobnet knobs: **OUT** this slice

| Knob | Decision | Rationale |
|------|----------|-----------|
| WorkInDenmark user toggle | **Out** | Server `WorkInDenmarkOnly=true`; not a public request field; #15 already shows honest board context |
| Radius / distance | **Out** | Not a verified public API knob — would invent capability |
| Sort | **Out** | No Jobnet `SortSearch` on public request |
| Page size | **In (shared)** | `ResultsPerPage` already on Jobnet request; same 5/10/20 control as EURES when Jobnet selected |
| Future WiD / radius product knobs | **Defer** | Revisit only if product later wants non-WiD Jobnet scan (config change + API design) |

### D4 — URL-state recommendation

Preserve existing keys: `source`, `keywords`, `country`/`location`, `selected`.

**Add only for shipped params** (omit when default to keep links short):

| Param | Applies | Example | Omit when |
|-------|---------|---------|-----------|
| `sort` | EURES | `MOST_RECENT` / `BEST_MATCH` | default / multi-kw forced BEST_MATCH if preferred |
| `pageSize` | both | `5` / `10` / `20` | `10` (new default) |
| `published` | EURES | agreed period code | Any / unset |
| `schedule` | EURES | `FULLTIME` / part-time code | Any / unset |

Rules:

- Changing sort / page size / facets / keywords / country starts a **new search at page 1** (load-more pagination stays non-URL as today).
- Invalid or unknown enum values → clear validation message; do not invent board behavior.
- Source switch: EURES-only params dropped from URL when on Jobnet (and ignored if pasted).
- No dead query keys for deferred facets.

### D5 — Multi-keyword sort honesty

Keep existing API rule: **>1 keyword forces `BEST_MATCH`**.

UI must not pretend “Most recent” applies:

- Prefer: disable or auto-select Best match with helper text when ≥2 keywords; **or**
- Show banner: “Multiple keywords use best match.”

`/to-spec` locks the interaction; product preference = **auto-select + helper**, no silent override.

### D6 — Placement (product constraint for UX/FE)

Filters section job remains “compose & run search” (#15). New controls belong in the **primary filter row / compact results toolbar** — not a second admin panel:

- Sort + page size: results chrome or filter row (UX chooses; one primary Search still).
- Publication + schedule: EURES filter row only; hidden on Jobnet.
- No occupation typeahead in this slice.

### D7 — Priority / owners after human accept

1. Human accepts §5 defaults (or edits).  
2. `/to-spec` deepens #6 → `ready-for-agent`.  
3. `architecture-engineer` (thin): confirm EURES period/schedule wire codes + public request DTO shape under `eures-jobnet-search`.  
4. `backend-engineer` → `frontend-engineer` (+ `ui-ux-designer` for control placement) → `qa-engineer`.

---

## 6. Deliverables

### Acceptance criteria draft (#6) — testable

- [ ] **EURES API** accepts and forwards: `SortSearch`, `ResultsPerPage`, publication period, and schedule codes (agreed closed sets) with validation docs / clear 4xx messages for invalid values.
- [ ] **EURES UI** exposes sort (Most recent · Best match) and page size (5/10/20, default 10) **only** wired to API + URL — no inert controls.
- [ ] **EURES UI** exposes publication period and schedule (Any + closed options) wired to API + URL; Jobnet does not show these controls.
- [ ] **Jobnet UI** shares page-size control (same options/default); does **not** add WorkInDenmark, radius, or sort chrome.
- [ ] URL round-trip: share/reload restores shipped non-default params; unknown values rejected with clear copy; EURES-only params ignored on Jobnet.
- [ ] Changing sort, page size, period, or schedule re-runs search from page 1; empty/invalid combos do not call upstream with invented codes.
- [ ] Multi-keyword: UI reflects forced/selected Best match (no silent “Most recent” lie).
- [ ] Keywords + EURES country remain working; #15 honesty (no Remote/tags chrome, Jobnet static WiD copy) preserved — this slice does not reopen those.
- [ ] No full portal facet set; no occupation picker; no federated search.

### Issue / delivery

- Primary issue: **#6** (after `/to-spec`: deepen AC above; triage → `ready-for-agent`).
- Do **not** edit GitHub in this operate discuss turn (Principal-authorized later).
- Non-goals stay explicit on #6 (already listed on issue; reinforce below).

---

## 7. Contracts

- Respect **`eures-jobnet-search`** (APPROVED_EXISTING) — extend request/forwarding; do not invent a second search contract.
- Preserve FR-03 shareable URL state; additive params only for shipped knobs.
- Slice split remains: **#15** = IA/honesty (done operate path); **#6** = API+UI discovery power; **#12** = Jobnet suggestion lists deferred.
- Supabase identity unchanged; no payment scope.

---

## 8. Security notes

- Per-user auth unchanged; public board search remains authenticated dashboard journey as today.
- No secrets in Issues/briefs; upstream EURES/Jobnet credentials stay server-side.
- Untrusted listing HTML continues via existing sanitization path (out of this slice).
- Validate/allow-list facet codes server-side — do not pass arbitrary client strings through to upstream.

---

## 9. Validation

- Discuss-only: **no builds/tests run** this turn.
- After implementation: QA against §6 AC; prefer presentation + API contract tests where the project already covers search seams.
- Suggest (not run): targeted API normalizer tests for new fields; FE URL-state util tests for new keys.

---

## 10. Risks

| Risk | Mitigation |
|------|------------|
| Wrong `PublicationPeriod` / schedule wire codes | Arch/BE confirm against EURES before FE ships labels |
| Multi-keyword sort override surprises users | Honest UI (D5) |
| Page size 20 + list|detail feels heavy | Cap UI at 20; default 10 |
| Scope creep into occupation/contract | Explicit defer; reject in review |
| Reopening #15/#12 | Non-goals below; packaging note on #6 |

Residual from archive: if “listings are off” meant **relevance** (#7), filters help but do not replace ranking work.

---

## 11. Handoffs

| To | Ask |
|----|-----|
| **Human** | Accept or amend §5 defaults (one pass) |
| **Principal** | After accept → `/to-spec` on #6; optionally thin arch Task for wire codes |
| **architecture-engineer** | Public DTO + EURES payload mapping for period/schedule; Jobnet pageSize only |
| **backend-engineer** | Implement + validate under `eures-jobnet-search` |
| **frontend-engineer** + **ui-ux-designer** | Wired controls + URL; placement per #15 hierarchy |
| **qa-engineer** | Acceptance vs §6 AC |

Artifacts:

- This brief: `agent-system/handoffs/active/job-search-filters-slice2-2026-08-03/product-filters-brief.md`
- Thin YAML: `agent-system/handoffs/active/job-search-filters-slice2-2026-08-03/handoff-product-manager.yaml`
- Scratch: `agent-system/scratch/job-search-filters-slice2-2026-08-03/`

---

## 12. Status

**NEEDS_DECISION** — product recommendation complete; blocked only on **human accept** of §5 defaults before `/to-spec` / implementation.

### Non-goals (explicit)

- #15 IA, results chrome dedupe, mobile focus, Jobnet country honesty, FE dead Remote/tags
- #12 Jobnet-specific suggestion list content
- Occupation URI facet; contract/offering facet; sector/skills/education/languages/benefits
- Jobnet WorkInDenmark user toggle; Jobnet radius; Jobnet sort
- AI fit (#14); relevance scoring (#7); federated multi-source (#17); saved-jobs Phases 2–4
- Full EURES europa portal parity; dead UI without API+URL

### Open questions for human

1. **Accept ship set?** Sort + page size (default 10) + publication period + **schedule** (defer contract + occupation)?
2. **Jobnet:** confirm **OUT** for WiD/radius/sort; page size shared only?
3. **Multi-keyword sort:** OK to keep forced Best match with honest UI (auto-select + helper)?
4. **Publication buckets:** Any / last week / last month / last 3 months sufficient, or need “last 24h”?
5. **Control placement preference:** sort+pageSize in results chrome vs primary filter row? (UX can decide if human has no preference.)

### One-pass accept checklist (recommended answers)

1. Yes — ship set as D1.  
2. Yes — Jobnet knobs out; page size shared.  
3. Yes — keep multi-kw Best match + honest UI.  
4. Yes — Any / week / month / 3 months (no 24h unless BE finds it free).  
5. Defer to UX — prefer sort+pageSize in **results chrome**; period+schedule in **EURES filter row**.
