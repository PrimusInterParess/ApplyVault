# Structured CV — Regenerate Summary (propose → review → approve)

**Task:** `cv-summary-regenerate-approve-2026-08-01`  
**Agent:** architecture-engineer  
**Mode:** Design only (no application code in this pass)  
**Date:** 2026-08-01  
**Locked decisions:** D1–D4 (human "go")

---

## Architecture design summary

- **Request:** new-feature-shape (brownfield Assist surface)
- **Status:** COMPLETE — READY for implementer sequencing
- **Recommendation:** Add an **ephemeral** `ai-summary-propose` Gemini endpoint (evaluation-style: never persists) that returns proposed Summary prose + change bullets; Assist shows current vs proposed side-by-side; **Approve** patches only the Summary section in local Structured CV and persists via the existing PUT save path; **Discard** clears in-memory proposal. Do **not** reuse `POST .../ai-update` for regenerate (it persists immediately).

---

## Locked product decisions

| ID | Binding |
|---|---|
| **D1** | Dedicated **Regenerate summary** control in Assist panel (primary entry) |
| **D2** | Side-by-side **current vs proposed** Summary text + short AI **what changed** bullets |
| **D3** | AI context = full Structured CV + Contact section + AppUser `DisplayName`/`Email` when available + optional free-text instructions |
| **D4** | **Approve** persists **Summary section only**; **Discard** drops proposal; never wipe other sections |

Still in force (do not reopen): ADR-0001 catalog; ADR-0002 CV builder sole surface; existing Assist `ai-update` immediate Update semantics; Supabase JWT on cv-documents APIs; Google Gemini HTTP only.

---

## Current state (evidence)

### Assist surface (FE)

| Concern | Path | Behavior today |
|---|---|---|
| Assist panel UI | `frontend/.../cv-builder-assist-panel/` | Three blocks: **Update with instructions**, **Suggestions**, **Evaluate CV**. No regenerate-summary control. |
| Wire-up | `cv-builder-page.component.ts` | `updateStructuredWithAi` → flush save → `facade.updateWithAi`; suggestions / evaluate similar. |
| Immediate AI update | `cv-structured.facade.ts` `updateWithAi` | Calls `POST .../ai-update`; merges by section id; may corrective-save. **Persists on server before user reviews text.** |
| Assist merge util | `cv-structured-assist-merge.util.ts` | Documents that API persists full/partial body; FE merges as defense in depth. |
| Ephemeral eval pattern | facade `evaluateQuality` + `POST .../ai-evaluation` | In-memory only; does not mutate Structured CV — **best analog for propose**. |

### API / AI

| Concern | Path | Behavior today |
|---|---|---|
| Persist-on-call update | `CvDocumentsController` `POST current/structured/ai-update` | Auth via `GetRequiredUserAsync`; delegates to update service. |
| Update service | `CvStructuredUpdateService.cs` | Gemini update → `MergeAssistUpdate` → **`SaveStructuredAsync` immediately**. |
| Gemini update client | `GoogleAiCvStructuredUpdateClient.cs` | Full Structured CV JSON in prompt; response = sections document (not Summary-only). |
| Merge before persist | `CvStructuredUpdateNormalizer.MergeAssistUpdate` | Focus / omit-preserve (aligned with FE); still saves merged full document. |
| Ephemeral siblings | `CvStructuredSuggestionsService`, `CvStructuredEvaluationService` | Load structured CV, call Gemini, **return DTO without save**. |
| Summary catalog | `shared/cv-section-catalog/cv-section-catalog.json` | Type `Summary`; entry field `body` (`importKey: summary`). Flat API projection uses entry `summary`. |
| Contact | same catalog + import grounding helpers | First-class section; identity channels as entries. |
| AppUser identity | `AppUserEntity` (`Email`, `DisplayName`) | Available on authenticated user for D3; not currently injected into update prompts. |

### Domain / ADRs

- `CONTEXT.md` — Structured CV / Section / Entry / catalog vocabulary.
- ADR-0001 — catalog + `FieldsJson`; Contact first-class.
- ADR-0002 — CV builder sole surface (Assist stays on `/cv-builder`).

### Gap vs D1–D4

