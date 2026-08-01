# PDF CV import pipeline redesign

**Task:** `cv-pdf-import-simplify-2026-08-01`  
**Agent:** architecture-engineer  
**Mode:** Design only (no application code changes)  
**Date:** 2026-08-01

---

## Architecture design summary

- **Request:** redesign
- **Status:** COMPLETE (human approval required before implementation)
- **Recommendation:** Split import into explicit Extract → Heuristic structure → gated AI → Persist stages; improve deterministic PdfPig extraction; call Gemini only when a confidence gate fails; replace substring coverage notices with high-signal stage notices.

---

## Current state

Evidence-based reconstruction of the upload → import path:

### End-to-end flow

1. **Upload / persist PDF** — `CvDocumentService` (`api/ApplyVault.Api/Services/CvDocuments/CvDocumentService.cs`) stores PDF bytes, then immediately calls `ICvStructuredImportService.ImportAndPersistAsync`.
2. **Read PDF** — `CvStructuredImportService.ReadCurrentPdfBytesAsync` loads `BaseStorageKey` / `StorageKey` via `ICvDocumentStorage`.
3. **Extract + section-split** — `CvPdfFullTextExtractor.ExtractSections` (`CvPdfFullTextExtractor.cs`):
   - Opens PDF with UglyToad.PdfPig
   - `page.GetWords()` grouped by `Math.Round(word.BoundingBox.Bottom, 0)` → line text (left-to-right)
   - Lines sorted page ASC, Y DESC
   - Headings matched via `CvPdfSectionDetector.TryMatchSectionHeading` (catalog aliases from ADR-0001 / `shared/cv-section-catalog/`)
   - Emits `CvPdfRawSection(Heading, NormalizedKey, PageIndex, Text)`
4. **Structure (AI-first today)** — `CvStructuredImportService.BuildPreviewAsync`:
   - If `GoogleAi:Enabled` → always call `ICvStructuredImportAiClient.ParseAsync` (`GoogleAiCvStructuredImportClient`) with raw sections
   - Normalize via `CvStructuredImportNormalizer`
   - On AI failure (non-`InvalidOperationException`) or empty AI sections → fall back to `CvStructuredImportHeuristic.Parse` + normalizer
   - If AI disabled → heuristic only, with notice encouraging enablement
5. **Coverage notice** — `CvStructuredImportCoverageAudit.BuildNotice` appends “N lines from the PDF may not have been imported…” by substring-matching source lines (≥12 chars) against flattened structured text — noisy false positives when layout/normalization splits or rephrases lines.
6. **Persist** — `ICvStructuredDocumentService.SaveStructuredAsync` (+ optional profile photo via `CvPdfProfilePhotoExtractor`).
7. **FE surface** — CV builder sole surface (ADR-0002); upload via `cv-document-api` / `cv-document.facade` → `importSummary` (`succeeded`, `sectionCount`, `usedAi`, `profilePhotoExtracted`, `notice`).

### Pain points (mapped to code)

| Pain | Root cause (evidence) |
|---|---|
| Incomplete text extraction | Coarse Y-rounding word grouping; no column/band awareness; no extraction quality signal; empty `GetWords()` → hard fail |
| Unnecessary AI trips | AI invoked whenever `GoogleAi:Enabled`, before heuristic quality is known |
| Noisy notices | Substring coverage audit + “enable Google AI” messaging |
| Hard to maintain/test | Stages coupled inside `BuildPreviewAsync`; no dedicated extractor unit tests found under `api/ApplyVault.Api.Tests/` |

### Verified providers / constraints

- AI: Google Gemini HTTP only (`GoogleAiOptions` + `CvImportAiOptions`) — do not invent vendors
- Vocabulary: CONTEXT.md **PDF import** — avoid “OCR CV” as product action
- Catalog: ADR-0001 binding for headings / section types
- Builder: ADR-0002 sole surface — no parallel My CV import UI

---

## Target state

### Pipeline stages (clear seams)

