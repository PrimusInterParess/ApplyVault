# PDF CV import — AI-first Structured CV fill

**Task:** `cv-pdf-section-match-2026-08-01`  
**Agent:** architecture-engineer  
**Mode:** Design only (no application code changes this pass)  
**Date:** 2026-08-01  
**Operator binding:** `agent-system/scratch/cv-pdf-section-match-2026-08-01/operator-decision-ai-first.yaml` (choice **2**)

---

## Architecture design summary

- **Request:** redesign (Structure stage — AI-first template fill)
- **Status:** COMPLETE — READY for implementation sequencing
- **Recommendation:** After extract succeeds, send **full ordered extracted text** to Gemini with ADR-0001 catalog / `GoogleAiCvSectionsResponseSchema` so the model **fills the Structured CV**. Heuristic + catalog-only Sectionize remain a **thin fallback** when AI is off or fails. Afterwards **remove** soft-heading / minimize-AI gate pollution.

---

## SUPERSEDED prior recommendations

| Artifact | What is superseded |
|---|---|
| `design-cv-pdf-section-match.md` | Phase A soft-heading Summary-only as **primary** fix; Phase B hollow-Skills gate as primary; recommendation to **keep minimize-AI / not AI-always** |
| Prior handoff `handoffs/active/.../handoff-architecture-engineer.yaml` | Soft-promotion + hollow gate sequencing |
| `design-cv-pdf-import-pipeline.md` | Heuristic-first + confidence gate as the happy-path Structure shape (Extract / P1 / notices / link integrity still stand) |

**Still in force (do not reopen):**

| ID | Binding |
|---|---|
| **Empty extract** | Hard-fail; never invent body text |
| **P1** | Never silent drop — Custom catch-all / never-omit spirit |
| **Link integrity** | URLs / emails remain atomic tokens |
| **Provider** | Google Gemini HTTP only |
| **Catalog** | ADR-0001 / `shared/cv-section-catalog/` |

Do **not** delete superseded design files; this note is the new Structure-stage source of truth for `/operate` on this task.

---

## Current state (evidence)

```text
PDF bytes
  → Extract + Sectionize (catalog aliases + SoftHeading)
  → Heuristic Place + Normalizer
  → Residual → Custom (P1)
  → AI gate (confidence / ForceAi) → 0–1 Gemini on pre-sectionized chunks
  → Notice → Persist
```

Orchestrator: `CvPdfImportPipeline.BuildPreviewAsync`.

| Concern | Path | Notes |
|---|---|---|
| Soft heading | `CvStructuredImportSoftHeading.cs` + `CvPdfFullTextExtractor.Sectionize` | Still on happy-path Sectionize; Summary-only narrow landed partially + tests in residual file |
| Confidence gate | `CvStructuredImportAiGate.cs` | SkipAi when typed career sections look fine — sample never reaches Gemini |
| ForceAi escape | `CvImportAiOptions.ForceAi` | Ops override for minimize-AI world |
| AI input | `CvImportSectionInput[]` from `rawSections` | Pre-sectionized chunks (polluted by SoftHeading) |
| Response schema | `GoogleAiCvSectionsResponseSchema` + catalog `BuildImportSystemPrompt` | Already fills Structured CV wire shape |
| P1 residual | `CvStructuredImportResidualPlacement` | Keep |
| Partial SoftHeading tests | `CvStructuredImportResidualPlacementTests.cs` SoftHeading_* | Pollution to remove with SoftHeading |

Live Gemini quality on operator sample: **NOT_EXECUTED** this design pass (do not claim).

---

## Target state

```text
PDF bytes
  → [1] Extract          ordered lines + quality (link-aware joins unchanged)
  → [2] AI fill template Gemini + catalog system prompt + response schema
                         (happy path when GoogleAi:Enabled)
  → [3] Normalize        CvStructuredImportNormalizer
  → [4] P1 residual      CvStructuredImportResidualPlacement if still needed
  → [5] Notice           CvStructuredImportNotices
  → [6] Persist          unchanged ownership

Fallback (AI off OR AI fail/empty sections only):
  → catalog-alias Sectionize (NO SoftHeading)
  → Heuristic.Parse + Normalize + P1 residual → Notice → Persist
```

No new microservice. No new catalog section type. No new AI vendor.

### Exact AI input (binding default)

**Send full extracted ordered text** — join extract lines with `\n` (optional page-break markers if already available on lines). **Do not** send SoftHeading / pre-sectionized `CvImportSectionInput[]` as the primary payload.

| Choice | Verdict |
|---|---|
| Full extracted lines/text | **Recommended** — simplest; model owns section boundaries; avoids SoftHeading shredding the prompt |
| Pre-sectionized catalog chunks | Rejected as default — couples AI to heuristic Sectionize mistakes |
| SoftHeading chunks | Forbidden — pollution |

Catalog aliases / field rules remain in the **system prompt** (`BuildImportSystemPrompt`) and **response schema**, not as input chunking.

