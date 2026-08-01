# Implementation plan — CV builder edit architecture simplification

**Status:** `APPROVED` (human — 2026-08-01; “approve to all”)  
**Task id:** `cv-builder-edit-arch-simplify-2026-08-01`  
**Author:** architecture-engineer (operate option D — design/plan only)  
**Date:** 2026-08-01  
**Approved decisions:** see §7 (all G1/G2/G3 defaults locked for implementation)

**Does not replace** `agent-system/implementation-plan.md` (stable CV builder v1 M0–M2 export program — complete with documented limitations).

**Design summary:** `agent-system/scratch/cv-builder-edit-arch-simplify-2026-08-01/architecture-design-summary.md`

---

## 1. Objective

Simplify the **Structured CV edit path** on `/cv-builder` end-to-end: clearer module boundaries, less dead code, fewer redundant guards, and a maintainable draft/persist lifecycle — without rewriting the feature or violating ADR-0001 / ADR-0002 / ADR-0003.

**Out of scope**
- Backend REST / Puppeteer / wwwroot template redesign
- Reviving `/my-cv` or Content drawer as primary editor
- Making Angular canvas pixel-equal to export HTML (ADR-0003 accepts approximation)
- Payments, extension, non-CV features
- Overwriting `agent-system/implementation-plan.md`

---

## 2. Current → target (one page)

| Today | Target |
|---|---|
| God page owns draft machine + all panels | Thin shell + extracted panels + `CvEditSession` |
| Dual normalize (facade + page mount) | Normalize once at structured ingress; explicit persist policy if needed |
| Orphaned Content stack still in tree | Removed after verification |
| Document facade mixes unused PDF-modal / reimport / delete UI APIs | Trimmed or deprecated surface; optional `CvExportPreview` extract |
| 4 panel booleans + copy-paste mutex | Single `activePanel` signal |
| Assist injects facade + receives props | Presentational panel only |

---

## 3. Principles

1. **Smallest safe increment** — each milestone shippable alone.
2. **Characterization before move** — draft/generation behavior gets tests before relocation.
3. **Behavior-neutral extracts first** — HTML/TS splits before logic changes.
4. **Honor ADRs** — sole surface; edit canvas ≠ export HTML.
5. **BRIDGE delivery** — after approval: Issues/`to-tickets` → `implement` + `tdd` → `code-review` → QA.
6. Label speculative extras `ARCHITECT_PROPOSED`.

---

## 4. Approval gates

| Gate | Required before |
|---|---|
| **G0 — Human approves this plan** | Any C implementation |
| **G1 — Dead-code purge list confirmed** (esp. reimport/delete/original/PDF-modal wrappers; catalog service; sampleMode) | M1 merge |
| **G2 — Lifecycle smoke** (authenticated edit → save → Check export → PDF) | Calling M3/M4 “done” |
| **G3 — Optional ADR** | Only if Principal asks for domain-memory citation beyond this plan |

---

## 5. Milestones

### M0 — Plan approval (no code)

**Owner:** Principal + human  
**DoD:**
- [x] This plan marked approved (status line updated) or rejected with direction
- [x] Open decisions in §7 resolved or explicitly deferred
- [x] GitHub Issue filed (`ready-for-agent`) with milestone checklist — BRIDGE

**Dependencies:** none  
**Parallel:** none  
**Completed:** 2026-08-01 (Principal after human “approve to all”)  
**Issue:** https://github.com/PrimusInterParess/ApplyVault/issues/2 (`ready-for-agent`)

---

### M1 — Dead code & dead style purge (behavior-neutral)

**Owner:** frontend-engineer  
**QA:** smoke unit/build only  

**Remove or quarantine (default: delete FE-only orphans):**
1. `components/cv-structured-preview/`
2. `components/cv-structured-entry-editor/`
3. `components/cv-markdown-field/`
4. `utils/cv-entry-fields.util.ts`
5. `utils/markdown-selection.util.ts` (+ spec) if unused after #3
6. `utils/markdown.util.ts` re-export if unused
7. Page SCSS: `.cv-builder__drawer--content`, `.cv-builder__content-editor` (+ related orphans)
8. Confirm no route/template references remain

**Facade trim (behind G1):**
- Remove or mark private/deprecated unused: `startBlank()` (empty), `reimportStructured()`, `delete()`, `downloadOriginal()`, PDF blob modal cluster (`previewFormatted` / `downloadFormatted` / `downloadFormattedFromPreview` / `closePreview` / related signals) **if product confirms no near-term UI**.
- Keep `cv-document-api.service` methods unless product retires REST.

**Optional same milestone:**
- Delete unused `CvSectionCatalogService` **or** ticket “wire catalog” separately (do not half-wire in purge).
- Collapse `saveSectionOrder` if still unused after grep.

**DoD:**
- [x] `rg` shows no remaining imports of deleted symbols
- [x] Targeted Karma + development build green
- [x] No intentional UX change