Today the only way to AI-rewrite Summary is **Update with instructions** (optionally focusing the Summary chip) or applying a suggestion — both route through **`ai-update`**, which **persists before** the user can compare current vs proposed or discard. There is no dedicated regenerate control, no side-by-side review, and no Summary-only approve gate.

---

## Target state

```text
Assist panel
  → [Regenerate summary] (+ optional instructions)
       → POST .../ai-summary-propose  (ephemeral; JWT)
            → load Structured CV
            → build prompt: full CV + Contact excerpt + AppUser identity + instructions
            → Gemini → { proposedSummaryText, changeBullets[] }
            → respond; DO NOT SaveStructuredAsync
       → UI: current | proposed + bullets
            → Discard → clear in-memory proposal only
            → Approve → patch Summary section in local draft
                      → existing facade.save / PUT structured
                      → Summary content only changed; other sections unchanged
```

### UX (Assist — D1 / D2)

New Assist block (above or below Update-with-instructions; prefer **near top** so regenerate is discoverable without competing with Evaluate):

1. Title: **Regenerate summary**
2. Optional instructions textarea (placeholder e.g. "Emphasize backend leadership; keep under 80 words.")
3. Primary button: **Regenerate summary** (disabled while proposing / when no structured sections)
4. After success: two-column (stack on narrow) **Current** / **Proposed** read-only prose + **What changed** bullet list (3–5)
5. Actions: **Approve** | **Discard**
6. Errors: inline alert (same Assist error pattern as other blocks)
7. Keep existing Update / Suggestions / Evaluate unchanged and independent

### Persist path (D4)

**Recommended:** Approve is a **client-side Summary patch + existing save**.

1. Resolve Summary section in local `structured()` (by `sectionType === 'Summary'`; if multiple, prefer first by `sortOrder`).
2. Set single-entry Summary prose into the flat `summary` field (catalog `body` / importKey `summary`) on the existing entry id when present; preserve section id, heading, sortOrder, and other entries untouched.
3. If no Summary section exists: append one Summary section with one entry (starter-shaped), using catalog default heading — do not remove other sections.
4. Call existing `facade.save(sections, summarySectionId)` (same path as manual edit).
5. Clear proposal state after successful save enqueue.

**Do not** call `ai-update` on Approve.

**Rejected for Approve:** new server `ai-summary-apply` that loads DB and patches Summary — risks clobbering unsaved local edits to other sections that the builder routinely holds in draft before save. FE save already owns that draft truth.

### Propose API (proposed contract)

```http
POST /api/cv-documents/current/structured/ai-summary-propose
Authorization: Bearer <Supabase JWT>
Content-Type: application/json
```

**Request (proposed):**

```json
{
  "instructions": "optional free text; empty or omit allowed"
}
```

- `instructions` optional (unlike `ai-update`, which requires non-blank instructions). Empty ⇒ regenerate from CV + identity alone.
- No `sectionIds` — target is always Summary.

**Response (proposed):**

```json
{
  "documentId": "<guid>",
  "summarySectionId": "<guid|null>",
  "currentSummaryText": "<string>",
  "proposedSummaryText": "<string>",
  "changeBullets": ["...", "..."]
}
```

| Field | Rules |
|---|---|
| `summarySectionId` | Id of existing Summary section, or `null` if absent (Approve may create) |
| `currentSummaryText` | Server-extracted current Summary prose (first Summary entry `summary` / body); `""` if missing |
| `proposedSummaryText` | Non-empty trimmed prose; reject empty AI output with 400 |
| `changeBullets` | 1–5 short strings; trim; drop blanks; cap length reasonably (e.g. 200 chars each) |

**Service shape:**

- `ICvStructuredSummaryProposeService` + `CvStructuredSummaryProposeService`
- Load via `ICvStructuredDocumentService.GetStructuredAsync` (same 404 / empty-sections guards as evaluation)
- Call new `ICvStructuredSummaryProposeAiClient`
- **Never** call `SaveStructuredAsync`
- Inject `AppUserEntity.DisplayName` / `Email` into the AI client (D3)

**AI client / options (ai-llm-engineer):**

