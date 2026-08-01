# Structured CV — Update with AI (propose → review → approve)

**Task:** `cv-update-propose-approve-2026-08-01`  
**Mode:** Implementation design (locked by plan)  
**Date:** 2026-08-01  
**ADR:** [docs/adr/0011-cv-update-ai-propose-approve.md](../docs/adr/0011-cv-update-ai-propose-approve.md)

---

## Locked decisions

| ID | Binding |
|---|---|
| **D1** | Replace Assist immediate persist for **Update CV with AI** and **Apply selected** with one shared propose → Approve/Discard path |
| **D2** | Ephemeral `POST .../ai-update-propose`; never `SaveStructuredAsync` |
| **D3** | Assist shows change bullets + Current vs Proposed per affected section |
| **D4** | Approve = FE `mergeAssistStructuredUpdate` + existing PUT save; never `ai-update` |
| **D5** | Leave legacy `ai-update` endpoint; Assist must not call it for Update / Apply selected |

Still in force: ADR-0001, ADR-0002, ADR-0004 (Summary regenerate), Google Gemini HTTP only.

---

## Target flow

```text
Assist Update / Apply selected
  → POST .../ai-update-propose (ephemeral; JWT)
       → load Structured CV → Gemini update client → normalize sections
       → return { focusSectionIds, changeBullets, proposedSections }
       → DO NOT SaveStructuredAsync
  → UI: What changed + Current | Proposed panes
       → Discard → clear in-memory proposal
       → Approve → merge by section id into local draft → facade.save PUT
```

---

## Contracts

**Request:** `UpdateCvStructuredWithAiRequest` (`instructions`, optional `sectionIds`).

**Response:** `CvUpdateProposalDto` — `documentId`, `focusSectionIds`, `changeBullets`, `proposedSections` (read DTOs; ids preserved when model returned them).

AI JSON schema extended with optional `changeBullets` (3–5). Server normalizes length/count like Summary propose; if empty, derive short bullets from focus/changed headings.
