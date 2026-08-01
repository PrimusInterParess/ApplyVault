# PDF CV import — Structure / Placement redesign

**Task:** `cv-pdf-structure-placement-2026-08-01`  
**Agent:** architecture-engineer  
**Mode:** Design only (no application code changes)  
**Date:** 2026-08-01  
**Builds on:** `agent-system/design-cv-pdf-import-pipeline.md` (Extract → Heuristic → gated AI — largely implemented)  
**Operator decisions bound:** `agent-system/scratch/cv-pdf-structure-placement-2026-08-01/operator-decisions.yaml`

---

## Architecture design summary

- **Request:** redesign (Structure / Placement stage after Extract)
- **Status:** COMPLETE (P1 + link integrity bound by operator; ready for implementation sequencing)
- **Recommendation:** Keep Extract/gate seams; add a deterministic **placement + residual** pass that never silently drops body text—spill unmatched content into ADR-0001 `Custom` sections (Additional); tighten AI gate to placement quality (not heading count); fix **link integrity** so URLs survive extract → contact split → structure → persist as whole tokens.

---

## Operator decisions (bound — do not re-ask)

| ID | Decision | Binding |
|---|---|---|
| **P1** | Unplaced / unmatched extracted text | **MUST** persist as `Custom` (Additional) sections — never silent drop. Short `Notice` only when Custom catch-all is used **or** structure is weak. Quiet success (`Notice = null`) when placement is good. |
| **Link integrity** | URLs / hyperlinks | **REQUIRED.** Full URLs (and PDF link text when available) must survive extract → heuristic/AI → normalizer → persist. |

Recorded in [ADR-0005](../docs/adr/0005-cv-pdf-import-ai-first-structure.md) (still in force after AI-first Structure).

---

## Current state

Evidence from `api/ApplyVault.Api/Services/CvDocuments/` + catalog + prior pipeline design.

### Pipeline today (post simplify)

```text
PDF bytes
  → Extract (CvPdfFullTextExtractor) — lines + quality + Sectionize
  → Heuristic (CvStructuredImportHeuristic) + Normalizer
  → AI gate (CvStructuredImportAiGate) → optional Gemini
  → Notice (CvStructuredImportNotices) → Persist
```

Orchestrator: `CvPdfImportPipeline.BuildPreviewAsync`.

### Why matching still fails after full text extract

Full extract solves “no glyphs.” Placement fails later:

| Failure | Evidence | Effect |
|---|---|---|
| **Heading alias wall** | `Sectionize` only starts a new section when `ICvSectionCatalog.TryMatchSectionHeading` matches (`CvPdfFullTextExtractor` ~152–197; aliases in `shared/cv-section-catalog/cv-section-catalog.json`) | Unknown headings (e.g. “Tech stack”, “Interests”, “Professional highlights”) stay as body of the previous section (often default Profile/`summary`) |
| **Wrong type → wrong fields** | Heuristic chooses parsers by `MapSectionType(normalizedKey)` (`CvStructuredImportHeuristic.Parse` / `ParseEntries`) | Mis-bucketed body is shredded by Experience/Skills rules that do not fit |
| **Custom underused** | Catalog `Custom` already has aliases (certifications, languages, …) and full entry fields | Non-alias headings never become their own `Custom` sections |
| **No residual path** | `CvStructuredImportCoverageAudit` is diagnostics-only; notices deliberately ignore coverage (`CvStructuredImportNotices`) | Unplaced lines can disappear from the Structured CV with no user signal |
| **Gate false confidence** | `IsLowHeuristicConfidence` treats any non-`Profile` heading as “matched” and only checks presence of Experience/Education/Projects types—not entry-field fit or residual lines (`CvStructuredImportAiGate`) | Gemini skipped when headings look fine but placement is poor |
| **Contact / Custom drift** | ADR-0001 Contact is first-class; `CvImportAiOptions.DefaultSystemPrompt` still documents Contact→Custom; `RestoreMissingContactFromSource` merges only `Custom`+heading Contact, while `CreateContactSection` emits `Contact` type | Inconsistent restore / AI docs vs runtime catalog prompt |
| **Silent entry drops** | `EntryHasContent` filters; Skills `pendingGroupTitle` can be abandoned; empty sections filtered | Content can vanish without residual capture |