**Status:** `REQUEST COMPLETE` for M1 (2026-08-01) — handoff `handoff-frontend-engineer-m1.yaml` reconciled READY.  
**Evidence:** Karma 51/51; `ng build --configuration development` green; API REST methods retained.

**Risk:** Low. Accidental delete of something still dynamically referenced — mitigate with full-feature grep + build.

**Rollback:** revert single purge PR.

---

### M2 — Panel extraction (behavior-neutral)

**Owner:** frontend-engineer  
**Optional parallel:** ui-ux-designer for Projects popover → drawer parity (visual only)

**Extract presentational components** (move markup + local handlers; page keeps wiring):
1. `cv-builder-empty-start`
2. `cv-builder-structure-panel`
3. `cv-builder-projects-panel`
4. `cv-builder-check-export` (modal + html-preview binding)
5. Replace 4 booleans with `activePanel` (or keep booleans internally but one setter API)

**Assist panel cleanup (light):**
- Stop injecting `CvStructuredFacade` into assist; pass `updatingWithAi`, errors, busy as inputs (or a single `assistViewModel` input).

**DoD:**
- [x] Page HTML shrinks materially; behavior unchanged
- [x] Existing page specs updated; build green
- [x] Panel mutex still exclusive

**Status:** `REQUEST COMPLETE` for M2 (2026-08-01) — handoff `handoff-frontend-engineer-m2.yaml` reconciled READY.  
**Evidence:** Karma 52/52; development build green; `activePanel` + four panel components present; assist has no facade inject.

**Risk:** Low–medium (a11y focus for Check export Escape/return). Preserve focus helpers with the modal extract.

**Parallel with M1?** Only after M1 lands or in a branch that doesn’t fight deletes.

---

### M3 — Extract `CvEditSession` (draft lifecycle move)

**Owner:** frontend-engineer  
**QA:** qa-engineer (unit + authenticated checklist)

**Create** `data-access/cv-edit-session.ts` (name flexible) owning:
- `inlineDraft` / effective `sections`
- `editGeneration` + save-generation clear rules (port from page effects)
- `apply(edit: CvTemplateInlineEdit)` / structure helpers
- `scheduleSave` / `flushSave` / `persistSections`
- `saveStatus` computed
- project-import busy coordination currently on page (or keep import orchestration on page calling session.apply + save)

**Page keeps:** file inputs, panel shell, calling session + facades.

**Pre-step (required):** characterization tests for:
- draft clear on matching successful generation
- no clear when newer local edit exists
- document-id structured load gate still on page/facade (do not regress prefs echo reload)

**DoD:**
- [x] Page no longer owns debounce timer / generation counters
- [x] Specs cover generation clear + coalesce interaction at session or facade boundary
- [x] G2 smoke executed or explicitly deferred with Principal acknowledgment

**Status:** `REQUEST COMPLETE` for M3 (2026-08-01) — code + G2 reconciled READY.  
**Evidence:** Karma 56/56; development build green; `CvEditSession` owns draft/debounce/generations.  
**G2:** `PASSED` — checklist `agent-system/scratch/cv-builder-edit-arch-simplify-2026-08-01/g2-authenticated-smoke-checklist-m3.md`

**Risk:** **High** — historical wipe/sticky-draft class of bugs. Do not combine with normalize collapse in the same PR.

**Dependencies:** M2 recommended (smaller page) but not strictly required.

---

### M4 — Collapse dual normalize & busy gates

**Owner:** frontend-engineer  
**Depends on:** M3 (session owns when persist-normalize runs)

**Work:**
1. Prove `normalizeSectionsForEditing` idempotence with tests (or fix until idempotent).
2. Single ingress: only `CvStructuredFacade.hydrateForContentEditing` / `setStructured`.
3. Replace page `ensureContentEditShape` with either:
   - **(A)** delete if normalized server state never needs re-PUT, or
   - **(B)** one-shot `persistIfNormalizedDiffersFromRawServer` inside structured facade after load (explicit, tested).
4. Stop double `hydrateStructuredDocument` at upload/blank call sites — pass raw DTO into `setStructured`.
5. Single `editBusy` / `canMutateStructured` computed used by Projects, Assist, Structure.
6. Simplify unused `savingSectionOrder` if order-only API remains unused.

**DoD:**
- [x] One normalize path documented in code comment pointing to ADR-0003 edit shapes
- [x] No duplicate busy-gate copy-paste on page
- [x] Contact normalize specs still green (do not regress Contact absorb/dedupe)

**Status:** `REQUEST COMPLETE` for M4 (2026-08-01) — handoff `handoff-frontend-engineer-m4.yaml` reconciled READY.  
**Choice:** Option **(A)** delete `ensureContentEditShape` (normalize idempotent; second pass was no-op).  
**Evidence:** Karma 62/62; development build green. Authenticated re-smoke skipped (M3 G2 already PASSED; Option A behavior-neutral).