**Proposed internal contract (non-breaking REST):** evolve `ICvStructuredImportAiClient.ParseAsync` to accept a full-text (or ordered-lines) payload. Keep wire **output** as `CvStructuredImportResult` / existing schema. Mark `CvImportSectionInput` section-array as legacy for import (may remain temporarily if client adapts by wrapping one synthetic section — prefer a clear full-text parameter).

### When heuristic runs

| Condition | Path |
|---|---|
| `GoogleAi:Enabled == false` | Heuristic fallback only |
| AI throws (non-fatal) / returns empty sections | Keep / fall back to heuristic result; set `aiFailed` |
| Empty extract | **Hard-fail** before AI or heuristic place — unchanged |
| Happy path + AI succeeds | **No** heuristic place; Normalize + P1 residual only |

Heuristic is **not** run “first then maybe AI.” Gate confidence matrix is removed.

### Residual / P1 after AI

Always run `CvStructuredImportResidualPlacement` after Normalize on the AI (or fallback) sections against a **stable source representation**:

- Prefer source = ordered extract lines (single document blob or thin catalog-alias-only sections **without** SoftHeading).
- P1 still parks unrepresented lines into Custom / “Additional information”.
- Never silent drop.

### Notices

Keep high-signal notices (empty extract throw; IncompleteReview when catch-all or weak/sparse+fail). Quiet success when placement good. Do **not** advertise “AI assisted” (existing D4 omit). Retune any notice logic that assumes “heuristic ran first” (`heuristicSections` parameter today) so it compares fallback-or-pre-AI structure honestly — implementer detail, preserve user-facing strings where possible.

---

## Options considered

### Recommended — AI-first full-text fill + thin heuristic fallback

Matches operator choice 2. Smallest happy-path mental model. Cleanup deletes SoftHeading / gate complexity that existed to protect minimize-AI.

### Alternative — AI-first but feed catalog-sectionized chunks — **rejected as default**

Slightly lower token ambiguity on huge CVs, but reintroduces Sectionize coupling; SoftHeading must still die. May revisit later as **optional** hint payload (`ARCHITECT_PROPOSED`) if full-text quality fails live Gemini evidence.

### Alternative — Keep SoftHeading under Summary only (prior Phase A) — **superseded**

Was a minimize-AI local fix; operator chose AI-first + pollution removal.

---

## Pollution removal checklist

Legend: **KEEP** / **DELETE** / **SIMPLIFY**

### Soft heading

| Symbol / artifact | Action |
|---|---|
| `CvStructuredImportSoftHeading` (type + file) | **DELETE** |
| `CvPdfFullTextExtractor.Sectionize` SoftHeading branch | **DELETE** |
| Catalog `TryMatchSectionHeading` Sectionize path | **KEEP** (fallback + residual source) |
| `SoftHeading_PromotesUnknownTitleCaseOutsideExperience` | **DELETE** |
| `SoftHeading_DoesNotPromoteOutsideSummaryContext` | **DELETE** |
| `SoftHeading_StillPromotesUnknownHeadingUnderSummary` | **DELETE** |

### AI gate / ForceAi / minimize-AI

| Symbol / artifact | Action |
|---|---|
| `CvStructuredImportAiGate.Decide` confidence path (`IsLowHeuristicConfidence`, cue arrays, `WeakResidual*`, `CountTypedSections` gate use) | **DELETE** |
| Thin enable check: Call AI iff `googleAiEnabled && quality != Empty` | **SIMPLIFY** (keep type or inline in pipeline) |
| `CvImportAiOptions.ForceAi` + appsettings + options tests for ForceAi | **DELETE** (redundant: enable Google AI = AI-first) |
| `LowConfidenceMinBodyChars` gate tuning | **DELETE** (or keep unused → prefer **DELETE**) |
| `CvStructuredImportAiGateTests` confidence / ForceAi matrix | **DELETE** / replace with enabled-vs-disabled |
| `BuildPreviewAsync_DoesNotCallAiWhenHeuristicConfidenceHigh` | **DELETE** / invert → calls AI when enabled |
| `BuildPreviewAsync_CallsAiWhenForceAi` | **DELETE** / replace with enabled happy-path AI call |
| `ICvStructuredImportAiClient` docs “heuristic-first / not always-on” | **SIMPLIFY** → AI-first when enabled |
| Prompt preface “invoked only when deterministic… needs help” | **SIMPLIFY** → AI-first structurer; still never invent / never omit / URLs atomic |
| User template “Deterministic structuring was insufficient…” | **SIMPLIFY** → “Structure the following extracted CV text…” |
| `GoogleAiCvSectionsResponseSchema` | **KEEP** |
| Catalog `BuildImportSystemPrompt` | **KEEP** (ai-llm may tighten skill-group / never-omit wording) |
| `CvStructuredImportHeuristic` + Normalizer + Residual + LinkIntegrity | **KEEP** (fallback / post-AI) |
| `CvStructuredImportCoverageAudit` noisy path | **KEEP deleted/unused** if already retired — do not revive |