### Link integrity — current failure points

**1. Contact token splitting (primary, confirmed)**

`CvStructuredImportEntrySupport.SplitContactTokens` splits on `|`, `·`, `•`, **`/`**, and **`\`**:

```62:68:api/ApplyVault.Api/Services/CvDocuments/CvStructuredImportEntrySupport.cs
    public static IReadOnlyList<string> SplitContactTokens(string line) =>
        line.Split(['|', '·', '•', '/', '\\'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .SelectMany((token) =>
                token.Contains(',', StringComparison.Ordinal) && LooksLikeContactLine(token)
                    ? token.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    : [token])
```

Called from Profile/Contact heuristic paths (`CvStructuredImportHeuristic.ParseContactSection`, `SplitLeadingContactBlock`).  
Effect: `https://github.com/org/repo` → fragments (`https:`, `github.com`, `org`, `repo`).  
Unit tests already encode the intended behavior (`SplitContactTokens_PreservesUrlsWithSlashes`, `HeuristicParse_PreservesContactUrlsWithSlashes` in `CvStructuredImportContactTests`) — current delimiter set contradicts those tests.

**2. Extract line assembly (secondary, layout-dependent)**

`CvPdfFullTextExtractor.ClusterLinesByY` joins tokens with a single space. PdfPig often emits URL pieces as separate words (`https://`, `github.com/foo/bar`). Space join usually keeps a usable string, but:

- Mid-URL word breaks with large gaps can insert spaces inside the URL.
- Letter-fallback merge (`AssembleTokensFromLetters` / `MergeTokens`) can glue or split path segments incorrectly when glyph gaps are noisy.
- Column banding (`TryDetectColumnSplit`) can reorder left/right bands and visually separate a URL from its label (text may still be present, but not as one contact token).

**3. PDF hyperlink annotations (gap)**

Extractor uses words/letters only — no PdfPig link/URI annotation pass. When visible text is “LinkedIn” and the URI lives only in an annotation, the full URL never enters the pipeline.

**4. Downstream (must not re-break)**

- Normalizer field trim / bullet strip must not truncate URL punctuation beyond existing safe trims.
- Persist clamps (`TechStack` nvarchar(512) etc.) can truncate very long URLs if misfiled into `techStack`; Contact bullets are the preferred home for links.
- AI path: catalog prompt does not explicitly say “keep URLs atomic”; gated AI must not rewrite links into pieces.

---

## Target state

### Stage shape (incremental on existing pipeline)

```text
PDF bytes
  → [1] Extract (+ link-aware line assembly; optional annotation URIs)
  → [2] Sectionize (catalog aliases + soft Custom heading promotion)
  → [3] Place (type-specific heuristic entries)
  → [4] Residual → Custom (Additional)   ← P1, never silent drop
  → [5] AI gate (placement-quality signals; 0–1 Gemini call)
  → [6] Normalize (link-safe; Contact first-class)
  → [7] Notice (short; only catch-all or weak)
  → Persist (unchanged ownership)
```

No new microservice. Stay inside `Services/CvDocuments/`.

### [A] Placement algorithm (deterministic-first, simple)

1. **Sectionize**
   - Keep catalog `TryMatchSectionHeading` for known aliases → typed or `Custom` (per catalog).
   - **Soft heading promotion (conservative):** a short line (≤64 chars after normalize), not a bullet/date/contact/URL line, title-case or ALL CAPS, that fails alias match → start a new raw section with `NormalizedKey` = that heading and mapped type `Custom` (preserve original heading text).
   - Do **not** promote lines that look like role titles inside Experience (date-anchored blocks nearby) — prefer false-negative promotion over shredding jobs; residual pass still captures orphans.

2. **Place**
   - Existing type parsers (Contact / Summary / Skills / dated entry chunks).
   - Track, per raw section, body lines that are not represented in any placed field (deterministic containment check on normalized line or ≥20-char prefix — reuse coverage-audit idea **internally only**).

3. **Residual → Custom (P1)**
   - Any non-trivial residual line (≥ configurable min length, default 8–12) must land in a `Custom` section:
     - Prefer append to the `Custom` section created for that unmatched heading; else
     - Emit/append a single catch-all `Custom` section with heading **“Additional information”** (or source heading when residual is section-scoped).
   - Entry shape: one entry per residual chunk (or one entry with bullets = residual lines). Use catalog `Custom` fields (`title`/`summary`/`bullets`). Never discard.
   - Catalog-aliased Custom sections (Certifications, Languages, …) keep their PDF heading; catch-all uses “Additional information”.

4. **AI gate (minimize trips)**
   Call Gemini only when `GoogleAi:Enabled` and any of:
   - `ForceAi`
   - Zero sections after place+residual (should be rare)
   - Extraction `Sparse` and typed section count low
   - Placement weak: large residual ratio **before** Custom spill **and** missing expected typed sections (Experience/Education/Projects cues) — **or** only Summary/Custom with huge body and no Experience when cues present
   - **Do not** call AI solely because Custom catch-all was used after a successful residual spill
   - On AI success: re-run residual pass on AI output vs raw lines so P1 still holds  
   - On AI failure: keep heuristic+Custom result

5. **Notices (P1 + prior D4)**
   - `Notice = null` when placement good and no Custom catch-all / soft-promoted Custom-only spill.
   - Short notice when Custom catch-all (“Additional information”) used **or** structure weak / Sparse+weak — e.g. reuse/adapt `IncompleteReview` copy; **no** substring “N lines missing” coverage notices.
   - Omit “AI assisted” (prior D4).

### [B] Link integrity rule (concrete, maintainable)

**Invariant:** After import, every URL-like span present in extracted text appears as a contiguous string in some persisted entry field (prefer Contact bullets; else Summary/Custom bullets/summary).

**Rules for implementers:**

1. **URL span detection (shared helper)**  
   One small internal helper (e.g. `CvImportLinkIntegrity`) used by extract assembly, contact split, and tests:
   - Recognize: `https?://…`, `www.…`, bare `linkedin.com/…`, `github.com/…`, optional `mailto:`  
   - Span continues through `/ ? # & = % . - _ ~ @ :` until whitespace or a clear delimiter (`|`, `·`, `•`).

2. **`SplitContactTokens` (must fix)**  
   - Delimiters for contact multi-value lines: `|`, `·`, `•` only (and comma **outside** URL spans).  
   - **Never** use `/` or `\` as delimiters.  
   - Algorithm: scan left-to-right; if inside URL span, copy chars verbatim; only split on delimiters outside spans.  
   - Satisfy existing `CvStructuredImportContactTests` URL cases.

3. **Extract line building**  
   - When joining tokens on a Y-cluster, if adjacent tokens form a URL span (previous ends with `://` or both sides match URL continuation), join **without** inserting a space.  
   - Optional (same phase, still PdfPig only): if page link annotations exist, attach URI to overlapping text; when visible text is a short label and URI differs, persist `label (uri)` or URI in Contact — **prefer URI as the stored value** for import bullets so links stay actionable. Mark annotation enrichment as best-effort; text-path URLs remain mandatory.

4. **Heuristic / AI / Normalizer**  
   - Treat whole URL lines as atomic (already partly via `LooksLikeLinkLine` — do not split further).  
   - Catalog import prompt + preface: “Keep URLs and emails as single tokens; never split on `/`.”  
   - Normalizer: do not move URL fragments into separate bullets; Contact restore must merge into `Contact` type (fix Custom-only lookup drift).

5. **Persist / builder**  
   - Prefer Contact bullets for links; avoid filing long URLs into `techStack` (512 clamp).  
   - No REST change required for link integrity.

6. **Tests (DoD for link work)**  
   - Unit: `SplitContactTokens` with `https://…/…`, `linkedin.com/in/…`, mixed `|` separators.  
   - Heuristic Profile header with piped URLs.  
   - Extract join fixture: tokens `https://` + `example.com/a/b` → one line without mid-URL space.  
   - Residual Custom must not re-split URLs.

### [C] Industry patterns → ApplyVault catalog

| Common practice | What others do | ApplyVault mapping |
|---|---|---|
| Catch-all / Other | Unmapped chunks → Other/Additional | **`Custom`** (+ heading “Additional information” for catch-all) — **P1** |
| Named soft sections | Keep PDF heading, generic fields | **`Custom`** with preserved heading (aliases already cover certs/languages/…) |
| Raw text sidecar | API returns `raw_text` + structured | Keep extract server-side; Structured CV remains sole edit surface (ADR-0002) — no parallel raw UI |
| Partial import + review | Confidence UI / remap | Short `Notice` + visible Custom sections in builder — **no** noisy coverage counts |
| Classify then field-fill | ML two-stage | Our deterministic Sectionize→Place; Gemini only gated |

---

## Options considered

### Recommended — Deterministic place + Custom residual (P1) + link-safe tokens

- Matches operator P1 and link integrity.
- Minimal architecture; reuses ADR-0001 `Custom`.
- AI stays optional; fewer false-positive notices.

### Alternative A — AI-first remap of all text — **rejected**

- More Gemini trips; contradicts minimize-AI goal; still needs residual for fail-closed.

### Alternative B — New `Unplaced` section type in catalog — **rejected**

- `Custom` already exists; new type needs catalog/UI/codec churn without benefit.

### Alternative C — Persist only raw sidecar without Structured placement — **rejected**

- Breaks builder-as-sole-surface editing model (ADR-0002); orphans stay non-editable as sections/entries.

### Alternative D — Restore substring coverage user notices — **rejected**

- Prior initiative removed them for false positives; P1 uses Custom presence + short notice instead.

---

## Impacted contracts

### Approved — must respect

- ADR-0001 section catalog (`Custom`, `Contact`, aliases)
- ADR-0002 CV builder sole surface
- CONTEXT.md PDF import vocabulary (no “OCR CV” product wording)
- REST upload / `CvStructuredImportSummaryDto` shape (`Succeeded`, `SectionCount`, `UsedAi`, `ProfilePhotoExtracted`, `Notice`)
- Google Gemini HTTP only

### Proposed (non-breaking / internal)

| Change | Kind | Approval |
|---|---|---|
| Internal residual placement + soft Custom heading promotion | implementation | after this design |
| Link-integrity helper + `SplitContactTokens` / extract join fix | implementation | after this design |
| Notice when Custom catch-all used (same `Notice` string field) | behavior | **bound by P1** |
| Additive catalog aliases for common missing headings | catalog additive | optional follow-up; not required if soft promotion + residual work |
| PdfPig annotation URI enrichment | extract enhancement | recommended best-effort in same backend phase; not a new vendor |
| Additive REST unplaced markers | **not recommended** | Custom sections + `Notice` suffice (P1) |

---

## Migration / sequencing

### Phase 0 — Design accepted (this doc + operator P1 / links)

No further product choice on unplaced policy.

### Phase 1 — Link integrity (`backend-engineer`)

- Fix `SplitContactTokens`; shared URL-span helper; extract adjacent-token join; Contact restore uses `Contact` type.
- Tests from existing URL cases + extract fixtures.
- **DoD:** piped GitHub/LinkedIn URLs survive Profile→Contact; no `https:` orphan bullets.

### Phase 2 — Placement + Custom residual (`backend-engineer`)

- Soft heading promotion; residual→Custom “Additional information”; wire into `CvPdfImportPipeline` after heuristic (and after AI if used).
- Gate: stop treating “any non-Profile heading” as high confidence; skip AI when residual successfully parked in Custom unless other weak signals.
- Notices: short notice iff catch-all Custom used or weak structure.
- **DoD:** fixture with unknown heading + orphan lines → Custom sections contain every residual line; good placement → `Notice` null; Gemini not called when place+residual is strong.

### Phase 3 — AI prompt consistency (`ai-llm-engineer`)

- Align preface/docs with Contact first-class + “never omit lines; park in Custom” + “URLs atomic”.
- No new vendor; gated path only.

### Phase 4 — FE notice polish (`frontend-engineer`, optional)

- Only if builder copy needs tweak for Custom catch-all notice; DTO unchanged.

### Phase 5 — QA (`qa-engineer`)

- Matrix: unknown headings; residual Custom; URL pipes; AI on/off; quiet vs notice; no coverage false positives.
- Do not claim live Gemini unless executed.

Delivery chain: project skills `to-spec` → `to-tickets` → `implement` (+ `tdd`) → `code-review` as Principal sequences Issues.

---

## Ownership recommendations

| Concern | Primary | Secondary |
|---|---|---|
| Place / residual / link integrity / notices / gate signals | backend-engineer | qa-engineer |
| Gemini prompt / preface alignment | ai-llm-engineer | backend-engineer |
| Catalog additive aliases (optional) | backend-engineer | ai-llm-engineer |
| Builder notice UX | frontend-engineer | ui-ux-designer |
| This design | architecture-engineer | principal-software-architect |

No ownership-matrix dual-own; optional note: “PDF import placement + link integrity under `CvDocuments/` = backend primary.”

---

## Risks and open decisions

### Risks

1. Soft heading promotion may over-split Experience — mitigate with conservative heuristics + residual still catches orphans.
2. Annotation URIs absent on many PDFs — text-path integrity remains the hard requirement.
3. Gate under-calling AI when typed entries are wrong but residuals empty (text parked in wrong fields) — accept for v1; optional later “entry shape” signals; `ForceAi` for ops.
4. Very long URLs in clamped columns if misfiled — prefer Contact bullets.

### Open decisions

| ID | Status |
|---|---|
| P1 unplaced → Custom | **DECIDED** (operator) |
| Link integrity required | **DECIDED** (operator) |
| Additive REST unplaced fields | **Recommend no** — escalate only if FE later needs codes |
| Scratch cleanup S/K | **not_decided** (operator) — leave alone |
| OCR | Out of scope (prior D2) |

---

## Next actions for implementers

1. **Principal:** accept this design; open/label GitHub Issue via skills chain; delegate Phase 1–2 backend Task.
2. **backend-engineer:** link integrity first (unblocks contact), then placement residual + notices + gate.
3. **ai-llm-engineer:** Phase 3 prompt alignment after residual contract exists.
4. **frontend-engineer:** Phase 4 only if notice UX needs copy.
5. **qa-engineer:** Phase 5 evidence matrix.
6. Do not invent OCR/providers/secrets; do not restore coverage user notices.

---

## Security / tenancy notes

- Import remains user-scoped; no authz change.
- URLs may contain PII/profile paths — stay in user’s Structured CV; Gemini receives section text only when gate fires.
- Sanitize only for export hyperlink schemes (existing `SanitizeLinkUrl`); import must preserve original URL text.

---

## Non-goals

- New catalog section type for unplaced text
- OCR / new AI vendors
- Extension changes
- Parallel raw-text editing surface outside CV builder
- Overwriting CONTEXT.md / ADRs / `.agents/skills` without approval