- New `GoogleAiCvStructuredSummaryProposeClient` + response schema (JSON: `proposedSummaryText`, `changeBullets`)
- New `CvSummaryProposeAiOptions` (`SystemPrompt`, `UserPromptTemplate`) — do not overload `CvUpdateAiOptions`
- Prompt must:
  - Use **facts only** from Structured CV + Contact + provided AppUser identity + user instructions
  - Not invent employers / metrics / degrees
  - Prefer Contact name/email over AppUser when both present and they conflict; treat AppUser as fallback identity
  - Output **Summary prose only** (no markdown headings / bullet prefixes inside the summary body unless user asks)
  - Emit **change bullets** describing deltas vs `currentSummaryText` (or "created initial summary from CV" when current empty)
- Provider: Google Gemini HTTP (`GoogleAi:Enabled` / ApiKey / Model) — same verified stack

**Catalog / ADR-0001:** no catalog schema change required. Summary remains `body` ↔ flat `summary`.

---

## Options considered

### Recommended: Ephemeral propose + FE Approve-via-save

- Matches evaluation/suggestions non-persist pattern
- Preserves D4 review gate
- Avoids `ai-update` wipe class of bugs for this flow
- Reuses proven PUT save for persistence
- Smallest new surface area

### Rejected A: Reuse `ai-update` with Summary focus + FE "preview"

- API already persisted before response → cannot Discard server state without a second restore write
- Violates D4 spirit; higher wipe risk if merge fails

### Rejected B: Persist proposal server-side (draft table / Redis)

- New store unjustified for ephemeral Assist UX
- Hosting/provider complexity; Redis optional today

### Rejected C: Server Apply endpoint for Approve

- Correct for multi-client sync, but CV builder is single-user draft-first; DB apply races local unsaved edits
- Extra contract without clear win over FE save

### Rejected D: Client-only Gemini call

- API key must stay server-side; JWT auth boundary stays on API

---

## Impacted contracts

### Approved (must respect — no silent break)

| Contract | Impact |
|---|---|
| `api-rest-controllers` | Add one new route under `CvDocumentsController`; leave `ai-update` / suggestions / evaluation semantics unchanged |
| `supabase-jwt` | Same auth as other `current/structured/*` endpoints |
| `cv-section-catalog` / ADR-0001 | No schema change; Summary field mapping unchanged |
| `structured-cv-fields-json` | Approve save continues through existing codec / PUT |
| `google-ai-gemini-http` | New client sibling under same provider |

### Proposed (new)

| Id (proposed) | Description |
|---|---|
| `cv-summary-propose-ephemeral` | `POST .../ai-summary-propose` returns proposed Summary + change bullets; **does not persist** |
| Assist FE contract | In-memory proposal state; Approve → Summary-only local patch + existing save; Discard → clear |

Mark in `contract-registry.yaml` `proposed_contracts` during implementation PR; promote to approved when landed.

### ADR

Accepted as [ADR-0004](../docs/adr/0004-cv-summary-regenerate-propose-approve.md) (propose-then-approve Summary regeneration). Numbering skipped proposed “ADR-0003” in the design draft because ADR-0003 already covers edit canvas vs export preview.

---

## Migration / sequencing

No DB migration. No catalog version bump. No extension work (out of scope).

Preferred delivery chain note: after this design, `/operate` may run implementers; prefer `.agents/skills` `to-spec` → `to-tickets` → `implement` (+ `tdd`) → `code-review` when filing Issues.

### Implementation order

1. **backend-engineer** + **ai-llm-engineer** (can parallelize after DTO/route sketch)
2. **frontend-engineer** (depends on request/response shape; can stub UI against contract)
3. **qa-engineer** (unit + API tests; Assist UX cases)
4. **code-review-engineer** on the PR (orchestrator-owned)

---

## Ownership recommendations

| Concern | Primary | Secondary |
|---|---|---|
| Propose endpoint + DTOs + service (no save) | backend-engineer | ai-llm-engineer |
| Gemini client, schema, `CvSummaryProposeAiOptions` prompts | ai-llm-engineer | backend-engineer |
| Assist UI + facade proposal state + Approve/Discard | frontend-engineer | ui-ux-designer (light polish only if needed) |
| Test evidence | qa-engineer | relevant engineer |
| ADR-0004 / CONTEXT touch | principal-software-architect | architecture-engineer |

No ownership-matrix row change required; fits existing API / Gemini / frontend rows.