### Partial-work note (operator)

Failed backend Task left SoftHeading unit tests without a durable soft-heading-primary product direction. Under this design: **remove those tests with SoftHeading** — do not finish Phase A as primary.

---

## Impacted contracts

### Approved — must respect

- ADR-0001 / `shared/cv-section-catalog/cv-section-catalog.json`
- P1 Custom catch-all + never silent drop
- Link integrity
- REST import summary DTO shape (`usedAi`, `notice`, sections) — no FE change required this task
- Google Gemini HTTP only

### Proposed (internal)

| Change | Kind |
|---|---|
| AI-first pipeline order in `CvPdfImportPipeline` | implementation |
| Full-text AI input (replace section-array primary payload) | internal AI client contract |
| Remove SoftHeading / ForceAi / confidence gate knobs | config simplify (`appsettings.example.json`) |
| Prompt preface/user template AI-first framing | options / ai-llm |

No public REST breaking change. No catalog schema version bump required for Structure fill (schema already exists).

---

## Migration / sequencing

### Phase 1 — AI payload + prompts (`ai-llm-engineer`, primary)

1. Define full-text user payload shape (ordered lines / single string + `{{payload}}` template).
2. Rewrite `SystemPromptPreface` / user template for AI-first (drop minimize-AI framing; keep never-invent, Contact first-class, never-omit → Custom, URL atomicity).
3. Confirm `GoogleAiCvSectionsResponseSchema` + catalog prompt remain the Structured CV fill contract.
4. Coordinate `ParseAsync` signature with backend (full text in).
5. Do **not** claim live Gemini success without HTTP execution.

**DoD:** Documented prompt + payload contract ready for backend wire-up; schema still validates sections/entries.

### Phase 2 — Pipeline + cleanup (`backend-engineer`, primary)

1. Reorder `CvPdfImportPipeline`: Extract → (if AI enabled) Parse full text → Normalize → Residual → else heuristic fallback path.
2. Delete SoftHeading type + Sectionize branch; SoftHeading_* tests.
3. Simplify/remove AiGate confidence + ForceAi + related tests/options/example config.
4. Residual source without SoftHeading; preserve P1 + empty extract hard-fail.
5. Realign pipeline tests to AI-first defaults.
6. Notices: adjust parameters so AI-first / fallback semantics stay high-signal.

**DoD:** With Google AI enabled (spy/fake client), pipeline calls AI on non-empty extract without ForceAi; SoftHeading gone; heuristic only on off/fail; P1 + URL tests still green.

### Phase 3 — QA (`qa-engineer`)

Matrix:

- Empty PDF → hard-fail notice
- Google AI disabled → heuristic fallback structures something; no invent
- AI enabled success (fake/spy) → usedAi; typed sections from schema
- AI fail → fallback + aiFailed notice behavior
- Sample PDF Skills/Languages not shredded (no SoftHeading); P1 catch-all; URL atomic regression
- Live Gemini: **NOT_EXECUTED** unless secrets + HTTP authorized

Delivery chain: Principal may open Issue via skills `to-spec` → `to-tickets` → `implement` (+ `tdd`) → `code-review` as usual.

**Order note:** Prefer Phase 1 contract lock (or pair with Phase 2 start) before large backend delete; cleanup SoftHeading/gate can land in the same backend PR as pipeline reorder once payload shape is agreed.

---

## Ownership recommendations

| Concern | Primary | Secondary |
|---|---|---|
| Prompts / full-text payload / schema framing | ai-llm-engineer | backend-engineer |
| Pipeline reorder + SoftHeading/gate deletion + residual source | backend-engineer | ai-llm-engineer |
| Test evidence matrix | qa-engineer | backend-engineer |
| This design | architecture-engineer | principal-software-architect |

No ownership-matrix dual-own change required.

---

## Risks and open decisions

### Risks

1. Live Gemini quality on full-text input unknown (**NOT_EXECUTED**).
2. Large CVs → token limits — monitor; optional future chunking is `ARCHITECT_PROPOSED`, not this pass.
3. Heuristic fallback without SoftHeading may swallow unknown mid-CV headings into prior catalog section — P1 residual still required.
4. Config: removing `ForceAi` is intentional; disable via `GoogleAi:Enabled`.

### Open decisions for Principal / operator

None blocking — operator choice 2 binds AI-first. Escalate only if live Gemini fails quality bar and a temporary dual-path is requested.

---

## Security / tenancy

Unchanged: user-scoped import; no new secrets; Gemini only when Google AI enabled; never embed API keys in prompts/handoffs.

---

## Non-goals

- Frontend / extension changes
- New Unplaced section type
- OCR / new vendors
- Reopening P1 or link-integrity bindings
- Keeping SoftHeading as a parallel “improvement” on the AI path
- Claiming live Gemini results without execution