```text
PDF bytes
  → [1] Extract          ICvPdfTextExtraction (full text + lines + quality)
  → [2] Sectionize       heading detect → CvPdfRawSection[]
  → [3] Structure        CvStructuredImportHeuristic + Normalizer
  → [4] AI gate          decide skip | call Gemini | fail-closed to heuristic
  → [5] Persist          SaveStructuredAsync + photo (unchanged ownership)
  → [6] Notice           high-signal only (stage codes → user strings)
```

Orchestrator remains `CvStructuredImportService` (or a thin rename-internal `CvPdfImportPipeline` helper in the same folder) — **no new microservice**.

### Stage contracts (internal, not public REST)

| Stage | Input | Output | Testable in isolation |
|---|---|---|---|
| Extract | `Stream` / bytes | ordered lines (+ page, Y, optional band), char/word counts, `ExtractionQuality` | Yes — fixture PDFs |
| Sectionize | lines | `CvPdfRawSection[]` | Yes — line fixtures + catalog |
| Heuristic structure | raw sections | `CvStructuredSectionWriteDto[]` | Already partly covered |
| AI structure | raw sections (or sparse heuristic) | AI sections or throw | Existing client + prompt options |
| Gate | extraction quality + heuristic result + `GoogleAi:Enabled` | `SkipAi` \| `CallAi` | Pure unit tests |
| Notice | stage diagnostics | `string?` notice / optional structured codes | Unit tests |

### [1] Extract — fuller deterministic text (PdfPig, in-stack)

**Goal:** maximize recoverable text from digital PDFs without OCR.

Recommended improvements (implementer detail; still UglyToad.PdfPig):

1. **Line clustering with tolerance** — replace hard `Math.Round(..., 0)` with cluster-by-Y within a small ε (e.g. 1–2 pt) to avoid splitting one visual line.
2. **Reading-order bands** — detect simple 1- vs 2-column layouts by X gaps; order left column then right (or top-to-bottom within bands). Avoid full layout engine rewrite.
3. **Word/letter fallback** — if `GetWords()` is empty/sparse but letters/glyphs exist, assemble from letters; if still empty → `ExtractionQuality = Empty` (scan-like / image-only PDF).
4. **Preserve newlines intentionally** — keep line breaks for heuristic/AI; avoid collapsing entire page to one blob.
5. **ExtractionQuality enum** (internal): `Good` | `Sparse` | `Empty` — drives AI gate and notices.

**Out of scope for default implementation:** OCR engines / new vendors. See Risks.

### [2] Sectionize

Keep catalog-driven `TryMatchSectionHeading` (ADR-0001). Prefer injecting `ICvSectionCatalog` into extraction/sectionize path consistently (today static `DefaultCatalog` vs injected detector diverge slightly — unify under one path).

### [3] Heuristic-first structure

Always run `CvStructuredImportHeuristic` + `CvStructuredImportNormalizer` first when extraction produced sections. This is the default Structured CV candidate.

### [4] AI invocation policy

Call `ICvStructuredImportAiClient.ParseAsync` **only when all** hold:

1. `GoogleAi:Enabled == true`
2. Extraction quality ≠ `Empty` (empty → fail with clear notice; AI cannot invent text)
3. **Gate fails** — any of:
   - Heuristic section count = 0 after normalizer
   - Extraction `Sparse` (e.g. very low char count vs page count threshold — tune in tests)
   - Heuristic confidence low: e.g. only default “Profile/Summary” bucket with large body and few/no catalog headings matched; or Experience/Education/Projects expected headings present in raw text but mapped poorly (define 2–3 measurable signals max)
   - Optional config: `CvImportAi:ForceAi` / `PreferAi` for ops — **proposed** additive option; default off so heuristic-first is the product default

On AI success with non-empty normalized sections → use AI result (`UsedAi = true`).  
On AI failure → keep heuristic result; notice only if heuristic itself is weak.  
Do **not** show “Enable GoogleAi” as a user-facing import notice in the builder (ops config, not end-user guidance).

### [5] Persist

Unchanged seam: `SaveStructuredAsync`, photo extract, upload DTO shape.

### [6] Coverage / evaluation policy — tighten / replace

**Remove** (or demote to debug logging only) substring line-count warnings from `CvStructuredImportCoverageAudit` as the primary user notice.

