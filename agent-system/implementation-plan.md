# Implementation Plan — Stable CV builder v1 (simple templates)

**Status:** `APPROVED`  
**Request:** FlowCV-inspired builder — amended to a **small stable first version** (not feature parity)  
**Mode:** BROWNFIELD  
**Created:** 2026-07-31  
**Amended:** 2026-07-31 (user: ~3 simple templates; start simple; no full FlowCV copy)  
**Approved:** 2026-07-31 (user accepted D1–D6 amended defaults; Classic / Modern / Minimal template set)  
**Architect:** principal-software-architect  
**Spec:** `agent-system/project-specification.md` (APPROVED)  
**Library:** 0.5.4 (pack aligned)

---

## 1. Intent

Ship a **stable first version** of the CV builder export/preview path:

- **About 3 simple Templates** (not 5+, not 50, not a FlowCV clone).
- Preview and download use the **same HTML** so what you see is what you get.
- Keep existing strengths: Structured CV edit, PDF import, Blank CV, AI assist, photo, project import ([ADR-0002](../docs/adr/0002-cv-builder-sole-surface.md)).
- **Do not** chase FlowCV feature parity (design studio, cover letters, multi-version, RTL, huge gallery).

### v1 success

A user can pick one of **3 simple Templates**, edit their Structured CV on `/cv-builder`, and download a PDF that matches the on-screen preview. Template choice is remembered. The path is tested and boringly reliable.

---

## 2. Current baseline (verified)

| Area | Today |
|---|---|
| Templates | 5 layouts; Classic = QuestPDF, 2–5 = HTML→PDF |
| Preview | Angular CSS mock — can diverge from PDF |
| Template preference | `sessionStorage` only |
| Design / theme UI | None |
| One Structured CV per user | Yes (keep) |

Prior phase 1–2d handoffs under legacy `agent-system/handoffs/cv-builder-phase*` are historical only.

---

## 3. v1 scope (in / out)

### In (v1)

| Item | Notes |
|---|---|
| **3 simple Templates** | e.g. Classic one-column, Modern two-column, Minimal ATS — exact names/IDs in M0 |
| **Consolidate catalog** | Reduce from 5 → 3; one HTML pipeline for all three (drop dual QuestPDF vs HTML split for v1 supported set) |
| **Preview = export** | Shared HTML → iframe preview + Puppeteer PDF |
| **Persist template (+ max pages if already used)** | On CV document, not only sessionStorage |
| **Regression tests** | Fixture CV through preview HTML + export for each of the 3 |

### Out of v1 (explicitly deferred)

| Item | Why |
|---|---|
| More templates / gallery expansion | User: not needed |
| Design theme panel (colors/fonts/margins) | Complexity; add later if wanted |
| A4 vs Letter picker | A4-only is fine for v1 |
| Drag-drop structure polish | Existing ↑↓ reorder stays |
| Multi-version CVs | Domain change |
| Cover letters | New product |
| RTL | Niche |
| FlowCV UI clone / payments / watermarks | Non-goals |

---

## 4. Decision gates

| ID | Decision | **Amended default** | Blocks |
|---|---|---|---|
| D1 | Scope | **Stable v1 only** (3 templates + fidelity + persist). No FlowCV parity program. | Plan shape |
| D2 | Preview architecture | **Single HTML source** for all 3 templates (preview + PDF). Recorded as [ADR-0006](../docs/adr/0006-cv-export-html-puppeteer-pipeline.md). | M1 |
| D3 | Theme / design tokens | **Defer** — templates ship with fixed simple styling. | — |
| D4 | Multi-version CVs | **Defer** | — |
| D5 | Cover letters | **Defer** | — |
| D6 | Template count | **Exactly 3** simple templates for v1. | M1 |

**D1–D6 accepted** on 2026-07-31 with the amended defaults in this document.

---

## 5. Milestones

### M0 — Freeze the 3 templates (planning)

**Objective:** Name the 3 layouts and migration rule for users who had template ids 4–5.  
**Owners:** principal-software-architect (+ ui-ux-designer if naming/layout choice needed).  
**Produces:** Written list (id → name → one-column vs two-column); map old ids → new ids; optional GitHub Issue.  
**Does not:** Change application code.

**Proposed set (amend freely at approval):**

| New id | Role | Notes |
|---|---|---|
| 1 | Classic | Simple one-column, ATS-friendly |
| 2 | Modern | Simple two-column |
| 3 | Minimal | Clean one-column, sparse |

**Migration:** Unknown/retired template ids fall back to Classic (1).

**Completion criteria:**

- [ ] Three names + layouts agreed
- [ ] Old id → new id fallback documented
- [ ] Issue filed (BRIDGE) linking this plan — optional but preferred

**Approval gate:** Human approve this plan + the three-template set.

---

### M1 — Three HTML templates + preview/export fidelity

**Objective:** All 3 templates render from the same HTML pipeline; builder preview matches PDF.  
**Owners:** architecture-engineer (contract) → backend-engineer + frontend-engineer; qa-engineer.  
**Paths:**