---

## Risks and open decisions

| Risk | Mitigation |
|---|---|
| Model returns full CV or empty Summary | Strict response schema; reject empty `proposedSummaryText`; ignore unknown fields |
| Multiple Summary sections | Read/write the first by `sortOrder`; do not delete extras on Approve |
| Missing Summary section | Propose with `summarySectionId: null` / empty current; Approve creates one section |
| Contact vs AppUser conflict | Prompt rule: Contact wins; AppUser fills gaps |
| User edits Summary while proposal open | Approve overwrites Summary with proposed text (intended); Discard keeps edits. Optional UX: disable canvas Summary edit while proposal pending — **ARCHITECT_PROPOSED**, not locked |
| Concurrent `ai-update` while proposal open | Clear proposal when `updateWithAi` succeeds (FE); document in facade |
| Token size of full CV | Same as existing update/eval; no new store. If later needed, ARCHITECT_PROPOSED truncation — out of scope now |

**Open decisions:** none blocking. D1–D4 locked. Soft polish (disable edit while pending) left to FE judgment / optional follow-up.

---

## Security / privacy

- JWT required; user can only propose against own CV document.
- AppUser email/name sent to Gemini as identity context — same sensitivity class as Structured CV already sent to Gemini on update/eval; no new secret types.
- Do not log full CV or API keys in handoffs/tests.
- Proposal is ephemeral (HTTP response + FE memory only) — no durable proposal store.

---

## Next actions for implementers

### backend-engineer

1. Add DTOs (`ProposeCvSummaryRequest`, `CvSummaryProposalDto`) near existing Assist contracts in `ScrapeContracts.cs` (or CV-specific models file if preferred by local style).
2. Add `POST current/structured/ai-summary-propose` on `CvDocumentsController` — mirror evaluation error mapping (404 / 400).
3. Implement `CvStructuredSummaryProposeService`: get structured → call AI client → normalize bullets → **no save**.
4. Extract `currentSummaryText` helper (first Summary section entry `Summary`).
5. Unit tests: rejects empty structured; empty AI proposal → 400; does not call save; passes AppUser identity into client; instructions optional.
6. Register DI in `ServiceCollectionExtensions.cs`.

### ai-llm-engineer

1. `ICvStructuredSummaryProposeAiClient` + `GoogleAiCvStructuredSummaryProposeClient` + response schema.
2. `CvSummaryProposeAiOptions` with defaults enforcing D3 context packing and no-invention rules.
3. Prompt includes: `{{instructions}}`, `{{currentSummary}}`, `{{identityJson}}` (AppUser + Contact excerpt), `{{payloadJson}}` (full structured CV).
4. Fake client for unit/integration tests; no live key in CI.
5. Do **not** change `GoogleAiCvStructuredUpdateClient` behavior for this task.

### frontend-engineer

1. Assist panel: new Regenerate summary block (D1/D2) with optional instructions, propose CTA, side-by-side, bullets, Approve/Discard.
2. `cv-document-api.service`: `proposeSummaryRegeneration(instructions?: string)`.
3. Facade: proposal signals (`proposing`, `proposal`, `proposalError`); `proposeSummary` / `discardSummaryProposal` / `approveSummaryProposal` (patch Summary → `save` → clear).
4. Clear proposal when Assist closes and when `updateWithAi` succeeds.
5. Page wiring: flush save before propose (same as evaluate).
6. Component/facade specs for propose → discard and propose → approve Summary-only.

### qa-engineer

1. API: propose does not persist (assert save not called / GET unchanged after propose-only).
2. API: Approve path covered via FE save tests or integration: Summary text changes; Contact/Experience unchanged.
3. FE: side-by-side renders current vs proposed; Discard restores no mutation; Approve triggers save with Summary patch only.
4. Auth: unauthenticated propose → 401/403 per existing cv-documents behavior.
5. Do not claim live Gemini pass without evidence; fakes sufficient for CI.

---

## Definition of done (design)

- [x] Current-state Assist / ai-update / Summary paths cited
- [x] Target flow with rejected alternatives
- [x] Proposed vs approved contracts distinguished
- [x] Sequencing for backend, ai-llm, frontend, qa
- [x] Thin handoff under active handoff dir