**Keep high-signal notices only**, examples:

| Condition | User notice intent |
|---|---|
| Extraction `Empty` | No readable text in PDF (suggest text-based PDF; not “OCR CV” wording) |
| Extraction `Sparse` + heuristic weak + AI skipped/failed | Import may be incomplete — review in builder |
| AI used after gate | Optional quiet: omit, or short “AI assisted structuring” if product wants transparency |
| AI failed, heuristic used | Only when heuristic confidence also low |
| Success, good quality | `Notice = null` |

Internal diagnostics (logs/metrics) may retain coverage ratios for engineers; do not surface false-positive “N lines missing” to users.

---

## Options considered

### Recommended — Heuristic-first staged pipeline + PdfPig extract hardening

- Matches goals: fuller extract, fewer AI calls, maintainable stages, quieter notices
- Incremental seams inside `Services/CvDocuments/`
- Preserves REST upload/import DTOs (`CvStructuredImportSummaryDto`)

### Alternative A — Keep AI-first, only fix extractor — **rejected**

- Does not eliminate unnecessary Gemini trips when heuristic is already good
- Leaves coupled `BuildPreviewAsync` hard to test

### Alternative B — AI-only structuring, drop heuristic — **rejected**

- Worse offline/`GoogleAi:Enabled=false` behavior; more cost/latency; contradicts “no unnecessary AI”

### Alternative C — Require OCR for “fuller text” — **rejected as default**

- New provider/capability; product vocabulary avoids “OCR CV”; hosting/cost undecided
- Marked below as optional product decision only

---

## Impacted contracts

### Approved — must respect (no silent change)

- ADR-0001 section catalog + PDF heading aliases
- ADR-0002 CV builder sole surface
- CONTEXT.md PDF import vocabulary
- REST shapes: upload → `CvDocumentUploadResultDto` / `CvStructuredImportSummaryDto` (`Succeeded`, `SectionCount`, `UsedAi`, `ProfilePhotoExtracted`, `Notice`)
- Verified AI provider: Google Gemini HTTP

### Proposed (additive / non-breaking preferred)

| Change | Kind | Approval |
|---|---|---|
| Internal stage interfaces / helper types under `Services/CvDocuments/` | implementation | after design approve |
| `CvImportAi` gate options (e.g. thresholds, `ForceAi`) | config additive | after design approve |
| Narrower/emptyer `Notice` semantics (same field) | behavior change, same DTO | product/Principal OK in design approve |
| Optional structured notice codes in DTO | **proposed additive** REST | only if FE needs codes — escalate if breaking |
| OCR capability / vendor | **new provider** | **NEEDS_DECISION** — not in default plan |

No catalog schema change required for this redesign unless heading aliases need expansion for sectionize quality (catalog edit owned by backend + ADR-0001 process).

---

## Migration / sequencing

### Phase 0 — Approve design (human / Principal)

Gate: this document + handoff `READY`/`NEEDS_DECISION` on OCR only.

### Phase 1 — Extract + sectionize seam (`backend-engineer`)

- Harden `CvPdfFullTextExtractor` (tolerance clustering, basic columns, quality signal)
- Unify catalog heading match injection
- Unit tests with fixture PDFs / synthetic word layouts
- **DoD:** digital multi-section PDF fixtures extract fuller ordered text than baseline; empty PDF → clear `Empty` quality; existing upload integration still green

### Phase 2 — Heuristic-first + AI gate (`backend-engineer` + `ai-llm-engineer`)

- Reorder `BuildPreviewAsync`: heuristic always → gate → optional `ParseAsync`
- `ai-llm-engineer`: prompt/options only as needed for gated path; no new AI vendor; keep catalog-generated guidance
- Remove/disable user-facing substring coverage; replace with stage notices
- **DoD:** when heuristic confidence high, Gemini not called (test with fake/disabled client or spy); when gate trips and AI enabled, AI still used; notices have unit tests for empty/sparse/success

### Phase 3 — FE notice polish (`frontend-engineer`, optional/small)

- Only if notice copy/UX needs builder treatment; DTO field already exists
- **DoD:** builder shows high-signal import notice; no “enable Google AI” style ops copy