**Risk:** Medium — Contact/import shapes. Keep contact util as the seam.

---

### M5 — Optional: split export preview from document facade

**Owner:** frontend-engineer  
**Status:** `ARCHITECT_PROPOSED` — do only if M1–M4 leave `cv-document.facade.ts` still unwieldy

**Extract** `CvExportPreviewService` (or facade slice):
- `refreshExportHtmlPreview`, srcdoc/notice/compact, clear
- `downloadFormattedFile`
- (deleted PDF blob modal already gone in M1)

Document facade retains: load/upload/blank/prefs/photo.

**DoD:** Check export + Download PDF still work; no contract change.

**Risk:** Low if extract is move-only.

---

### M6 — Optional cleanups (defer by default)

| Item | Note |
|---|---|
| Delete `sampleMode` / `createSamplePreviewSections` | Only if product confirms empty-state gallery won’t return soon |
| Wire `CvSectionCatalogService` into edit chrome | Larger feature; separate Issue |
| Template preview HTML split Modern vs Minimal | Only if file size hurts review velocity |
| Projects popover → full drawer | UX; pairs with M2 |
| Feature-scoped providers for edit-session | If root singleton draft leaks across navigations |

---

### M7 — Hardening & close

**Owner:** qa-engineer (+ frontend fixes)  
**DoD:**
- [x] Unit/integration evidence recorded for draft + normalize + panel extracts
- [x] Authenticated checklist: blank start, PDF replace confirm, inline edit autosave, Structure reorder/remove, Projects import, Assist update, Check export vs canvas, Download PDF, template switch Modern↔Minimal mid-edit
- [x] Handoff archive + triage labels on Issue
- [x] Residual limitations documented (especially if G2 deferred)

**Status:** `REQUEST COMPLETE WITH DOCUMENTED LIMITATIONS` (2026-08-01).  
**Notes:** Initial Assist FAIL (section wipe) → FE merge fix → Assist re-verify PASS. Projects import NOT_EXECUTED (no summaries). M5/M6 deferred. API Assist still full-replaces server-side before FE merge repair.

---

## 6. Dependency graph

```text
M0 approval
 └── M1 dead-code purge
      └── M2 panel extract (+ optional UX Projects drawer)
           └── M3 edit-session extract ──requires──> characterization tests
                └── M4 normalize + busy-gate collapse
                     └── M5 optional export split
                          └── M7 hardening
M6 optional cleanups can parallel after M2 if non-overlapping files
```

**Parallel work after M0:**
- ui-ux-designer: Projects drawer visual spec (no FE block)
- product-manager: G1 decisions (keep reimport/delete UI APIs?)
- qa-engineer: write smoke matrix early (doesn’t need code)

---

## 7. Open decisions (human) — RESOLVED 2026-08-01

Human: **approve to all**. Locked for implementation:

1. **Unused document APIs:** **Delete** unused FE wrappers now (reimport / delete CV / download original / PDF blob modal cluster). Keep `cv-document-api.service` REST methods unless a later product task retires them.
2. **Catalog service:** **Delete** unused `CvSectionCatalogService` (catalog-driven edit chrome = separate future Issue if needed).
3. **sampleMode / pick remnants:** **Delete** (empty-state gallery return = new work, not keep-dead-code).
4. **G2 smoke:** **Required** before calling M3/M4 complete (authenticated edit → save → Check export → PDF).
5. **New ADR?** **No** — this plan + ADR-0003 suffice.

---

## 8. Risks & mitigations

| Risk | Mitigation |
|---|---|
| Reintroduce edit wipe | M3 characterization tests; don’t mix with M4; G2 smoke |
| Contact channel dup regressions | Keep `cv-contact-channels.util` tests green; no logic move in M1/M2 |
| Over-extraction churn | Stop after M4 unless M5 clearly needed |
| Scope creep into export HTML fidelity | Out of scope; track under separate export issues |
| Dual plans confuse agents | This file is edit-architecture only; v1 export plan stays historical |

---

## 9. Completion criteria (program)

Program complete when:
1. Orphaned Content edit stack removed from `cv-projects`.
2. Draft/persist lifecycle lives in one edit-session module with tests.
3. Page is primarily composition/shell.
4. Normalize + busy policy not duplicated.
5. ADR-0002/0003 behavior preserved.
6. QA evidence recorded; open limitations explicit.
7. No Structured CV REST contract break.

---

## 10. Recommended next delegation (after G0)

1. **frontend-engineer** — M1 (then M2) with task-delegation bound to this plan.
2. **qa-engineer** — independent verification per milestone.
3. **ui-ux-designer** — optional Projects drawer (parallel).
4. **architecture-engineer** — only if G3 ADR requested or mid-flight design conflict.

Do not assign backend-engineer unless G1 retires REST endpoints (not recommended as part of simplification).
