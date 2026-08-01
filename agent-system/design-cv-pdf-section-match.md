# PDF CV import — Section / template match failure

**Task:** `cv-pdf-section-match-2026-08-01`  
**Agent:** architecture-engineer  
**Mode:** Design only (no application code changes)  
**Date:** 2026-08-01  
**Builds on:** `design-cv-pdf-structure-placement.md` (P1 residual Custom + soft headings + link integrity — shipped)  
**Evidence probe:** `agent-system/scratch/cv-pdf-section-match-2026-08-01/probe-out/` (sample CV via Extract → Heuristic → Residual → AI gate)

---

## Architecture design summary

- **Request:** redesign (post-extract Structure / Placement quality — typed section match)
- **Status:** COMPLETE — READY for implementation (no P1 / catalog / AI-always operator re-decision required)
- **Recommendation:** Keep pipeline seams and P1 residual. **Primary fix:** narrow soft Custom heading promotion so it cannot fire inside `Skills` / `Custom` / `Contact` (only allow under leading `Summary`/Profile context). **Secondary:** tighten AI gate for “typed section present but empty/hollow” (empty Skills while skill cues exist). Do **not** switch to AI-first.

---

## Operator complaint (interpreted)

Extract succeeds (glyphs/lines present). Structuring fails to land content in ADR-0001 typed templates (`Experience` / `Education` / `Skills` / `Projects` / `Summary` / `Contact` / `Custom`). Prior task closed residual/link work with limitations (live Gemini **NOT_EXECUTED**).

Probe on operator sample (`sample.pdf` / Desktop `Yordan-Borisov.pdf`): extract quality **Good**, 80 lines — then Sectionize emits **16** raw sections including empty `Skills` and skill-group fragments as `Custom`.

---

## Current state (post structure-placement ship)

```text
PDF bytes
  → Extract + Sectionize (catalog aliases + soft Custom promotion)
  → Heuristic Place + Normalizer
  → Residual → Custom (P1)
  → AI gate (0–1 Gemini)
  → Notice → Persist
```

Orchestrator: `CvPdfImportPipeline.BuildPreviewAsync`.

Bound decisions still in force (do not reopen unless evidence forces it):