### Phase 4 — QA evidence (`qa-engineer`)

- Matrix: text PDF good extract; sparse; empty/scan-like; AI on/off; gate skip vs call; notice cases
- Integration: `CvDocumentsUploadImportIntegrationTests` extended as needed
- **DoD:** evidence recorded; no fabricated pass claims

OCR (if approved later) would be Phase 5+ and a separate provider decision — not sequenced into Phases 1–4.

---

## Ownership recommendations

| Concern | Primary | Secondary |
|---|---|---|
| Extract / sectionize / heuristic / pipeline orchestration / notices | backend-engineer | qa-engineer |
| Gemini client, `CvImportAi` prompts, gate thresholds affecting AI payload | ai-llm-engineer | backend-engineer |
| Builder upload notice UX | frontend-engineer | ui-ux-designer |
| Catalog heading aliases | backend-engineer | ai-llm-engineer (prompt consistency) |
| Design / this doc | architecture-engineer | principal-software-architect |
| OCR / new provider (if ever) | principal + product → platform/ai as chosen | — |

No ownership-matrix row change required; optional note later: “PDF import pipeline stages under `CvDocuments/` = backend primary.”

---

## Risks and open decisions

### Risks

1. **Scan-only PDFs** — PdfPig cannot recover text; without OCR, import correctly fails with clear notice. Residual product gap.
2. **Complex layouts** — tables/multi-column CVs may still lose reading order; band heuristic is best-effort, not a full layout engine.
3. **Gate false negatives** — skipping AI when heuristic “looks fine” but mis-splits entries; mitigate with 2–3 conservative gate signals + `ForceAi` for debugging.
4. **Gate false positives** — still calling AI often if thresholds too loose; tune with fixture suite.
5. **Notice behavior change** — users who relied on “N lines missing” lose that signal (intentionally — it was low-trust).

### Open decisions (human)

| ID | Decision | Recommendation |
|---|---|---|
| D1 | Approve staged heuristic-first pipeline + PdfPig extract hardening | **Approve** (this design) |
| D2 | OCR in-scope for this initiative? | **No for v1** — `ARCHITECT_PROPOSED` follow-up only if scan PDFs are a measured share of failures |
| D3 | Additive REST notice codes vs keep `Notice` string only | Prefer **string only** unless FE needs i18n codes |
| D4 | User-visible “AI assisted” notice when AI used | Prefer **omit** (quiet success) unless product wants transparency |

**OCR note (`ARCHITECT_PROPOSED` / `NEEDS_DECISION`):**  
If product later requires scan-PDF support, treat OCR as a separate capability behind `ICvPdfTextExtraction` with an explicit provider selection (not invented here). Keep CONTEXT.md vocabulary: product action remains **PDF import**, not “OCR CV”.

---

## Next actions for implementers

1. **Principal / human:** approve D1 (and decide D2–D4); then open/label GitHub Issue via project skills as needed.
2. **backend-engineer:** Phase 1 extract seams + tests; Phase 2 pipeline reorder + notice policy.
3. **ai-llm-engineer:** Phase 2 AI gate integration with existing Gemini client; prompt tweaks only if gated payloads change.
4. **frontend-engineer:** Phase 3 only if notice UX copy needs builder updates.
5. **qa-engineer:** Phase 4 matrix + integration evidence after implementation Tasks.
6. **Do not** invent OCR providers, secrets, or claim test/deploy outcomes in design follow-through.

---

## Security / tenancy notes

- Import remains authenticated user-scoped (`AppUserEntity` → own CV document) — no change to authz model.
- PDF bytes and extracted text stay server-side; Gemini receives section text only when gate fires (fewer outbound payloads than today when AI is enabled).
- Never log API keys; `GoogleAi:ApiKey` remains configuration-only.

---

## Non-goals

- Rewriting CV builder UX beyond import notice copy
- Changing export / HtmlExport pipelines
- Extension changes
- Payments / new hosting
- Overwriting CONTEXT.md, ADRs, or `.agents/skills` (propose vocabulary updates only if OCR productized later)