- `api/ApplyVault.Api/Services/CvDocuments/HtmlExport/`
- `api/ApplyVault.Api/wwwroot/cv-export-templates/`
- `frontend/.../cv-export-template-preview/` / `cv-builder-page/`
- FE `cv-export-template.model.ts`; BE `CvExportHtmlTemplateCatalog`

**Work:**

1. Freeze HTML placeholder contract for the 3 templates.
2. Implement/trim templates to the simple set; remove or stop exposing the extra two from the gallery.
3. Preview via authenticated HTML (iframe/srcdoc); PDF via existing Puppeteer path on that HTML.
4. Retire QuestPDF-as-default for the v1 set (or keep QuestPDF only if one template still needs it — prefer all-HTML for simplicity).
5. Update gallery to show only the 3.
6. Tests: each template id exports; preview auth/tenancy; mapper smoke with fixture CV.

**Dependencies:** M0.  
**Risks:** XSS in preview iframe (sandbox + sanitize); visual polish temptation (keep templates simple).  
**Completion criteria:**

- [ ] Gallery shows exactly 3 templates
- [ ] For each, preview content matches PDF on a fixed fixture (manual checklist + automated smoke)
- [ ] Invalid/legacy template id maps to Classic
- [ ] No secrets in preview responses

---

### M2 — Persist template preference

**Objective:** Selected `templateId` (and existing `maxPages` if kept) stored on the CV document.  
**Owners:** backend-engineer, frontend-engineer, qa-engineer.  
**Work:**

1. Small EF/API field(s) on current CV document.
2. Builder load/save; stop relying solely on `sessionStorage` (may keep as cache).
3. Integration test: prefs round-trip + tenancy.

**Dependencies:** M1 catalog ids stable (can start API in parallel once ids frozen in M0).  
**Completion criteria:**

- [ ] Choice survives new session/browser
- [ ] Export uses persisted template

---

### M3+ — Later (not v1)

Only after v1 is stable and explicitly requested:

- Minimal theme (e.g. one accent color)
- Letter page size
- More templates
- Structure UX polish
- Multi-version / cover letters / RTL

---

## 6. Dependency graph

```text
M0 (agree 3 templates + fallbacks)
 └── M1 (HTML ×3 + preview = PDF)
      └── M2 (persist template)
           └── v1 DONE
```

No parallel feature tracks for v1.

---

## 7. Agents (for later C/D)

| Milestone | Primary | Review |
|---|---|---|
| M0 | principal-software-architect | Human |
| M1 | architecture-engineer → backend-engineer, frontend-engineer | qa-engineer |
| M2 | backend-engineer, frontend-engineer | qa-engineer |

Handoffs: `handoffs/active/<task-id>/`; scratch: `scratch/<task-id>/`.

---

## 8. Contracts and constraints

- ADR-0001 catalog + ADR-0002 sole surface / Template = layout
- Tenancy on all CV APIs
- BRIDGE: GitHub Issues + `.agents/skills` for implementation tickets when coding starts
- No payments; no FlowCV brand clone
- Do not overwrite `CONTEXT.md` / ADRs without authorization (v1 may need only minor Template-count copy, not a Theme ADR)

---

## 9. Risks

| Risk | Mitigation |
|---|---|
| Scope creep back to “FlowCV parity” | This plan’s out-list; reject add-ons until v1 ships |
| Dual renderer kept “for Classic” | Prefer all 3 on HTML in M1 |
| Breaking users on old template ids | Documented fallback to Classic |
| Over-designing the 3 layouts | “Simple” is a requirement — review for restraint |

---

## 10. Validation

- API: export each of 3 ids; legacy id fallback; prefs tenancy (M2)
- FE: gallery length === 3; pick → edit → download smoke
- Manual: Blank CV + PDF import paths still work; project import unchanged
- Claim CI green only with observed runs

---

## 11. Non-goals (v1)

- FlowCV feature copy or UI clone  
- Template count above 3  
- Theme/design studio  
- Cover letters, multi-version CVs, RTL  
- Full layout designer  

---

## 12. After approval

1. Mark plan **APPROVED** (optionally rename the three templates).  
2. Next operate step: **B** (task mapping) or **C** if paths are clear enough to start M1.  
3. Do not start deferred M3+ without a new approval.

---

## 13. Program completion (v1)

1. Exactly **3** simple Templates in the gallery.  
2. Preview matches PDF for each on a fixture CV.  
3. Template preference persists on the document.  
4. Legacy ids fall back safely.  
5. CI evidence for API + FE changes recorded.

---

## Approval

**APPROVED** 2026-07-31. Template set locked: Classic (1), Modern (2), Minimal (3); legacy ids → Classic.

**M0 status:** Complete via approval.  
**M1 status:** `REQUEST COMPLETE WITH DOCUMENTED LIMITATIONS` (2026-07-31) — archive `handoffs/archive/cv-builder-v1-m1-2026-07-31/summary.yaml`.  
**M2 status:** `REQUEST COMPLETE WITH DOCUMENTED LIMITATIONS` (2026-07-31) — archive `handoffs/archive/cv-builder-v1-m2-2026-07-31/summary.yaml`.  
**Program v1 (M0–M2):** Complete with documented limitations (see M1 + M2 archives).