| ID | Binding |
|---|---|
| **P1** | Unplaced text → Custom / “Additional information”; quiet success when placement good |
| **Link integrity** | URLs stay atomic (no `/` `\` contact split) |

---

## Root-cause ranking (with evidence)

### RC1 — PRIMARY — Soft heading over-promotion inside Skills / Custom

**Where:** `CvStructuredImportSoftHeading.LooksLikePromotableHeading` only suppresses promotion when current type is Experience / Education / Projects (`CvStructuredImportSoftHeading.cs` ~43–50). `CvPdfFullTextExtractor.Sectionize` (~173–184) promotes on that predicate.

**Sample probe (`01-sectionize.json` / extractor log):**

| Expected | Actual Sectionize |
|---|---|
| One `Skills` body with groups Backend/Frontend/… | `Skills` **0 body lines**; then Custom sections `Backend`, `Frontend`, `AI & Automation`, `Fintech & Auth`, `Cloud/DevOps` |
| One `Languages` Custom with proficiency lines | `Languages` keeps only “Language Proficiency”; `Bulgarian (Native)`, `English (Fluent)`, `Danish (B1-DU3M4)` become empty Custom headings |

Soft-hit dump (`03-soft-heading-hits.json`) lists every skill-group label as `Kind: soft` under `skills`.

**Why epic:** Heuristic then maps unknown keys → `Custom` (`MapHeadingAliasToSectionType` default) and runs generic `ParseEntryChunk`, stuffing comma skill lists into **Title** — not Skills `title`/`techStack` groups. Result: empty Skills section + fake Custom “sections” that look like a broken template match.

**Note:** Unit test `SoftHeading_PromotesUnknownTitleCaseOutsideExperience` only asserts block under Experience — never Skills (`CvStructuredImportResidualPlacementTests.cs` ~122–136). Gap enabled the regression.

### RC2 — AI gate skips Gemini when typed career sections look fine

**Where:** `CvStructuredImportAiGate.IsLowHeuristicConfidence` (~83–147). Calls AI when Experience/Education/Projects **cues** missing typed sections, or large residual **and** `typedCount == 0`. Does **not** treat empty/hollow `Skills` (or shredded Custom siblings) as low confidence.

**Probe:** `AiGate = SkipAi` despite empty Skills and language shredding. Residual before spill = 4 / 64 (below weak threshold for the typedCount==0 branch). Gemini path **NOT_EXECUTED** in this task (no live HTTP). Operator “model fails epicly” is consistent with either (a) model never called, or (b) unvalidated AI quality — do not claim live Gemini outcome.

### RC3 — Residual / P1 masks wrong typing instead of fixing it

**Where:** `CvStructuredImportResidualPlacement` parks unrepresented lines (P1). Probe: `UsedCatchAll=true` with four `Technologies: …` lines in “Additional information” even though Projects already have `techStack` (colon/comma containment false negatives when values contain nested commas, e.g. `LLMs (Ollama, Gemini)`).

P1 correctly prevents silent drop but **does not** restore typed Skills. False-positive Additional bullets add noise/notices.

### RC4 — Heuristic entry-field quality (secondary)

| Issue | Evidence |
|---|---|
| Experience achievement lines without `•/-/*` stay in `summary` blob, not bullets | Probe Experience entries: `BulletCount: 0` while source has “Cross-Team Orchestration: …” lines |
| Second Education row collapsed into first entry summary | Probe Education `EntryCount: 1`; MA/BA line in `Summary` |
| Contact address comma-split | “Address: Fruenshave 24, 8541 …” → multiple bullets (delimiter outside URL span — pre-existing contact split) |

These hurt polish; they are **not** the epic “wrong template section” failure.

### RC5 — Catalog alias side-effect (minor)

`About Me` matches Summary alias `about me` → second Summary section. Acceptable under ADR-0001; optional later: prefer Custom when body looks like interests. Not primary.

### RC6 — AI prompt / schema (not primary for this sample)

Phase 3 prompts already require Contact first-class, never omit, URLs atomic (`CvImportAiOptions`). Live Gemini **NOT_EXECUTED**. Prompt work alone cannot fix RC1/RC2 when gate skips AI.

---

## Target state

Keep stage shape. Change Sectionize soft-promotion policy and gate signals:

```text
[1] Extract (unchanged link-aware joins)
[2] Sectionize
      - catalog aliases (unchanged)
      - soft Custom promotion ONLY when current mapped type is Summary
        (Profile / Summary context). Never inside Skills, Contact, Custom,
        Experience, Education, Projects.
[3] Place (heuristic) — Skills two-line groups work again on intact body
[4] Residual → Custom (P1 unchanged)
[5] AI gate — also CallAi when a catalog-typed section is hollow while
      source cues/body exist (esp. Skills empty + skills cue / skill-like Custom siblings)
[6] Normalize / Notice / Persist
```

No new microservice. No new catalog section type. No AI-always.

---

## Options considered

### Recommended — Narrow soft promotion + hollow-typed gate signal

- Smallest change that restores Skills/Languages on the failing sample.
- Preserves soft promotion for unknown headings in the leading Summary/Profile blob (original intent: “Tech stack”, “Interests” before career sections).
- Preserves P1 and minimize-AI (one optional trip when structure still hollow).

### Alternative A — Disable soft promotion entirely — **rejected**

- Reopens “unknown heading swallowed into previous section” from prior design; residual catch-all alone yields worse headings.

### Alternative B — AI-first remap always — **rejected**

- Contradicts minimize-AI; still needs deterministic Sectionize; live Gemini unproven (**NOT_EXECUTED**).

### Alternative C — Post-hoc merge Custom siblings back into Skills — **rejected as primary**

- Possible follow-up heuristic; harder and unnecessary if soft promotion stops shredding.

### Alternative D — Catalog aliases for Backend/Frontend/… — **rejected**

- Those are skill **groups**, not section types; aliasing to Skills would break Sectionize (each group would start a new Skills section). Soft-promotion fix is the correct seam.

---

## Impacted contracts

### Approved — must respect

- ADR-0001 / `shared/cv-section-catalog/cv-section-catalog.json`
- P1 Custom catch-all + notice policy
- Link integrity
- REST import summary DTO shape
- Google Gemini HTTP only when gate fires

### Proposed (internal / non-breaking)

| Change | Kind | Approval |
|---|---|---|
| Soft promotion allowed only under Summary current type | implementation | **no operator gate** (bugfix of prior soft-heading scope) |
| AI gate hollow-typed / empty Skills signal | implementation | **no** AI-always; still gated |
| Residual Technologies line containment tweak | optional polish | no contract change |
| Experience “Label: prose” → bullet | optional polish | no contract change |
| Catalog change for About Me | **not recommended** now | would need operator if pursued |

---

## Migration / sequencing

### Phase A — Soft heading scope (`backend-engineer`) — **primary**

1. Update `LooksLikePromotableHeading` to return false when current mapped type is Skills, Contact, Custom, Experience, Education, or Projects (equivalently: allow only when current type is Summary).
2. Tests: sample-shaped Skills two-line groups must stay inside Skills; Languages proficiency lines must not become headings; keep “Tech stack” promotable under `summary`; keep Experience block.
3. Fixture/regression: run sample PDF through Extract→Heuristic→Residual; assert Skills entry count ≥ 1 and no Custom heading ∈ {Backend, Frontend, …}.

**DoD:** Probe-equivalent sample → typed Skills groups (`title` + `techStack`); Languages body contains proficiency lines; AI may still SkipAi if structure strong.

### Phase B — Hollow-typed AI gate (`backend-engineer`, small; `ai-llm-engineer` consult)

1. Gate `CallAi` when Skills (or other typed) section exists with zero contentful entries **and** raw text has skills cues / orphaned skill-like Custom siblings from Sectionize.
2. Keep “do not call solely because catch-all used.”
3. Unit tests for Skip vs Call matrix.

**DoD:** If soft fix regresses or PDF has no clear Skills heading, gate can still request one Gemini pass. Live Gemini still optional / may be **NOT_EXECUTED** in CI.

### Phase C — Prompt/schema polish (`ai-llm-engineer`) — only if Phase B needs it

- Emphasize: skill group labels are entry titles inside Skills, not new sections; do not invent section boundaries for Backend/Frontend.
- No vendor change. Do not claim live HTTP success without execution.

### Phase D — Entry polish (`backend-engineer`, optional)

- Experience: treat non-bullet `Title Case Label: prose` lines as bullets when inside dated entry.
- Education: split additional `Title | Institution` lines without dates into separate entries.
- Residual: improve Technologies-line containment (nested commas).

### Phase E — QA (`qa-engineer`)

- Matrix: sample.pdf Skills/Languages; soft under Summary still works; Experience/Projects unchanged; gate on/off; P1 catch-all; URL integrity regression.
- Live Gemini: mark **NOT_EXECUTED** unless secrets + HTTP run.

Delivery: skills chain `to-spec` → `to-tickets` → `implement` (+ `tdd`) → `code-review` as Principal sequences.

---

## Ownership recommendations

| Concern | Primary | Secondary |
|---|---|---|
| Soft heading + Skills/Languages Sectionize | backend-engineer | qa-engineer |
| AI gate hollow-typed signal | backend-engineer | ai-llm-engineer |
| Gemini prompt tweak (Phase C) | ai-llm-engineer | backend-engineer |
| This design | architecture-engineer | principal-software-architect |

No ownership-matrix dual-own change required.

---

## Risks and open decisions

### Risks

1. Soft promotion only under Summary may leave mid-CV unknown headings inside the previous typed section — residual Custom still captures orphans (P1).
2. Hollow-typed gate may increase Gemini calls on messy CVs — still ≤1 trip; ForceAi remains ops escape hatch.
3. Live Gemini quality unknown (**NOT_EXECUTED**).

### Open decisions (operator)

None blocking Phase A. Escalate only if Principal wants AI-always or catalog About Me remapping.

---

## Security / tenancy

Unchanged: user-scoped import; no new secrets; Gemini only when gate fires.

---

## Non-goals

- AI-first pipeline
- New Unplaced section type
- OCR / new vendors
- Frontend/extension changes
- Reopening P1 or link-integrity bindings
- Claiming live Gemini results without execution
